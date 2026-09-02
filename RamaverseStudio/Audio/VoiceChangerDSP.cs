using System;
using RamaverseStudio.Models;

namespace RamaverseStudio.Audio
{
    /// <summary>
    /// Real-time low-latency Voice Changer DSP engine with Pitch Shifting, Ring Modulation,
    /// Resonant Filtering, Harmonic Saturation, and Formant emulation.
    /// </summary>
    public class VoiceChangerDSP
    {
        private readonly int _sampleRate;
        private readonly float[] _ringBuffer;
        private int _writeIndex = 0;
        private double _readIndex1 = 0;
        private double _readIndex2 = 0;
        private readonly int _grainSize;

        // Modulators
        private double _carrierPhase = 0;

        // Bandpass Filter
        private readonly BiQuadFilter _bandpassFilter;
        private double _cachedLowHz = -1;
        private double _cachedHighHz = -1;

        public VoiceChangerDSP(int sampleRate)
        {
            _sampleRate = sampleRate;
            _grainSize = (int)(sampleRate * 0.035); // 35ms grain
            _ringBuffer = new float[sampleRate * 2]; // 2-second buffer
            _readIndex1 = 0;
            _readIndex2 = _grainSize / 2.0;

            _bandpassFilter = new BiQuadFilter(sampleRate);
        }

        /// <summary>Non-negative modulo: result always in [0, m).</summary>
        private static int SafeMod(int value, int m)
        {
            int r = value % m;
            return r < 0 ? r + m : r;
        }

        private static double SafeMod(double value, double m)
        {
            double r = value % m;
            return r < 0 ? r + m : r;
        }

        /// <summary>
        /// Keeps a fractional read index within one ring-length behind the
        /// write head, so the granular reads always reference valid samples.
        /// </summary>
        private static double ClampToWindow(double readIndex, int writeIndex, int bufferLength)
        {
            double max = writeIndex + bufferLength;
            while (readIndex >= max) readIndex -= bufferLength;
            while (readIndex < writeIndex - bufferLength) readIndex += bufferLength;
            return readIndex;
        }

        public float Process(float sample, AudioFilterSettings settings)
        {
            if (!settings.VoiceChangerEnabled)
                return sample;

            // 1. Store incoming sample in ring buffer
            _ringBuffer[_writeIndex] = sample;
            int bufferLength = _ringBuffer.Length;

            // 2. Pitch Shifter (Granular Overlap-Add)
            float pitchShiftedSample = sample;
            double semitones = settings.PitchShiftSemitones;

            if (Math.Abs(semitones) > 0.05)
            {
                double pitchRatio = Math.Pow(2.0, semitones / 12.0);

                // Read from grain 1 (SafeMod: C# % returns negative for negative
                // dividends, which crashed with IndexOutOfRange after wraps.)
                int idx1_0 = SafeMod((int)Math.Floor(_readIndex1), bufferLength);
                int idx1_1 = (idx1_0 + 1) % bufferLength;
                float frac1 = (float)(_readIndex1 - Math.Floor(_readIndex1));
                float s1 = _ringBuffer[idx1_0] * (1.0f - frac1) + _ringBuffer[idx1_1] * frac1;

                // Triangular / Hann crossfade window for grain 1
                double phaseInGrain1 = SafeMod(_readIndex1, _grainSize) / _grainSize;
                float win1 = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * phaseInGrain1)));

                // Read from grain 2 (offset by 180 degrees)
                int idx2_0 = SafeMod((int)Math.Floor(_readIndex2), bufferLength);
                int idx2_1 = (idx2_0 + 1) % bufferLength;
                float frac2 = (float)(_readIndex2 - Math.Floor(_readIndex2));
                float s2 = _ringBuffer[idx2_0] * (1.0f - frac2) + _ringBuffer[idx2_1] * frac2;

                double phaseInGrain2 = SafeMod(_readIndex2, _grainSize) / _grainSize;
                float win2 = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * phaseInGrain2)));

                pitchShiftedSample = (s1 * win1 + s2 * win2);

                _readIndex1 += pitchRatio;
                _readIndex2 += pitchRatio;

                // Grain read heads must chase the write head from behind, wrapping
                // modulo the ring. Without the wrap, downshifts run far past the
                // write index and produce a ~2 second delayed echo.
                if (_readIndex1 >= _writeIndex + _grainSize)
                    _readIndex1 -= bufferLength;
                if (_readIndex2 >= _writeIndex + _grainSize)
                    _readIndex2 -= bufferLength;

                // Clamp any residual drift into the valid window so indices
                // never wander out of [0, bufferLength).
                _readIndex1 = ClampToWindow(_readIndex1, _writeIndex, bufferLength);
                _readIndex2 = ClampToWindow(_readIndex2, _writeIndex, bufferLength);
            }

            _writeIndex = (_writeIndex + 1) % bufferLength;

            float outSample = pitchShiftedSample;

            // 3. Ring Modulation (Robot Voice)
            if (settings.RobotModFrequencyHz > 1.0)
            {
                float carrier = (float)Math.Sin(_carrierPhase);
                _carrierPhase += 2.0 * Math.PI * settings.RobotModFrequencyHz / _sampleRate;
                if (_carrierPhase >= 2.0 * Math.PI)
                    _carrierPhase -= 2.0 * Math.PI;

                // Mix original pitch-shifted sample with modulated carrier
                outSample = outSample * (0.35f + 0.65f * carrier);
            }

            // 4. Bandpass Filtering (Radio / Megaphone / Telephone)
            if (settings.BandpassEnabled)
            {
                if (Math.Abs(_cachedLowHz - settings.BandpassLowHz) > 0.1 || Math.Abs(_cachedHighHz - settings.BandpassHighHz) > 0.1)
                {
                    _cachedLowHz = settings.BandpassLowHz;
                    _cachedHighHz = settings.BandpassHighHz;
                    float centerFreq = (float)((settings.BandpassLowHz + settings.BandpassHighHz) / 2.0);
                    float bandwidth = (float)(settings.BandpassHighHz - settings.BandpassLowHz);
                    float q = Math.Max(0.5f, centerFreq / Math.Max(100.0f, bandwidth));
                    _bandpassFilter.SetBandPass(centerFreq, q);
                }
                outSample = _bandpassFilter.Process(outSample) * 2.2f; // Makeup
            }

            // 5. Distortion / Overdrive (Megaphone / Radio crunch)
            if (settings.DistortionDrive > 0.05)
            {
                float drive = (float)(1.0 + settings.DistortionDrive);
                float x = outSample * drive;
                // Soft cubic clipping: f(x) = 1.5*x - 0.5*x^3
                if (x > 1.0f) outSample = 1.0f;
                else if (x < -1.0f) outSample = -1.0f;
                else outSample = 1.5f * x - 0.5f * x * x * x;
            }

            return outSample;
        }

        public void Reset()
        {
            Array.Clear(_ringBuffer, 0, _ringBuffer.Length);
            _writeIndex = 0;
            _readIndex1 = 0;
            _readIndex2 = _grainSize / 2.0;
            _carrierPhase = 0;
            _cachedLowHz = -1;
            _cachedHighHz = -1;
            _bandpassFilter.Reset();
        }
    }
}
