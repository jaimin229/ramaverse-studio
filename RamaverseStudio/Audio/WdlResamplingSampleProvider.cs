using System;

namespace RamaverseStudio.Audio
{
    /// <summary>
    /// Push-style streaming resampler for the live mic/loopback capture path.
    ///
    /// Linear interpolation between adjacent source samples — for speech and
    /// desktop audio at 44.1k→48k (ratio 1.088) the quality difference vs
    /// polyphase filtering is inaudible, while being exactly ratio-correct,
    /// allocation-free, and free of block-buffering latency.
    ///
    /// ProcessSample feeds one input sample; call TryPullSample in a loop
    /// afterwards to collect every output the resynthesis owes (upsampling
    /// ratios above 2.0 emit several outputs per input).
    /// </summary>
    public sealed class WdlResamplingSampleProvider
    {
        private readonly double _ratio;      // output samples per input sample
        private double _step;               // t-advance per emitted output (= 1/ratio inverted)
        private float _prevSample;
        private bool _hasPrev;
        private double _t;                   // next output position between prev (0) and current (1) inputs

        // Pending outputs when a single input emits more than one (ratio > 2).
        private readonly float[] _pending = new float[8];
        private int _pendingCount;
        private int _pendingRead;

        public WdlResamplingSampleProvider(int sourceRate, int targetRate)
        {
            sourceRate = Math.Max(1, sourceRate);
            targetRate = Math.Max(1, targetRate);
            _ratio = (double)targetRate / sourceRate;
            // Outputs land every (in-rate/out-rate) units along the t interval.
            _step = (double)sourceRate / targetRate;
        }

        /// <summary>
        /// Pushes one input sample. The interpolation position advances by the
        /// ratio; every whole step past the current input produces one output,
        /// which is queued for retrieval via TryPullSample.
        /// </summary>
        public void ProcessSample(float sample)
        {
            if (!_hasPrev)
            {
                // First sample ever: nothing to interpolate from yet.
                _prevSample = sample;
                _hasPrev = true;
                _t = 0.0;
                return;
            }

            // Emit every output position that falls between the previous and
            // this new input sample. Positions advance by 1/ratio per output,
            // so exactly `ratio` outputs are produced per input on average.
            while (_t < 1.0)
            {
                float output = _prevSample + (sample - _prevSample) * (float)_t;
                EnqueueOutput(output);
                _t += _step;
            }

            _t -= 1.0; // carry the remainder into the next input interval
            _prevSample = sample;
        }

        /// <summary>
        /// Retrieves one produced output sample if available.
        /// </summary>
        public bool TryPullSample(out float sample)
        {
            if (_pendingCount > 0)
            {
                sample = _pending[_pendingRead++];
                if (_pendingRead >= _pendingCount)
                {
                    _pendingRead = 0;
                    _pendingCount = 0;
                }
                return true;
            }

            sample = 0;
            return false;
        }

        private void EnqueueOutput(float value)
        {
            // The fixed 8-slot ring is ample: 48k capture is only ever
            // upsampled from >= 8k sources (ratio <= 6), and downstream audio
            // devices realistically run 44.1k or 48k (ratio <= 1.088).
            if (_pendingCount >= _pending.Length && _pendingRead > 0)
            {
                // Compact: shift unread outputs to the front.
                Array.Copy(_pending, _pendingRead, _pending, 0, _pendingCount - _pendingRead);
                _pendingCount -= _pendingRead;
                _pendingRead = 0;
            }

            if (_pendingCount < _pending.Length)
            {
                _pending[_pendingCount++] = value;
            }
            // A full ring with no consumer is an error state we simply drop;
            // the AudioEngine drains every push, so this never occurs.
        }
    }
}
