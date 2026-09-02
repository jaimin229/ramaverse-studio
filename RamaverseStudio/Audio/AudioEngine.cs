using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using RamaverseStudio.Models;

namespace RamaverseStudio.Audio
{
    /// <summary>
    /// Real-time audio engine: captures the microphone via WASAPI (any channel
    /// count), captures desktop audio via WASAPI loopback, resamples both to a
    /// fixed 48 kHz stereo master, runs the DSP chain, mixes in the soundboard,
    /// and emits finished 16-bit PCM for the recorder/streamer.
    /// </summary>
    public class AudioEngine : IDisposable
    {
        private const int TargetSampleRate = 48000;
        private const int TargetChannels = 2;

        private WasapiCapture? _micCapture;
        private MMDevice? _micDevice;
        private WasapiLoopbackCapture? _loopbackCapture;
        private MMDeviceEnumerator? _deviceEnumerator;

        // Mic path: source format → float → resample → 48k stereo
        private WaveFormat? _micFormat;
        private readonly ConcurrentQueue<float> _micResampledQueue = new();
        private WdlResamplingSampleProvider? _micResamplerL;
        private WdlResamplingSampleProvider? _micResamplerR;
        private int _micSourceChannels;
        private int _micQueuedSamples = 0;

        // Desktop path: loopback float 2ch → resample → 48k stereo
        private readonly ConcurrentQueue<float> _desktopSampleQueue = new();
        private WdlResamplingSampleProvider? _loopResamplerL;
        private WdlResamplingSampleProvider? _loopResamplerR;
        private int _loopSourceRate = 48000;
        private int _desktopQueuedSamples = 0;

        // Dual-Channel (Stereo) DSP Processors
        private readonly NoiseGate[] _noiseGate;
        private readonly BiQuadFilter[] _eqLow;
        private readonly BiQuadFilter[] _eqMid;
        private readonly BiQuadFilter[] _eqHigh;
        private readonly DynamicCompressor[] _compressor;
        private readonly AudioLimiter[] _limiter;
        private readonly VoiceChangerDSP[] _voiceChanger;

        public AudioFilterSettings FilterSettings { get; set; } = new AudioFilterSettings();

        public double MicVolume { get; set; } = 1.0;
        public double DesktopVolume { get; set; } = 0.8;
        public SoundboardEngine Soundboard { get; } = new SoundboardEngine();

        // ---- Audio monitoring: hear the processed mic in your headphones ----
        private bool _monitorEnabled;
        private float _monitorVolume = 0.8f;
        private WasapiOut? _monitorOut;
        private VolumeWaveProvider16? _monitorVolumeProvider;
        private readonly BufferedWaveProvider _monitorBuffer;

        public bool MonitorEnabled
        {
            get => _monitorEnabled;
            set
            {
                if (_monitorEnabled == value) return;
                if (value)
                {
                    StartMonitor();
                }
                else
                {
                    _monitorEnabled = false;
                    StopMonitor();
                }
            }
        }

        public float MonitorVolume
        {
            get => _monitorVolume;
            set
            {
                _monitorVolume = Math.Clamp(value, 0f, 1f);
                if (_monitorVolumeProvider != null)
                {
                    _monitorVolumeProvider.Volume = _monitorVolume;
                }
            }
        }

        // Metering state
        public float CurrentPeakDb { get; private set; } = -60.0f;
        public float CurrentRmsDb { get; private set; } = -60.0f;
        public float PeakHoldDb { get; private set; } = -60.0f;
        private float _peakHoldTimer = 0;

        // Desktop audio real WASAPI loopback peak
        public float DesktopPeakDb { get; private set; } = -60.0f;

        private float _duckingGain = 1.0f;
        private string _selectedMicName = "Default";

        // Reusable output buffer (16-bit stereo @ 48 kHz, 20 ms = 3840 bytes)
        private byte[] _outputBytesBuffer = new byte[3840 * 4];

        public event Action<byte[], int>? AudioSamplesProcessed;

        /// <summary>Mic-only processed track (post-DSP) for multi-track recording.</summary>
        public event Action<byte[], int>? MicTrackSamplesProcessed;

        /// <summary>Desktop-only track (post-volume/ducking) for multi-track recording.</summary>
        public event Action<byte[], int>? DesktopTrackSamplesProcessed;

