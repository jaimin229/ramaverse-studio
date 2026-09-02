using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using RamaverseStudio.Video;

namespace RamaverseStudio.Output
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct VirtualCamHeader
    {
        public uint Magic;              // 0x52414D41 ("RAMA")
        public uint Version;            // 1
        public int Width;               // 1920
        public int Height;              // 1080
        public int Stride;              // 7680
        public uint PixelFormat;        // 0 = BGRA32, 1 = NV12, 2 = YUY2
        public uint FpsNumerator;       // 60
        public uint FpsDenominator;     // 1
        public uint BufferCount;        // 3 (Triple buffered)
        public uint BufferSize;         // Width * Height * 4
        public ulong SequenceNumber;    // Monotonic frame counter
        public uint LatestBufferIndex;  // 0, 1, or 2 (Atomic)
        public long TimestampQpc;       // QueryPerformanceCounter
        public long Timestamp100ns;     // 100ns Reference Time for DirectShow
    }

    /// <summary>
    /// High-performance, lock-free, triple-buffered Shared Memory IPC engine for Ramaverse Virtual Camera.
    /// Broadcasts frames to DirectShow and Windows Media Foundation virtual cameras with sub-0.1ms memory copying.
    /// </summary>
    public sealed class VirtualCameraIpcEngine : IDisposable
    {
        private const string MmfName = @"Local\RamaverseStudio_VirtualCam_Video";
        private const string EventName = @"Local\RamaverseStudio_VirtualCam_Event";
        private const uint MagicRama = 0x52414D41;
        private const int HeaderSize = 64;
        private const int SlotCount = 3;

        private MemoryMappedFile? _mmf;
        private MemoryMappedViewAccessor? _accessor;
        private unsafe byte* _basePtr = null;
        private EventWaitHandle? _frameEvent;

        private int _width;
        private int _height;
        private int _fps;
        private int _frameBufferSize;
        private ulong _sequenceNumber;
        private int _currentWriteSlot;
        private readonly object _stateLock = new();

        public bool IsActive { get; private set; }

        public void Start(int width = 1920, int height = 1080, int fps = 60)
        {
            lock (_stateLock)
            {
                Stop();

                _width = width;
                _height = height;
                _fps = fps;
                _frameBufferSize = width * height * 4;
                long totalSize = HeaderSize + ((long)_frameBufferSize * SlotCount);

                try
                {
                    _mmf = MemoryMappedFile.CreateOrOpen(
                        MmfName,
                        totalSize,
                        MemoryMappedFileAccess.ReadWrite,
                        MemoryMappedFileOptions.None,
                        System.IO.HandleInheritability.Inheritable);

                    _accessor = _mmf.CreateViewAccessor(0, totalSize, MemoryMappedFileAccess.ReadWrite);
                    
                    unsafe
                    {
                        byte* ptr = null;
                        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                        _basePtr = ptr;

                        VirtualCamHeader* header = (VirtualCamHeader*)_basePtr;
                        header->Magic = MagicRama;
                        header->Version = 1;
                        header->Width = _width;
                        header->Height = _height;
                        header->Stride = _width * 4;
                        header->PixelFormat = 0; // BGRA32
                        header->FpsNumerator = (uint)_fps;
                        header->FpsDenominator = 1;
                        header->BufferCount = SlotCount;
                        header->BufferSize = (uint)_frameBufferSize;
                        header->SequenceNumber = 0;
                        header->LatestBufferIndex = 0;
                        header->TimestampQpc = 0;
                        header->Timestamp100ns = 0;
                    }

                    _frameEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
                    _sequenceNumber = 0;
                    _currentWriteSlot = 0;
                    IsActive = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[VirtualCam] Start failed: {ex.Message}");
                    Stop();
                }
            }
        }

        public unsafe void PushFrame(SharedFrame frame)
        {
            if (!IsActive || _basePtr == null || frame.Pixels == null) return;

            int nextSlot = (_currentWriteSlot + 1) % SlotCount;
            long slotOffset = HeaderSize + ((long)nextSlot * _frameBufferSize);
            byte* destSlotPtr = _basePtr + slotOffset;

            int copyLength = Math.Min(frame.Pixels.Length, _frameBufferSize);

            fixed (byte* srcPtr = frame.Pixels)
            {
                Buffer.MemoryCopy(srcPtr, destSlotPtr, _frameBufferSize, copyLength);
            }

            _sequenceNumber++;
            long qpcNow = System.Diagnostics.Stopwatch.GetTimestamp();
            long refTime100ns = DateTimeOffset.UtcNow.Ticks;

            VirtualCamHeader* header = (VirtualCamHeader*)_basePtr;
            header->SequenceNumber = _sequenceNumber;
            header->TimestampQpc = qpcNow;
            header->Timestamp100ns = refTime100ns;

            Thread.MemoryBarrier();
            Interlocked.Exchange(ref header->LatestBufferIndex, (uint)nextSlot);

            _currentWriteSlot = nextSlot;
            _frameEvent?.Set();
        }

        public unsafe void Stop()
        {
            lock (_stateLock)
            {
                IsActive = false;

                if (_basePtr != null && _accessor != null)
                {
                    _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                    _basePtr = null;
                }

                try { _accessor?.Dispose(); } catch { }
                _accessor = null;

                try { _mmf?.Dispose(); } catch { }
                _mmf = null;

                try { _frameEvent?.Dispose(); } catch { }
                _frameEvent = null;
            }
        }

        public void Dispose() => Stop();
    }
}
