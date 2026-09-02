using System;
using System.Collections.Generic;
using NAudio.Dsp;
using NAudio.Wave;

namespace RamaverseStudio.Audio
{
    /// <summary>
    /// Reads any NAudio-supported audio file (mp3, wav, aiff) and converts it
    /// fully in-memory to interleaved IEEE float samples at 48 kHz stereo —
    /// the format the soundboard mixer consumes.
    /// </summary>
    public sealed class WaveTo48kStereoFloat : IDisposable
    {
        private readonly AudioFileReader _reader;
        private const int TargetRate = 48000;

        public WaveTo48kStereoFloat(AudioFileReader reader)
        {
            _reader = reader;
        }

        public float[] ReadAllSamples()
        {
            // 1. Read whole file as float at its native rate/channels.
            // AudioFileReader.Read(Span<float>) returns interleaved IEEE floats.
            var native = new List<float>(1 << 19);
            float[] chunk = new float[16384];
            int read;
            while ((read = _reader.Read(chunk.AsSpan())) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    native.Add(chunk[i]);
                }
            }

            int sourceChannels = Math.Max(1, _reader.WaveFormat.Channels);
            int sourceRate = _reader.WaveFormat.SampleRate;
            int frames = native.Count / sourceChannels;

            float[] stereo;
            if (sourceChannels == 1)
            {
                stereo = new float[frames * 2];
                for (int i = 0; i < frames; i++)
                {
                    stereo[i * 2] = native[i];
                    stereo[i * 2 + 1] = native[i];
                }
            }
            else if (sourceChannels == 2)
            {
                stereo = native.ToArray();
            }
            else
            {
                // Fold >2 channels down to stereo (first channel L, average rest R)
                stereo = new float[frames * 2];
                for (int i = 0; i < frames; i++)
                {
                    stereo[i * 2] = native[i * sourceChannels];
                    float sumR = 0;
                    for (int c = 1; c < sourceChannels; c++)
                    {
                        sumR += native[i * sourceChannels + c];
                    }
                    stereo[i * 2 + 1] = sumR / (sourceChannels - 1);
                }
            }

            // 2. Resample to 48 kHz if needed
            if (sourceRate != TargetRate)
            {
                return ResampleStereo(stereo, sourceRate);
            }

            return stereo;
        }

        /// <summary>
        /// Linear-interpolation resample of interleaved stereo floats.
        /// (The WDL block resampler proved unreliable for repeated cycles, so
        /// we use the same deterministic approach as the live path.)
        /// </summary>
        private static float[] ResampleStereo(float[] input, int sourceRate)
        {
            int inFrames = input.Length / 2;
            int outFrames = (int)Math.Ceiling(inFrames * (double)TargetRate / sourceRate);
            var output = new float[outFrames * 2];

            double ratio = (double)sourceRate / TargetRate; // input steps per output

            for (int o = 0; o < outFrames; o++)
            {
                double srcPos = o * ratio;
                int i0 = (int)srcPos;
                int i1 = Math.Min(i0 + 1, inFrames - 1);
                double frac = srcPos - i0;

                output[o * 2] = (float)(input[i0 * 2] + (input[i1 * 2] - input[i0 * 2]) * frac);
                output[o * 2 + 1] = (float)(input[i0 * 2 + 1] + (input[i1 * 2 + 1] - input[i0 * 2 + 1]) * frac);
            }

            return output;
        }

        public void Dispose() => _reader.Dispose();
    }
}
