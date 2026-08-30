using System;

namespace RamaverseStudio.Audio
{
    /// <summary>
    /// Real-time Digital Biquad Equalizer filter (Audio EQ Cookbook).
    /// </summary>
    public class BiQuadFilter
    {
        public enum FilterType
        {
            LowShelf,
            HighShelf,
            Peaking,
            BandPass,
            LowPass,
            HighPass
        }

        private float _a0, _a1, _a2, _b0, _b1, _b2;
        private float _x1, _x2, _y1, _y2;
        private int _sampleRate;

        public BiQuadFilter(int sampleRate)
        {
            _sampleRate = sampleRate;
        }

        public void SetLowShelf(float centerFreq, float gainDb, float q = 0.707f)
        {
            float A = (float)Math.Pow(10, gainDb / 40.0);
            float w0 = (float)(2.0 * Math.PI * centerFreq / _sampleRate);
            float cosw0 = (float)Math.Cos(w0);
            float sinw0 = (float)Math.Sin(w0);
            float alpha = sinw0 / (2.0f * q);
            float beta = 2.0f * (float)Math.Sqrt(A) * alpha;

            float b0 = A * ((A + 1) - (A - 1) * cosw0 + beta);
            float b1 = 2 * A * ((A - 1) - (A + 1) * cosw0);
            float b2 = A * ((A + 1) - (A - 1) * cosw0 - beta);
            float a0 = (A + 1) + (A - 1) * cosw0 + beta;
            float a1 = -2 * ((A - 1) + (A + 1) * cosw0);
            float a2 = (A + 1) + (A - 1) * cosw0 - beta;

            SetCoefficients(a0, a1, a2, b0, b1, b2);
        }

        public void SetHighShelf(float centerFreq, float gainDb, float q = 0.707f)
        {
            float A = (float)Math.Pow(10, gainDb / 40.0);
            float w0 = (float)(2.0 * Math.PI * centerFreq / _sampleRate);
            float cosw0 = (float)Math.Cos(w0);
            float sinw0 = (float)Math.Sin(w0);
            float alpha = sinw0 / (2.0f * q);
            float beta = 2.0f * (float)Math.Sqrt(A) * alpha;

            float b0 = A * ((A + 1) + (A - 1) * cosw0 + beta);
            float b1 = -2 * A * ((A - 1) + (A + 1) * cosw0);
            float b2 = A * ((A + 1) + (A - 1) * cosw0 - beta);
            float a0 = (A + 1) - (A - 1) * cosw0 + beta;
            float a1 = 2 * ((A - 1) - (A + 1) * cosw0);
            float a2 = (A + 1) - (A - 1) * cosw0 - beta;

            SetCoefficients(a0, a1, a2, b0, b1, b2);
        }

        public void SetPeaking(float centerFreq, float gainDb, float q = 1.0f)
        {
            float A = (float)Math.Pow(10, gainDb / 40.0);
            float w0 = (float)(2.0 * Math.PI * centerFreq / _sampleRate);
            float cosw0 = (float)Math.Cos(w0);
            float sinw0 = (float)Math.Sin(w0);
            float alpha = sinw0 / (2.0f * q);

            float b0 = 1 + alpha * A;
            float b1 = -2 * cosw0;
            float b2 = 1 - alpha * A;
            float a0 = 1 + alpha / A;
            float a1 = -2 * cosw0;
            float a2 = 1 - alpha / A;

            SetCoefficients(a0, a1, a2, b0, b1, b2);
        }

        public void SetBandPass(float centerFreq, float q = 1.4f)
        {
            float w0 = (float)(2.0 * Math.PI * centerFreq / _sampleRate);
            float cosw0 = (float)Math.Cos(w0);
            float sinw0 = (float)Math.Sin(w0);
            float alpha = sinw0 / (2.0f * q);

            float b0 = alpha;
            float b1 = 0;
            float b2 = -alpha;
            float a0 = 1 + alpha;
            float a1 = -2 * cosw0;
            float a2 = 1 - alpha;

            SetCoefficients(a0, a1, a2, b0, b1, b2);
        }

        private void SetCoefficients(float a0, float a1, float a2, float b0, float b1, float b2)
        {
            _a0 = a0;
            _a1 = a1 / a0;
            _a2 = a2 / a0;
            _b0 = b0 / a0;
            _b1 = b1 / a0;
            _b2 = b2 / a0;
        }

        public float Process(float sample)
        {
            float result = _b0 * sample + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            if (Math.Abs(result) < 1e-25f) result = 0.0f;
            _x2 = _x1;
            _x1 = sample;
            _y2 = _y1;
            _y1 = result;
            return result;
        }

        public void Reset()
        {
            _x1 = _x2 = _y1 = _y2 = 0;
        }
    }