        // Isolated-track output buffers for multi-track recording
        private byte[] _micTrackBuffer = new byte[3840 * 4];
        private byte[] _desktopTrackBuffer = new byte[3840 * 4];

        public bool IsRunning { get; private set; } = false;
        public string ActiveMicName => _micDevice?.FriendlyName ?? _selectedMicName;

        public AudioEngine()
        {
            _noiseGate = new[] { new NoiseGate(TargetSampleRate), new NoiseGate(TargetSampleRate) };
            _eqLow = new[] { new BiQuadFilter(TargetSampleRate), new BiQuadFilter(TargetSampleRate) };
            _eqMid = new[] { new BiQuadFilter(TargetSampleRate), new BiQuadFilter(TargetSampleRate) };
            _eqHigh = new[] { new BiQuadFilter(TargetSampleRate), new BiQuadFilter(TargetSampleRate) };
            _compressor = new[] { new DynamicCompressor(TargetSampleRate), new DynamicCompressor(TargetSampleRate) };
            _limiter = new[] { new AudioLimiter(TargetSampleRate), new AudioLimiter(TargetSampleRate) };
            _voiceChanger = new[] { new VoiceChangerDSP(TargetSampleRate), new VoiceChangerDSP(TargetSampleRate) };

            // Monitor sink runs on the default render device at master rate.
            // NAudio 3.x: buffer capacity is set via the constructor.
            _monitorBuffer = new BufferedWaveProvider(
                new WaveFormat(TargetSampleRate, 16, TargetChannels),
                TimeSpan.FromSeconds(2))
            {
                DiscardOnBufferOverflow = true
            };

            UpdateEqCoefficients();
            FilterSettings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName?.StartsWith("Eq") == true)
                {
                    UpdateEqCoefficients();
                }
            };
        }

        private void UpdateEqCoefficients()
        {
            float lowGain = (float)FilterSettings.EqLowGainDb;
            float midGain = (float)FilterSettings.EqMidGainDb;
            float highGain = (float)FilterSettings.EqHighGainDb;

            for (int ch = 0; ch < TargetChannels; ch++)
            {
                _eqLow[ch].SetLowShelf(100.0f, lowGain);
                _eqMid[ch].SetPeaking(1200.0f, midGain, 1.2f);
                _eqHigh[ch].SetHighShelf(8000.0f, highGain);
            }
        }

        public static List<string> GetMicrophoneDevices()
        {
            var list = new List<string> { "Default Microphone" };
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
                {
                    if (!string.IsNullOrWhiteSpace(device.FriendlyName))
                    {
                        list.Add(device.FriendlyName);
                    }
                }
            }
            catch { }
            return list;
        }

        public static List<string> GetOutputDevices()
        {
            var list = new List<string> { "Default Speakers / Output" };
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                {
                    if (!string.IsNullOrWhiteSpace(device.FriendlyName))
                    {
                        list.Add(device.FriendlyName);
                    }
                }
            }
            catch { }
            return list;
        }

        /// <summary>
        /// Starts capture. micDeviceName matches a name from GetMicrophoneDevices,
        /// or null/"Default" for the system default communications/recording device.
        /// </summary>
        public void Start(string? micDeviceName = null)
        {
            Stop();

            _selectedMicName = string.IsNullOrWhiteSpace(micDeviceName) ? "Default" : micDeviceName;

            // 1. Microphone capture via WASAPI (handles mono, stereo, any rate)
            try
            {
                _deviceEnumerator = new MMDeviceEnumerator();

                MMDevice micDevice;
                if (!string.IsNullOrWhiteSpace(micDeviceName) && micDeviceName != "Default Microphone")
                {
                    micDevice = FindCaptureDeviceByName(micDeviceName)
                                ?? _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                }
                else
                {
                    micDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                }

                _micDevice = micDevice;
                var micCapture = new WasapiCapture(micDevice) { ShareMode = AudioClientShareMode.Shared };
                _micFormat = micCapture.WaveFormat;
                _micSourceChannels = Math.Max(1, micCapture.WaveFormat.Channels);
                InitMicResampler(micCapture.WaveFormat.SampleRate, TargetSampleRate);

                micCapture.DataAvailable += OnMicDataAvailable;
                micCapture.RecordingStopped += OnMicRecordingStopped;
                micCapture.StartRecording();
                _micCapture = micCapture;
                IsRunning = true;
            }
            catch (Exception ex)
            {
                // Fallback: try the raw console device, then give up gracefully.
                System.Diagnostics.Debug.WriteLine($"Mic WASAPI start failed: {ex.Message}");
                _micDevice = null;
                _micCapture = null;
                IsRunning = false;
            }

            // 2. Real Desktop Audio WASAPI Loopback capture
            try
            {
                var loopback = new WasapiLoopbackCapture();
                _loopSourceRate = loopback.WaveFormat.SampleRate;
                InitLoopbackResampler(_loopSourceRate, TargetSampleRate);
                loopback.DataAvailable += OnLoopbackDataAvailable;
                loopback.RecordingStopped += OnLoopbackRecordingStopped;
                loopback.StartRecording();
                _loopbackCapture = loopback;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Loopback start failed: {ex.Message}");
            }
        }

        private void OnMicRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (IsRunning && e.Exception != null)
            {
                // Audio device hotplug disconnect: schedule automatic reconnection
                System.Diagnostics.Debug.WriteLine($"Mic disconnected: {e.Exception.Message}. Attempting auto-reconnect...");
                Task.Delay(500).ContinueWith(_ =>
                {
                    if (IsRunning)
                    {
                        try { Start(_selectedMicName); } catch { }
                    }
                });
            }
        }

        private void OnLoopbackRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (IsRunning && e.Exception != null)
            {
                System.Diagnostics.Debug.WriteLine($"Loopback stopped: {e.Exception.Message}. Attempting auto-reconnect...");
                Task.Delay(500).ContinueWith(_ =>
                {
                    if (IsRunning)
                    {
                        try { Start(_selectedMicName); } catch { }
                    }
                });
            }
        }

        private MMDevice? FindCaptureDeviceByName(string name)
        {
            try
            {
                foreach (var device in _deviceEnumerator!.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
                {
                    if (device.FriendlyName == name)
                    {
                        return device;
                    }
                }
                // Substring fallback for truncated combo-box entries
                foreach (var device in _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
                {
                    if (device.FriendlyName.Contains(name, StringComparison.OrdinalIgnoreCase))
                    {
                        return device;
                    }
                }
            }
            catch { }
            return null;
        }

        private void InitMicResampler(int sourceRate, int targetRate)
        {
            if (sourceRate == targetRate)
            {
                _micResamplerL = null;
                _micResamplerR = null;
                return;
            }
            _micResamplerL = new WdlResamplingSampleProvider(sourceRate, targetRate);
            _micResamplerR = new WdlResamplingSampleProvider(sourceRate, targetRate);
        }

        private void InitLoopbackResampler(int sourceRate, int targetRate)
        {
            if (sourceRate == targetRate)
            {
                _loopResamplerL = null;
                _loopResamplerR = null;
                return;
            }
            _loopResamplerL = new WdlResamplingSampleProvider(sourceRate, targetRate);
            _loopResamplerR = new WdlResamplingSampleProvider(sourceRate, targetRate);
        }

        /// <summary>
        /// Mic callback: WASAPI gives IEEE float in the device's own format.
        /// Convert to stereo float at 48 kHz and enqueue for the master loop.
        /// </summary>
        private void OnMicDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0 || _micFormat == null) return;

            int sourceChannels = Math.Max(1, _micSourceChannels);
            int frames = e.BytesRecorded / (sourceChannels * 4);
            if (frames == 0) return;

            var buffer = e.Buffer;
            bool bypass = !IsRunning;

            for (int i = 0; i < frames; i++)
            {
                float l = ReadChannelSample(buffer, i, 0, sourceChannels);
                float r = sourceChannels >= 2 ? ReadChannelSample(buffer, i, 1, sourceChannels) : l;

                if (bypass) continue;

                if (_micResamplerL != null && _micResamplerR != null)
                {
                    _micResamplerL.ProcessSample(l);
                    while (_micResamplerL.TryPullSample(out float outL)) EnqueueMic(outL);

                    _micResamplerR.ProcessSample(r);
                    while (_micResamplerR.TryPullSample(out float outR)) EnqueueMic(outR);
                }
                else
                {
                    EnqueueMic(l);
                    EnqueueMic(r);
                }
            }
        }

        private static float ReadChannelSample(byte[] buffer, int frame, int channel, int channels)
        {
            int idx = (frame * channels + channel) * 4;
            if (idx + 4 > buffer.Length) return 0f;
            return BitConverter.ToSingle(buffer, idx);
        }

        private void EnqueueMic(float sample)
        {
            // Bound mic queue to ~200ms to avoid drift without unbounded growth
            if (Volatile.Read(ref _micQueuedSamples) >= 19200) return;
            _micResampledQueue.Enqueue(sample);
            Interlocked.Increment(ref _micQueuedSamples);
        }

        private void EnqueueDesktop(float sample)
        {
            // Bound desktop queue to ~2s so slow video/encoder drains never
            // balloon memory; oldest audio is implicitly dropped by the cap.
            if (Volatile.Read(ref _desktopQueuedSamples) >= 192000) return;
            _desktopSampleQueue.Enqueue(sample);
            Interlocked.Increment(ref _desktopQueuedSamples);
        }

        private void OnLoopbackDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0) return;

            int floatCount = e.BytesRecorded / 4;
            if (floatCount < 2) return;

            float maxAbs = 0.0f;
            float deskVol = (float)DesktopVolume;

            var buffer = e.Buffer;
            for (int i = 0; i + 1 < floatCount; i += 2)
            {
                float l = BitConverter.ToSingle(buffer, i * 4) * deskVol;
                float r = BitConverter.ToSingle(buffer, (i + 1) * 4) * deskVol;

                float absL = Math.Abs(l), absR = Math.Abs(r);
                if (absL > maxAbs) maxAbs = absL;
                if (absR > maxAbs) maxAbs = absR;

                if (_loopResamplerL != null && _loopResamplerR != null)
                {
                    _loopResamplerL.ProcessSample(l);
                    while (_loopResamplerL.TryPullSample(out float outL))
                    {
                        EnqueueDesktop(outL);
                    }

                    _loopResamplerR.ProcessSample(r);
                    while (_loopResamplerR.TryPullSample(out float outR))
                    {
                        EnqueueDesktop(outR);
                    }
                }
                else
                {
                    EnqueueDesktop(l);
                    EnqueueDesktop(r);
                }
            }

            float db = maxAbs > 1e-5f ? (float)(20.0 * Math.Log10(maxAbs)) : -60.0f;
            DesktopPeakDb = Math.Clamp(db, -60.0f, 0.0f);
        }

        public void Stop()
        {
            if (_micCapture != null)
            {
                try
                {
                    _micCapture.DataAvailable -= OnMicDataAvailable;
                    _micCapture.StopRecording();
                    _micCapture.Dispose();
                }
                catch { }
                _micCapture = null;
            }

            if (_loopbackCapture != null)
            {
                try
                {
                    _loopbackCapture.DataAvailable -= OnLoopbackDataAvailable;
                    _loopbackCapture.StopRecording();
                    _loopbackCapture.Dispose();
                }
                catch { }
                _loopbackCapture = null;
            }

            _micDevice = null;
            if (_deviceEnumerator != null)
            {
                try { _deviceEnumerator.Dispose(); } catch { }
                _deviceEnumerator = null;
            }

            while (_micResampledQueue.TryDequeue(out _)) { }
            Interlocked.Exchange(ref _micQueuedSamples, 0);
            while (_desktopSampleQueue.TryDequeue(out _)) { }
            Interlocked.Exchange(ref _desktopQueuedSamples, 0);

            IsRunning = false;
            CurrentPeakDb = -60.0f;
            CurrentRmsDb = -60.0f;
            DesktopPeakDb = -60.0f;
        }

        /// <summary>
        /// Master output pump, driven by a 20 ms UI-side timer while running.
        /// Drains resampled mic + desktop queues, runs the DSP chain, mixes and
        /// emits exactly 20 ms of 16-bit stereo PCM per tick.
        /// </summary>
        public void PumpMasterMix()
        {
            if (!IsRunning) return;

            const int samplesPerTick = 48000 * 20 / 1000; // 960 frames
            int byteCount = samplesPerTick * 2 * 2;         // stereo, 16-bit
            if (_outputBytesBuffer.Length < byteCount)
            {
                _outputBytesBuffer = new byte[byteCount * 2];
                _micTrackBuffer = new byte[byteCount * 2];
                _desktopTrackBuffer = new byte[byteCount * 2];
            }

            float maxAbsSample = 0.0f;
            double sumSquares = 0.0;

            float inputGainLinear = (float)(Math.Pow(10.0, FilterSettings.InputGainDb / 20.0) * MicVolume);
            bool isMuted = FilterSettings.IsMuted;

            bool nsEnabled = FilterSettings.NoiseSuppressionEnabled;
            bool ngEnabled = FilterSettings.NoiseGateEnabled;
            bool eqEnabled = FilterSettings.EqEnabled;
            bool compEnabled = FilterSettings.CompressorEnabled;
            bool vcEnabled = FilterSettings.VoiceChangerEnabled;
            bool limEnabled = FilterSettings.LimiterEnabled;

            float noiseSuppressionFactor = nsEnabled ? (float)Math.Pow(10.0, FilterSettings.NoiseSuppressionAmountDb / 40.0) : 1.0f;

            double gateThresh = FilterSettings.GateThresholdDb;
            double gateAtt = FilterSettings.GateAttackMs;
            double gateHold = FilterSettings.GateHoldMs;
            double gateRel = FilterSettings.GateReleaseMs;

            double compThresh = FilterSettings.CompThresholdDb;
            double compRatio = FilterSettings.CompRatio;
            double compAtt = FilterSettings.CompAttackMs;
            double compRel = FilterSettings.CompReleaseMs;
            double compGain = FilterSettings.CompMakeupGainDb;

            double limThresh = FilterSettings.LimiterThresholdDb;
            double limRel = FilterSettings.LimiterReleaseMs;

            bool duckEnabled = FilterSettings.AutoDuckingEnabled;
            float duckThreshLinear = (float)Math.Pow(10.0, FilterSettings.DuckingThresholdDb / 20.0);
            float duckReductionLinear = (float)Math.Pow(10.0, FilterSettings.DuckingReductionDb / 20.0);
            float duckAttCoeff = (float)(1.0 - Math.Exp(-1.0 / (Math.Max(1.0, FilterSettings.DuckingAttackMs) * 0.001 * TargetSampleRate)));
            float duckRelCoeff = (float)(1.0 - Math.Exp(-1.0 / (Math.Max(10.0, FilterSettings.DuckingReleaseMs) * 0.001 * TargetSampleRate)));

            for (int i = 0; i < samplesPerTick; i++)
            {
                int channel = i % TargetChannels; // 0 = Left, 1 = Right

                bool hasMic = _micResampledQueue.TryDequeue(out float sample);
                if (!hasMic) sample = 0.0f;
                else Interlocked.Decrement(ref _micQueuedSamples);

                if (hasMic)
                {
                    sample *= inputGainLinear;

                    if (!isMuted)
                    {
                        // 1. Noise Suppression
                        if (nsEnabled && Math.Abs(sample) < 0.015f)
                        {
                            sample *= noiseSuppressionFactor;
                        }

                        // 2. Noise Gate (per-channel state)
                        if (ngEnabled)
                        {
                            sample = _noiseGate[channel].Process(sample, gateThresh, gateAtt, gateHold, gateRel);
                        }

                        // 3. 3-Band Equalizer (per-channel state)
                        if (eqEnabled)
                        {
                            sample = _eqLow[channel].Process(sample);
                            sample = _eqMid[channel].Process(sample);
                            sample = _eqHigh[channel].Process(sample);
                        }

                        // 4. Dynamic Compressor (per-channel state)
                        if (compEnabled)
                        {
                            sample = _compressor[channel].Process(sample, compThresh, compRatio, compAtt, compRel, compGain);
                        }

                        // 5. Voice Changer DSP (per-channel state)
                        if (vcEnabled)
                        {
                            sample = _voiceChanger[channel].Process(sample, FilterSettings);
                        }

                        // 6. Brickwall Limiter (per-channel state)
                        if (limEnabled)
                        {
                            sample = _limiter[channel].Process(sample, limThresh, limRel);
                        }
                    }
                    else
                    {
                        sample = 0.0f;
                    }
                }

                // Metering stats (Microphone post-DSP)
                float absSample = Math.Abs(sample);
                if (absSample > maxAbsSample) maxAbsSample = absSample;
                sumSquares += sample * sample;

                // Mix Desktop Loopback audio sample with Auto-Ducking
                float isolatedDesktop = 0.0f;
                if (_desktopSampleQueue.TryDequeue(out float desktopSample))
                {
                    Interlocked.Decrement(ref _desktopQueuedSamples);

                    if (duckEnabled)
                    {
                        bool isSpeaking = !isMuted && absSample > duckThreshLinear;
                        float targetDucking = isSpeaking ? duckReductionLinear : 1.0f;
                        float coeff = isSpeaking ? duckAttCoeff : duckRelCoeff;
                        _duckingGain += (targetDucking - _duckingGain) * coeff;
                        desktopSample *= _duckingGain;
                    }

                    isolatedDesktop = desktopSample;
                    sample += desktopSample;
                }

                // Isolated mic track (post-DSP, pre-mix) for multi-track recording
                float micTrackSample = hasMic ? sample : 0.0f;
                short micShort = (short)(Math.Clamp(micTrackSample, -1f, 1f) * 32767.0f);
                _micTrackBuffer[i * 2] = (byte)(micShort & 0xFF);
                _micTrackBuffer[i * 2 + 1] = (byte)((micShort >> 8) & 0xFF);

                // Isolated desktop track for multi-track recording
                short deskShort = (short)(Math.Clamp(isolatedDesktop, -1f, 1f) * 32767.0f);
                _desktopTrackBuffer[i * 2] = (byte)(deskShort & 0xFF);
                _desktopTrackBuffer[i * 2 + 1] = (byte)((deskShort >> 8) & 0xFF);

                // Mix Soundboard Cue audio
                if (Soundboard.TryGetNextSample(out float sfxSample))
                {
                    sample += sfxSample;
                }

                // Final Master Ceiling Clamp
                sample = Math.Clamp(sample, -1.0f, 1.0f);
                short outShort = (short)(sample * 32767.0f);

                _outputBytesBuffer[i * 2] = (byte)(outShort & 0xFF);
                _outputBytesBuffer[i * 2 + 1] = (byte)((outShort >> 8) & 0xFF);
            }

            // Calculate dBFS
            float peakDb = maxAbsSample > 1e-5f ? (float)(20.0 * Math.Log10(maxAbsSample)) : -60.0f;
            float rms = (float)Math.Sqrt(sumSquares / samplesPerTick);
            float rmsDb = rms > 1e-5f ? (float)(20.0 * Math.Log10(rms)) : -60.0f;

            CurrentPeakDb = Math.Clamp(peakDb, -60.0f, 0.0f);
            CurrentRmsDb = Math.Clamp(rmsDb, -60.0f, 0.0f);

            if (CurrentPeakDb > PeakHoldDb)
            {
                PeakHoldDb = CurrentPeakDb;
                _peakHoldTimer = 0;
            }
            else
            {
                _peakHoldTimer += 0.02f;
                if (_peakHoldTimer > 1.2f)
                {
                    PeakHoldDb = Math.Max(-60.0f, PeakHoldDb - 1.5f);
                }
            }

            // Deliver real 16-bit PCM stereo mixed master to FFmpeg recorder / streamer
            AudioSamplesProcessed?.Invoke(_outputBytesBuffer, byteCount);

            // Isolated tracks for multi-track recording (no-op when unsubscribed)
            MicTrackSamplesProcessed?.Invoke(_micTrackBuffer, byteCount);
            DesktopTrackSamplesProcessed?.Invoke(_desktopTrackBuffer, byteCount);

            // Mirror the master mix into the monitor sink when armed.
            if (_monitorEnabled)
            {
                _monitorBuffer.AddSamples(_outputBytesBuffer, 0, byteCount);
            }
        }

        /// <summary>
        /// Starts playback of the processed master mix on the default output
        /// device so the streamer can hear exactly what the audience hears.
        /// </summary>
        public void StartMonitor()
        {
            if (_monitorOut != null) return;

            try
            {
                _monitorVolumeProvider = new VolumeWaveProvider16(_monitorBuffer)
                {
                    Volume = _monitorVolume
                };

                _monitorOut = new WasapiOut(AudioClientShareMode.Shared, 50);
                _monitorOut.Init(_monitorVolumeProvider);
                _monitorOut.Play();
                _monitorEnabled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Monitor start failed: {ex.Message}");
                _monitorOut = null;
                _monitorVolumeProvider = null;
                _monitorEnabled = false;
            }
        }

        private void StopMonitor()
        {
            try
            {
                _monitorOut?.Stop();
                _monitorOut?.Dispose();
            }
            catch { }
            _monitorOut = null;
            _monitorVolumeProvider = null;
            try { _monitorBuffer.ClearBuffer(); } catch { }
        }

        public void Dispose()
        {
            StopMonitor();
            Stop();
        }
    }
}
