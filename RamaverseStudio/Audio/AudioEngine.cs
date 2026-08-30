using System;
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
        private readonly int _sampleRate = 48000;
        private readonly int _channels = 2;

        // DSP Processors
        private readonly NoiseGate _noiseGate;
        private readonly BiQuadFilter _eqLow;
        private readonly BiQuadFilter _eqMid;
        private readonly BiQuadFilter _eqHigh;
        private readonly DynamicCompressor _compressor;
        private readonly AudioLimiter _limiter;
        private readonly VoiceChangerDSP _voiceChanger;

        public AudioFilterSettings FilterSettings { get; } = new AudioFilterSettings();

        // Metering state
        public float CurrentPeakDb { get; private set; } = -60.0f;
        public float CurrentRmsDb { get; private set; } = -60.0f;
        public float PeakHoldDb { get; private set; } = -60.0f;
        private float _peakHoldTimer = 0;

        // Desktop audio simulated / loopback peak
        public float DesktopPeakDb { get; private set; } = -60.0f;

        // Event for raw processed PCM (16-bit 48kHz stereo) for FFmpeg recorder / streamer
        public event Action<byte[], int>? AudioSamplesProcessed;

        public bool IsRunning { get; private set; } = false;

        public AudioEngine()
        {
            _noiseGate = new NoiseGate(_sampleRate);
            _eqLow = new BiQuadFilter(_sampleRate);
            _eqMid = new BiQuadFilter(_sampleRate);
            _eqHigh = new BiQuadFilter(_sampleRate);
            _compressor = new DynamicCompressor(_sampleRate);
            _limiter = new AudioLimiter(_sampleRate);
            _voiceChanger = new VoiceChangerDSP(_sampleRate);

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
            _eqLow.SetLowShelf(100.0f, (float)FilterSettings.EqLowGainDb);
            _eqMid.SetPeaking(1200.0f, (float)FilterSettings.EqMidGainDb, 1.2f);
            _eqHigh.SetHighShelf(8000.0f, (float)FilterSettings.EqHighGainDb);
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
                // Fallback to default if selected index fails
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
            IsRunning = false;
            CurrentPeakDb = -60.0f;
            CurrentRmsDb = -60.0f;
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0) return;

            int sampleCount = e.BytesRecorded / 2; // 16-bit
            byte[] outputBytes = new byte[e.BytesRecorded];

            float maxAbsSample = 0.0f;
            double sumSquares = 0.0;

            float inputGainLinear = (float)Math.Pow(10.0, FilterSettings.InputGainDb / 20.0);
            bool isMuted = FilterSettings.IsMuted;

            for (int i = 0; i < sampleCount; i++)
            {
                short rawShort = BitConverter.ToInt16(e.Buffer, i * 2);
                float sample = (rawShort / 32768.0f) * inputGainLinear;

                if (isMuted)
                {
                    sample = 0.0f;
                }
                else
                {
                    // 1. Noise Suppression (attenuate low level ambient hiss)
                    if (FilterSettings.NoiseSuppressionEnabled && Math.Abs(sample) < 0.015f)
                    {
                        float suppressionFactor = (float)Math.Pow(10.0, FilterSettings.NoiseSuppressionAmountDb / 40.0);
                        sample *= suppressionFactor;
                    }

                    // 2. Noise Gate
                    if (FilterSettings.NoiseGateEnabled)
                    {
                        sample = _noiseGate.Process(sample,
                            FilterSettings.GateThresholdDb,
                            FilterSettings.GateAttackMs,
                            FilterSettings.GateHoldMs,
                            FilterSettings.GateReleaseMs);
                    }

                    // 3. 3-Band Equalizer
                    if (FilterSettings.EqEnabled)
                    {
                        sample = _eqLow.Process(sample);
                        sample = _eqMid.Process(sample);
                        sample = _eqHigh.Process(sample);
                    }

                    // 4. Dynamic Compressor
                    if (FilterSettings.CompressorEnabled)
                    {
                        sample = _compressor.Process(sample,
                            FilterSettings.CompThresholdDb,
                            FilterSettings.CompRatio,
                            FilterSettings.CompAttackMs,
                            FilterSettings.CompReleaseMs,
                            FilterSettings.CompMakeupGainDb);
                    }

                    // 5. Voice Changer DSP
                    if (FilterSettings.VoiceChangerEnabled)
                    {
                        sample = _voiceChanger.Process(sample, FilterSettings);
                    }

                    // 6. Brickwall Limiter
                    if (FilterSettings.LimiterEnabled)
                    {
                        sample = _limiter.Process(sample,
                            FilterSettings.LimiterThresholdDb,
                            FilterSettings.LimiterReleaseMs);
                    }
                }

                // Metering stats
                float absSample = Math.Abs(sample);
                if (absSample > maxAbsSample) maxAbsSample = absSample;
                sumSquares += sample * sample;

                // Clamp to 16-bit PCM
                sample = Math.Clamp(sample, -1.0f, 1.0f);
                short outShort = (short)(sample * 32767.0f);
                byte[] sampleBytes = BitConverter.GetBytes(outShort);
                outputBytes[i * 2] = sampleBytes[0];
                outputBytes[i * 2 + 1] = sampleBytes[1];
            }

            // Calculate dBFS
            float peakDb = maxAbsSample > 1e-5f ? (float)(20.0 * Math.Log10(maxAbsSample)) : -60.0f;
            float rms = (float)Math.Sqrt(sumSquares / sampleCount);
            float rmsDb = rms > 1e-5f ? (float)(20.0 * Math.Log10(rms)) : -60.0f;

            CurrentPeakDb = Math.Clamp(peakDb, -60.0f, 0.0f);
            CurrentRmsDb = Math.Clamp(rmsDb, -60.0f, 0.0f);

            if (CurrentPeakDb >= PeakHoldDb)
            {
                PeakHoldDb = CurrentPeakDb;
                _peakHoldTimer = 1.2f; // hold for 1.2s
            }
            else
            {
                _peakHoldTimer -= 0.02f;
                if (_peakHoldTimer <= 0)
                {
                    PeakHoldDb = Math.Max(-60.0f, PeakHoldDb - 1.5f);
                }
            }

            AudioSamplesProcessed?.Invoke(outputBytes, outputBytes.Length);
        }

        public void UpdateDesktopAudioMeter(float levelDb)
        {
            DesktopPeakDb = Math.Clamp(levelDb, -60.0f, 0.0f);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
