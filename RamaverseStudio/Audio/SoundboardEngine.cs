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
                using var reader = new AudioFileReader(path);
                var samples = new System.Collections.Generic.List<float>();
                byte[] buffer = new byte[8192];
                int read;
                while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < read - 1; i += 2)
                    {
                        short val = BitConverter.ToInt16(buffer, i);
                        samples.Add(val / 32768.0f);
                    }
                }
                return samples.ToArray();
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
                        samples[i * 2] = s;
                        samples[i * 2 + 1] = s;
                    }
                    return samples;

                default: // GG Horn
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
            }
        }

        public void Dispose()
        {
            while (_audioSampleQueue.TryDequeue(out _)) { }
        }
    }
}