    /// <summary>
    /// Real-time Noise Gate with hysteresis and smooth envelope following.
    /// </summary>
    public class NoiseGate
    {
        private enum State { Closed, Attacking, Open, Holding, Releasing }

        private State _state = State.Closed;
        private float _currentGain = 0.0f;
        private float _holdTimerSec = 0.0f;
        private int _sampleRate;

        public NoiseGate(int sampleRate)
        {
            _sampleRate = sampleRate;
        }

        public float Process(float sample, double thresholdDb, double attackMs, double holdMs, double releaseMs)
        {
            float inputLevel = Math.Abs(sample);
            float thresholdLinear = (float)Math.Pow(10.0, thresholdDb / 20.0);
            float attackStep = (float)(1.0 / (Math.Max(1.0, attackMs) * 0.001 * _sampleRate));
            float releaseStep = (float)(1.0 / (Math.Max(1.0, releaseMs) * 0.001 * _sampleRate));
            float holdDurationSec = (float)(holdMs * 0.001);
            float dt = 1.0f / _sampleRate;

            if (inputLevel >= thresholdLinear)
            {
                _state = State.Attacking;
                _holdTimerSec = holdDurationSec;
            }

            switch (_state)
            {
                case State.Attacking:
                    _currentGain += attackStep;
                    if (_currentGain >= 1.0f)
                    {
                        _currentGain = 1.0f;
                        _state = State.Open;
                    }
                    break;

                case State.Open:
                    _currentGain = 1.0f;
                    if (inputLevel < thresholdLinear)
                    {
                        _state = State.Holding;
                    }
                    break;

                case State.Holding:
                    _currentGain = 1.0f;
                    _holdTimerSec -= dt;
                    if (_holdTimerSec <= 0.0f)
                    {
                        _state = State.Releasing;
                    }
                    break;

                case State.Releasing:
                    _currentGain -= releaseStep;
                    if (_currentGain <= 0.0f)
                    {
                        _currentGain = 0.0f;
                        _state = State.Closed;
                    }
                    break;

                case State.Closed:
                    _currentGain = 0.0f;
                    break;
            }

            return sample * _currentGain;
        }
    }

    /// <summary>
    /// Real-time Dynamic Range Audio Compressor with soft knee and makeup gain.
    /// </summary>
    public class DynamicCompressor
    {
        private float _envelopeDb = -96.0f;
        private int _sampleRate;

        public DynamicCompressor(int sampleRate)
        {
            _sampleRate = sampleRate;
        }

        public float Process(float sample, double thresholdDb, double ratio, double attackMs, double releaseMs, double makeupGainDb)
        {
            float absSample = Math.Abs(sample);
            float sampleDb = absSample > 1e-6f ? (float)(20.0 * Math.Log10(absSample)) : -96.0f;

            float attackCoeff = (float)Math.Exp(-1.0 / (Math.Max(1.0, attackMs) * 0.001 * _sampleRate));
            float releaseCoeff = (float)Math.Exp(-1.0 / (Math.Max(1.0, releaseMs) * 0.001 * _sampleRate));

            if (sampleDb > _envelopeDb)
                _envelopeDb = attackCoeff * _envelopeDb + (1.0f - attackCoeff) * sampleDb;
            else
                _envelopeDb = releaseCoeff * _envelopeDb + (1.0f - releaseCoeff) * sampleDb;

            float gainDb = 0.0f;
            if (_envelopeDb > thresholdDb)
            {
                float overshootDb = _envelopeDb - (float)thresholdDb;
                gainDb = -overshootDb * (1.0f - 1.0f / (float)Math.Max(1.0, ratio));
            }

            float totalGainLinear = (float)Math.Pow(10.0, (gainDb + makeupGainDb) / 20.0);
            return sample * totalGainLinear;
        }
    }

    /// <summary>
    /// Fast Brickwall Peak Limiter.
    /// </summary>
    public class AudioLimiter
    {
        private float _envelope = 0.0f;
        private int _sampleRate;

        public AudioLimiter(int sampleRate)
        {
            _sampleRate = sampleRate;
        }

        public float Process(float sample, double thresholdDb, double releaseMs)
        {
            float maxLinear = (float)Math.Pow(10.0, thresholdDb / 20.0);
            float absSample = Math.Abs(sample);
            float releaseCoeff = (float)Math.Exp(-1.0 / (Math.Max(5.0, releaseMs) * 0.001 * _sampleRate));

            if (absSample > _envelope)
                _envelope = absSample;
            else
                _envelope = releaseCoeff * _envelope + (1.0f - releaseCoeff) * absSample;

            if (_envelope > maxLinear)
            {
                float reduction = maxLinear / _envelope;
                return sample * reduction;
            }

            return sample;
        }
    }
}
