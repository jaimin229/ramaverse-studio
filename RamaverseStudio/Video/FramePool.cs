using System;
using System.Collections.Concurrent;
using System.Threading;

namespace RamaverseStudio.Video
{
    /// <summary>
    /// A reference-counted BGRA video frame rented from <see cref="VideoFramePool"/>.
    /// Rent starts with one reference owned by the producer.
    /// Every consumer must call <see cref="AddRef"/> exactly once when it accepts the
    /// frame and <see cref="Release"/> exactly once when it is finished with it.
    /// </summary>
    public sealed class SharedFrame
    {
        internal VideoFramePool? Owner;
        internal byte[] Pixels = Array.Empty<byte>();

        /// <summary>Direct access to the BGRA pixel payload (stride = Width * 4).</summary>
        public byte[] Buffer => Pixels;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Stride { get; private set; }

        private int _refs;

        internal void Configure(int width, int height)
        {
            int size = width * height * 4;
            if (Pixels.Length < size)
            {
                Pixels = new byte[size];
            }
            Width = width;
            Height = height;
            Stride = width * 4;
        }

        internal void ResetRefs(int refs) => _refs = refs;

        public SharedFrame AddRef()
        {
            Interlocked.Increment(ref _refs);
            return this;
        }

        public void Release()
        {
            if (Interlocked.Decrement(ref _refs) == 0 && Owner != null)
            {
                Owner.Recycle(this);
            }
        }
    }

    /// <summary>
    /// Thread-safe frame pool that eliminates per-frame GC allocations in the
    /// 60 FPS render → record → stream pipeline. Growth is naturally bounded
    /// by the consumer queue depths.
    /// </summary>
    public sealed class VideoFramePool
    {
        private readonly ConcurrentStack<SharedFrame> _spare = new();

        public SharedFrame Rent(int width, int height, int initialRefs)
        {
            if (!_spare.TryPop(out var frame))
            {
                frame = new SharedFrame { Owner = this };
            }
            frame.Configure(width, height);
            frame.ResetRefs(initialRefs);
            return frame;
        }

        internal void Recycle(SharedFrame frame) => _spare.Push(frame);

        public int SpareCount => _spare.Count;
    }
}
