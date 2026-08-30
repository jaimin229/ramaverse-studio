using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using RamaverseStudio.Models;

namespace RamaverseStudio.Audio
{
    public class AudioEngine : IDisposable
    {
        private IWaveIn? _waveIn;
        private WasapiLoopbackCapture? _loopbackCapture;
        private readonly int _sampleRate = 48000;
        private readonly int _channels = 2;

        // Dual-Channel (Stereo) DSP Processors
        private readonly NoiseGate[] _noiseGate;
        private readonly BiQuadFilter[] _eqLow;
        private readonly BiQuadFilter[] _eqMid;
        private readonly BiQuadFilter[] _eqHigh;
        private readonly DynamicCompressor[] _compressor;
        private readonly AudioLimiter[] _limiter;
        private readonly VoiceChangerDSP[] _voiceChanger;

        public AudioFilterSettings FilterSettings { get; } = new AudioFilterSettings();

        // Volumes
        public double MicVolume { get; set; } = 1.0;
        public double DesktopVolume { get; set; } = 0.8;

        // Metering state
        public float CurrentPeakDb { get; private set; } = -60.0f;
        public float CurrentRmsDb { get; private set; } = -60.0f;
        public float PeakHoldDb { get; private set; } = -60.0f;
        private float _peakHoldTimer = 0;

        // Desktop audio real WASAPI loopback peak
        public float DesktopPeakDb { get; private set; } = -60.0f;

        // Thread-safe circular queue for Desktop Loopback samples to mix with Mic
        private readonly ConcurrentQueue<float> _desktopSampleQueue = new ConcurrentQueue<float>();
        private float _duckingGain = 1.0f;

        // Reusable audio buffer to eliminate GC allocations
        private byte[] _outputBytesBuffer = new byte[65536];

        // Event for raw processed PCM (16-bit 48kHz stereo) for FFmpeg recorder / streamer
        public event Action<byte[], int>? AudioSamplesProcessed;

        public bool IsRunning { get; private set; } = false;

        public AudioEngine()
        {
            _noiseGate = new[] { new NoiseGate(_sampleRate), new NoiseGate(_sampleRate) };
            _eqLow = new[] { new BiQuadFilter(_sampleRate), new BiQuadFilter(_sampleRate) };
            _eqMid = new[] { new BiQuadFilter(_sampleRate), new BiQuadFilter(_sampleRate) };
            _eqHigh = new[] { new BiQuadFilter(_sampleRate), new BiQuadFilter(_sampleRate) };
            _compressor = new[] { new DynamicCompressor(_sampleRate), new DynamicCompressor(_sampleRate) };
            _limiter = new[] { new AudioLimiter(_sampleRate), new AudioLimiter(_sampleRate) };
            _voiceChanger = new[] { new VoiceChangerDSP(_sampleRate), new VoiceChangerDSP(_sampleRate) };

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

            for (int ch = 0; ch < _channels; ch++)
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
                int count = WaveIn.DeviceCount;
                for (int i = 0; i < count; i++)
                {
                    var caps = WaveIn.GetCapabilities(i);
                    list.Add(caps.ProductName);
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
                int count = WaveOut.DeviceCount;
                for (int i = 0; i < count; i++)
                {
                    var caps = WaveOut.GetCapabilities(i);
                    list.Add(caps.ProductName);
                }
            }
            catch { }
            return list;
        }

        public void Start(int deviceIndex = -1)
        {
            Stop();

            // Clear residual loopback queue
            while (_desktopSampleQueue.TryDequeue(out _)) { }

            // 1. Microphone capture
            try
            {
                var waveIn = new WaveIn
                {
                    DeviceNumber = deviceIndex >= 0 ? deviceIndex : 0,
                    WaveFormat = new WaveFormat(_sampleRate, 16, _channels),
                    BufferMilliseconds = 20
                };
                waveIn.DataAvailable += OnDataAvailable;
                waveIn.StartRecording();
                _waveIn = waveIn;
                IsRunning = true;
            }
            catch (Exception)
            {
                try
                {
                    var waveIn = new WaveIn
                    {
                        DeviceNumber = 0,
                        WaveFormat = new WaveFormat(_sampleRate, 16, _channels),
                        BufferMilliseconds = 20
                    };
                    waveIn.DataAvailable += OnDataAvailable;
                    waveIn.StartRecording();
                    _waveIn = waveIn;
                    IsRunning = true;
                }
                catch
                {
                    IsRunning = false;
                }
            }

            // 2. Real Desktop Audio WASAPI Loopback capture
            try
            {
                var loopback = new WasapiLoopbackCapture();
                loopback.DataAvailable += OnLoopbackDataAvailable;
                loopback.StartRecording();
                _loopbackCapture = loopback;
            }
            catch { }
        }

