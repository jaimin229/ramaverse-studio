using System;
using System.Collections.Concurrent;
using System.IO;
using NAudio.Wave;
using RamaverseStudio.Models;

namespace RamaverseStudio.Audio
{
    public class SoundboardItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Sound";
        public string Icon { get; set; } = "🔊";
        public string? CustomFilePath { get; set; }
        public SoundEffectType EffectType { get; set; } = SoundEffectType.Custom;
    }

    public enum SoundEffectType
    {
        AirHorn,
        GGHorn,
        Applause,
        LevelUp,
        Laser,
        Buzzer,
        VictoryChime,
        Custom
    }

    public class SoundboardEngine : IDisposable
    {
        private const int SampleRate = 48000;
        private const int Channels = 2;

        public double Volume { get; set; } = 0.8;
        private readonly ConcurrentQueue<float> _audioSampleQueue = new ConcurrentQueue<float>();
        public System.Collections.ObjectModel.ObservableCollection<SoundboardItem> CustomPads { get; } = new();

        public void AddCustomPad(string name, string filePath, string icon = "🎵")
        {
            CustomPads.Add(new SoundboardItem
            {
                Name = name,
                CustomFilePath = filePath,
                Icon = icon,
                EffectType = SoundEffectType.Custom
            });
        }

        public void PlayPadByIndex(int index)
        {
            if (index >= 0 && index < CustomPads.Count)
            {
                var pad = CustomPads[index];
                PlaySound(pad.EffectType, pad.CustomFilePath);
            }
        }

        public void StopAll()
        {
            while (_audioSampleQueue.TryDequeue(out _)) { }
        }

        public void PlaySound(SoundEffectType effect, string? filePath = null)
        {
            float[] samples;

            if (effect == SoundEffectType.Custom && !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                samples = LoadAudioFile(filePath);
            }
            else
            {
                samples = GenerateProceduralSound(effect);
            }

            float vol = (float)Volume;
            for (int i = 0; i < samples.Length; i++)
            {
                _audioSampleQueue.Enqueue(samples[i] * vol);
            }
        }

        public bool TryGetNextSample(out float sample)
        {
            return _audioSampleQueue.TryDequeue(out sample);
        }

        private float[] LoadAudioFile(string path)
        {
            try
            {
                // AudioFileReader delivers IEEE float samples at the file's own
                // rate; previous code misread them as 16-bit PCM (pure noise).
                using var reader = new AudioFileReader(path);
                reader.Volume = 1.0f;

                var target = new RamaverseStudio.Audio.WaveTo48kStereoFloat(reader);
                return target.ReadAllSamples();
            }
            catch
            {
                return GenerateProceduralSound(SoundEffectType.LevelUp);
            }
        }

        private float[] GenerateProceduralSound(SoundEffectType type)
        {
            int durationSamples;
            float[] samples;

            switch (type)
            {
                case SoundEffectType.AirHorn:
                    durationSamples = SampleRate * 1; // 1 second
                    samples = new float[durationSamples * Channels];
                    for (int i = 0; i < durationSamples; i++)
                    {
                        double t = (double)i / SampleRate;
                        // Multi-tone dissonant brass chord (F4, A4, C5, Eb5)
                        float s = (float)(
                            0.35 * Math.Sin(2 * Math.PI * 349.23 * t) +
                            0.30 * Math.Sin(2 * Math.PI * 440.00 * t) +
                            0.25 * Math.Sin(2 * Math.PI * 523.25 * t) +
                            0.20 * Math.Sin(2 * Math.PI * 622.25 * t)
                        );
                        // Horn envelope
                        float env = (float)(Math.Min(1.0, t * 20.0) * Math.Max(0.0, 1.0 - t * 0.8));
                        s *= env * 0.6f;
                        samples[i * 2] = s;
                        samples[i * 2 + 1] = s;
                    }
                    return samples;

                case SoundEffectType.LevelUp:
                    durationSamples = (int)(SampleRate * 0.6);
                    samples = new float[durationSamples * Channels];
                    for (int i = 0; i < durationSamples; i++)
                    {
                        double t = (double)i / SampleRate;
                        double freq = 440.0 + (t * 880.0); // Arpeggio sweep
                        float s = (float)(0.4 * Math.Sin(2 * Math.PI * freq * t));
                        float env = (float)(1.0 - (t / 0.6));
                        s *= env;
                        samples[i * 2] = s;
                        samples[i * 2 + 1] = s;
                    }
                    return samples;

                case SoundEffectType.VictoryChime:
                    durationSamples = (int)(SampleRate * 1.2);
                    samples = new float[durationSamples * Channels];
                    for (int i = 0; i < durationSamples; i++)
                    {
                        double t = (double)i / SampleRate;
                        double freq = t < 0.3 ? 523.25 : (t < 0.6 ? 659.25 : 783.99); // C5 -> E5 -> G5
                        float s = (float)(0.4 * Math.Sin(2 * Math.PI * freq * t) + 0.15 * Math.Sin(2 * Math.PI * freq * 2 * t));
                        float env = (float)(1.0 - (t / 1.2));
                        s *= env;
                        samples[i * 2] = s;
                        samples[i * 2 + 1] = s;
                    }
                    return samples;

                case SoundEffectType.Laser:
                    durationSamples = (int)(SampleRate * 0.3);
                    samples = new float[durationSamples * Channels];
                    for (int i = 0; i < durationSamples; i++)
                    {
                        double t = (double)i / SampleRate;
                        double freq = 1800.0 * Math.Exp(-12.0 * t); // Pitch drop
                        float s = (float)(0.5 * Math.Sin(2 * Math.PI * freq * t));
                        samples[i * 2] = s;
                        samples[i * 2 + 1] = s;
                    }
                    return samples;

                case SoundEffectType.Buzzer:
                    durationSamples = (int)(SampleRate * 0.5);
                    samples = new float[durationSamples * Channels];
                    for (int i = 0; i < durationSamples; i++)
                    {
                        double t = (double)i / SampleRate;
                        // Low sawtooth wave
                        float s = (float)((t * 120.0 % 1.0) * 2.0 - 1.0) * 0.4f;
                        float env = (float)(1.0 - t / 0.5);
                        s *= env;
                        samples[i * 2] = s;
                        samples[i * 2 + 1] = s;
                    }
                    return samples;

                case SoundEffectType.GGHorn:
                    durationSamples = (int)(SampleRate * 0.8);
                    samples = new float[durationSamples * Channels];
                    for (int i = 0; i < durationSamples; i++)
                    {
                        double t = (double)i / SampleRate;
                        float s = (float)(0.4 * Math.Sin(2 * Math.PI * 587.33 * t));
                        float env = (float)(Math.Min(1.0, t * 15.0) * (1.0 - t / 0.8));
                        s *= env;
                        samples[i * 2] = s;
                        samples[i * 2 + 1] = s;
                    }
                    return samples;

                case SoundEffectType.Applause:
                    durationSamples = (int)(SampleRate * 1.8);
                    samples = new float[durationSamples * Channels];
                    var applauseRandom = new Random(20260830);
                    float[] claps = new float[durationSamples];
                    // Random transient claps (Poisson-ish) + crowd noise bed
                    for (int i = 0; i < durationSamples; i++)
                    {
                        double t = (double)i / SampleRate;
                        claps[i] = 0;
                    }
                    int nextClap = 0;
                    while (nextClap < durationSamples)
                    {
                        float amp = 0.25f + (float)applauseRandom.NextDouble() * 0.45f;
                        int decay = (int)(SampleRate * 0.035);
                        for (int k = 0; k < decay && nextClap + k < durationSamples; k++)
                        {
                            claps[nextClap + k] += amp * (float)(applauseRandom.NextDouble() * 2 - 1) * (float)Math.Exp(-k / (decay * 0.3));
                        }
                        nextClap += (int)(SampleRate * (0.004 + applauseRandom.NextDouble() * 0.012));
                    }
                    for (int i = 0; i < durationSamples; i++)
                    {
                        double t = (double)i / SampleRate;
                        float crowd = (float)(applauseRandom.NextDouble() * 2 - 1) * 0.04f;
                        float env = (float)(Math.Min(1.0, t * 8.0) * Math.Max(0.0, 1.0 - t * 0.45));
                        float s = Math.Clamp(claps[i] + crowd, -1f, 1f) * env;
                        samples[i * 2] = s;
                        samples[i * 2 + 1] = s;
                    }
                    return samples;
            }

            // Unreachable in practice (all SoundEffectType values handled above),
            // but keeps the compiler satisfied if the enum grows.
            return GenerateProceduralSound(SoundEffectType.GGHorn);
        }

        public void Dispose()
        {
            while (_audioSampleQueue.TryDequeue(out _)) { }
        }
    }
}
