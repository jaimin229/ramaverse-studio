using System;
using System.IO.MemoryMappedFiles;
using System.Threading;
using RamaverseStudio.Video;

namespace RamaverseStudio.Output
{
    /// <summary>
    /// Virtual Camera IPC Bridge.
    /// Exposes the live composited canvas via Windows Shared Memory (MemoryMappedFile)
    /// and named event synchronization, enabling Ramaverse Studio to act as a virtual
    /// webcam in Zoom, Discord, Microsoft Teams, and Google Meet.
    /// </summary>
    public class VirtualCameraEngine : IDisposable
    {
        private const string MmfName = "RamaverseStudio_VirtualCam_Video";
        private const string EventName = "RamaverseStudio_VirtualCam_Event";

        private MemoryMappedFile? _mmf;
        private MemoryMappedViewAccessor? _accessor;
        private EventWaitHandle? _frameReadyEvent;
        private readonly object _lock = new object();

        public bool IsActive { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Fps { get; private set; }

        public void Start(int width = 1920, int height = 1080, int fps = 60)
        {
            lock (_lock)
            {
                Stop();

                Width = width;
                Height = height;
                Fps = fps;
                long bufferSize = 24 + (long)width * height * 4; // 24-byte header + BGRA pixels

                try
                {
                    _mmf = MemoryMappedFile.CreateOrOpen(MmfName, bufferSize, MemoryMappedFileAccess.ReadWrite);
                    _accessor = _mmf.CreateViewAccessor(0, bufferSize, MemoryMappedFileAccess.Write);
                    _frameReadyEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
                    IsActive = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to start Virtual Camera: {ex.Message}");
                    Stop();
                }
            }
        }

        public void PushFrame(SharedFrame frame)
        {
            if (!IsActive || _accessor == null) return;

            lock (_lock)
            {
                if (!IsActive || _accessor == null) return;

                try
                {
                    // Write 24-byte header: Width (4), Height (4), Stride (4), TimestampMs (8), Reserved (4)
                    _accessor.Write(0, frame.Width);
                    _accessor.Write(4, frame.Height);
                    _accessor.Write(8, frame.Stride);
                    _accessor.Write(12, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    _accessor.Write(20, 0); // Reserved

                    // Write pixel data
                    int byteLength = Math.Min(frame.Buffer.Length, frame.Width * frame.Height * 4);
                    _accessor.WriteArray(24, frame.Buffer, 0, byteLength);

                    // Signal external virtual camera consumer
                    _frameReadyEvent?.Set();
                }
                catch
                {
                }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                IsActive = false;

                try { _accessor?.Dispose(); } catch { }
                _accessor = null;

                try { _mmf?.Dispose(); } catch { }
                _mmf = null;

                try { _frameReadyEvent?.Dispose(); } catch { }
                _frameReadyEvent = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