        public void Stop()
        {
            if (_waveIn != null)
            {
                try
                {
                    _waveIn.DataAvailable -= OnDataAvailable;
                    _waveIn.StopRecording();
                    _waveIn.Dispose();
                }
                catch { }
                _waveIn = null;
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

            IsRunning = false;
            CurrentPeakDb = -60.0f;
            CurrentRmsDb = -60.0f;
            DesktopPeakDb = -60.0f;
        }

        private void OnLoopbackDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0) return;

            // WASAPI Loopback delivers 32-bit Float samples
            int floatCount = e.BytesRecorded / 4;
            float maxAbs = 0.0f;
            float deskVol = (float)DesktopVolume;

            for (int i = 0; i < floatCount; i++)
            {
                float sample = BitConverter.ToSingle(e.Buffer, i * 4) * deskVol;
                float abs = Math.Abs(sample);
                if (abs > maxAbs) maxAbs = abs;

                // Enqueue for mixing with mic audio (limit queue depth to avoid latency drift)
                if (_desktopSampleQueue.Count < 96000)
                {
                    _desktopSampleQueue.Enqueue(sample);
                }
            }

            float db = maxAbs > 1e-5f ? (float)(20.0 * Math.Log10(maxAbs)) : -60.0f;
            DesktopPeakDb = Math.Clamp(db, -60.0f, 0.0f);
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0) return;

            int sampleCount = e.BytesRecorded / 2; // 16-bit PCM count
            if (_outputBytesBuffer.Length < e.BytesRecorded)
            {
                _outputBytesBuffer = new byte[e.BytesRecorded * 2];
            }

            float maxAbsSample = 0.0f;
            double sumSquares = 0.0;

            float inputGainLinear = (float)(Math.Pow(10.0, FilterSettings.InputGainDb / 20.0) * MicVolume);
            bool isMuted = FilterSettings.IsMuted;
            float noiseSuppressionFactor = FilterSettings.NoiseSuppressionEnabled ? (float)Math.Pow(10.0, FilterSettings.NoiseSuppressionAmountDb / 40.0) : 1.0f;

            // Pre-cache filter enable flags for performance
            bool nsEnabled = FilterSettings.NoiseSuppressionEnabled;
            bool ngEnabled = FilterSettings.NoiseGateEnabled;
            bool eqEnabled = FilterSettings.EqEnabled;
            bool compEnabled = FilterSettings.CompressorEnabled;
            bool vcEnabled = FilterSettings.VoiceChangerEnabled;
            bool limEnabled = FilterSettings.LimiterEnabled;

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

            // Auto-Ducking precalculations
            bool duckEnabled = FilterSettings.AutoDuckingEnabled;
            float duckThreshLinear = (float)Math.Pow(10.0, FilterSettings.DuckingThresholdDb / 20.0);
            float duckReductionLinear = (float)Math.Pow(10.0, FilterSettings.DuckingReductionDb / 20.0);
            float duckAttCoeff = (float)(1.0 - Math.Exp(-1.0 / (Math.Max(1.0, FilterSettings.DuckingAttackMs) * 0.001 * _sampleRate)));
            float duckRelCoeff = (float)(1.0 - Math.Exp(-1.0 / (Math.Max(10.0, FilterSettings.DuckingReleaseMs) * 0.001 * _sampleRate)));

            for (int i = 0; i < sampleCount; i++)
            {
                int channel = i % _channels; // 0 = Left, 1 = Right
                short rawShort = BitConverter.ToInt16(e.Buffer, i * 2);
                float sample = (rawShort / 32768.0f) * inputGainLinear;

                if (isMuted)
                {
                    sample = 0.0f;
                }
                else
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

                // Metering stats (Microphone)
                float absSample = Math.Abs(sample);
                if (absSample > maxAbsSample) maxAbsSample = absSample;
                sumSquares += sample * sample;

                // Mix Desktop Loopback audio sample with Auto-Ducking
                if (_desktopSampleQueue.TryDequeue(out float desktopSample))
                {
                    if (duckEnabled)
                    {
                        bool isSpeaking = !isMuted && absSample > duckThreshLinear;
                        float targetDucking = isSpeaking ? duckReductionLinear : 1.0f;
                        float coeff = isSpeaking ? duckAttCoeff : duckRelCoeff;
                        _duckingGain += (targetDucking - _duckingGain) * coeff;
                        desktopSample *= _duckingGain;
                    }

                    sample += desktopSample;
                }

                // Final Master Ceiling Clamp
                sample = Math.Clamp(sample, -1.0f, 1.0f);
                short outShort = (short)(sample * 32767.0f);

                _outputBytesBuffer[i * 2] = (byte)(outShort & 0xFF);
                _outputBytesBuffer[i * 2 + 1] = (byte)((outShort >> 8) & 0xFF);
            }

            // Calculate dBFS
            float peakDb = maxAbsSample > 1e-5f ? (float)(20.0 * Math.Log10(maxAbsSample)) : -60.0f;
            float rms = (float)Math.Sqrt(sumSquares / sampleCount);
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
            AudioSamplesProcessed?.Invoke(_outputBytesBuffer, e.BytesRecorded);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
