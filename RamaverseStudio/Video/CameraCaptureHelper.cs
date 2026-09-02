using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlashCap;

namespace RamaverseStudio.Video
{
    public class CameraDeviceInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "Camera";
        public string DeviceType { get; set; } = "DirectShow";
        public List<string> Formats { get; set; } = new List<string>();
        public object? Descriptor { get; set; }

        public override string ToString() => Name;
    }

    public class CameraCaptureHelper : IDisposable
    {
        private CaptureDevice? _captureDevice;
        private readonly object _frameLock = new object();
        private Bitmap? _latestFrame;
        private string? _runningCameraId;

        // Frame-gating: the compositor polls at canvas FPS, so decoding every
        // incoming camera frame both wastes CPU and (critically) churns GDI
        // bitmaps faster than they can be reused. We coalesce to one decode
        // per compositor request: the newest pixels only.
        private byte[]? _pendingFrameBytes;
        private int _pendingLength;

        public bool IsRunning
        {
            get
            {
                lock (_frameLock)
                {
                    return _captureDevice != null && _runningCameraId != null;
                }
            }
        }

        public string? ActiveCameraId => _runningCameraId;

        public event Action<Bitmap>? FrameArrived;

        public static List<CameraDeviceInfo> GetAvailableCameras()
        {
            var list = new List<CameraDeviceInfo>();

            try
            {
                var devices = new CaptureDevices();
                var descriptors = devices.EnumerateDescriptors().ToList();

                for (int i = 0; i < descriptors.Count; i++)
                {
                    var d = descriptors[i];
                    var info = new CameraDeviceInfo
                    {
                        Id = d.Identity?.ToString() ?? $"Camera_{i}",
                        Name = d.Name,
                        DeviceType = d.DeviceType.ToString(),
                        Descriptor = d
                    };

                    foreach (var c in d.Characteristics)
                    {
                        info.Formats.Add($"{c.PixelFormat} {c.Width}x{c.Height} @ {c.FramesPerSecond}fps");
                    }

                    list.Add(info);
                }
            }
            catch (Exception) { }

            return list;
        }

        public async Task<bool> StartCameraAsync(CameraDeviceInfo? cameraInfo, int targetWidth = 1280, int targetHeight = 720, int targetFps = 30)
        {
            await StopCameraAsync();

            if (cameraInfo?.Descriptor is not CaptureDeviceDescriptor descriptor)
            {
                var cameras = GetAvailableCameras();
                if (cameras.Count == 0) return false;
                descriptor = (CaptureDeviceDescriptor)cameras[0].Descriptor!;
            }

            try
            {
                var charact = descriptor.Characteristics
                    .OrderByDescending(c => c.Width == targetWidth && c.Height == targetHeight)
                    .ThenByDescending(c => c.Width * c.Height)
                    .FirstOrDefault();

                if (charact == null && descriptor.Characteristics.Length > 0)
                {
                    charact = descriptor.Characteristics[0];
                }

                if (charact == null) return false;

                _captureDevice = await descriptor.OpenAsync(charact, OnPixelBufferArrivedAsync);
                await _captureDevice.StartAsync();

                lock (_frameLock)
                {
                    _runningCameraId = cameraInfo?.Id ?? descriptor.Identity?.ToString();
                }
                return true;
            }
            catch (Exception)
            {
                lock (_frameLock) { _runningCameraId = null; }
                return false;
            }
        }

        /// <summary>
        /// Starts the camera whose Identity matches the given source's CameraDeviceId.
        /// This is the path used by the compositor when a saved scene references a webcam.
        /// </summary>
        public async Task<bool> StartCameraByIdAsync(string cameraId, int targetWidth = 1280, int targetHeight = 720, int targetFps = 30)
        {
            if (string.IsNullOrWhiteSpace(cameraId)) return false;

            var cameras = GetAvailableCameras();
            var match = cameras.FirstOrDefault(c => c.Id == cameraId)
                        ?? cameras.FirstOrDefault(c => cameraId.Contains(c.Id, StringComparison.Ordinal))
                        ?? cameras.FirstOrDefault(c => c.Id.Contains(cameraId, StringComparison.Ordinal));

            if (match == null)
            {
                return false;
            }

            return await StartCameraAsync(match, targetWidth, targetHeight, targetFps);
        }

        private Task OnPixelBufferArrivedAsync(PixelBufferScope bufferScope)
        {
            try
            {
                // Stage raw bytes only — the decode happens on the consumer's
                // cadence (GetLatestFrame), so a 60fps camera feeding a 30fps
                // canvas no longer allocates 2x the GDI bitmaps it needs.
                byte[] imageBytes = bufferScope.Buffer.ExtractImage();
                lock (_frameLock)
                {
                    if (_pendingFrameBytes == null || _pendingFrameBytes.Length < imageBytes.Length)
                    {
                        _pendingFrameBytes = new byte[imageBytes.Length];
                    }
                    Buffer.BlockCopy(imageBytes, 0, _pendingFrameBytes, 0, imageBytes.Length);
                    _pendingLength = imageBytes.Length;
                }
            }
            catch { }

            return Task.CompletedTask;
        }

        public Bitmap? GetLatestFrame()
        {
            // Only decode when NEW bytes arrived since the last call; otherwise
            // the previous decoded frame is still the newest picture we have.
            // This caps decode work at the camera's real frame rate regardless
            // of how often the compositor polls.
            bool hasNew;
            lock (_frameLock)
            {
                hasNew = _pendingLength > 0;
            }

            if (hasNew)
            {
                Bitmap? decoded = null;
                try
                {
                    lock (_frameLock)
                    {
                        using var ms = new MemoryStream(_pendingFrameBytes!, 0, _pendingLength);
                        _pendingLength = 0; // consume
                        using var raw = Image.FromStream(ms);
                        decoded = new Bitmap(raw);
                    }
                }
                catch
                {
                    decoded?.Dispose();
                    decoded = null;
                }

                if (decoded != null)
                {
                    lock (_frameLock)
                    {
                        _latestFrame?.Dispose();
                        _latestFrame = decoded;
                    }
                }
            }

            lock (_frameLock)
            {
                return _latestFrame != null ? (Bitmap)_latestFrame.Clone() : null;
            }
        }

        public async Task StopCameraAsync()
        {
            var device = _captureDevice;
            _captureDevice = null;

            if (device != null)
            {
                try
                {
                    await device.StopAsync();
                    await device.DisposeAsync();
                }
                catch { }
            }

            lock (_frameLock)
            {
                _runningCameraId = null;
                _latestFrame?.Dispose();
                _latestFrame = null;
            }
        }

        public void Dispose()
        {
            try
            {
                _ = StopCameraAsync();
            }
            catch { }
        }
    }
}
