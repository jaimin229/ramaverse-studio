using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
                // Find best matching characteristic
                var charact = descriptor.Characteristics
                    .OrderByDescending(c => c.Width == targetWidth && c.Height == targetHeight)
                    .ThenByDescending(c => c.Width * c.Height)
                    .FirstOrDefault();

                if (charact == null && descriptor.Characteristics.Length > 0)
                {
                    charact = descriptor.Characteristics[0];
                }

                if (charact == null) return false;

                _captureDevice = await descriptor.OpenAsync(
                    charact,
                    OnPixelBufferArrivedAsync);

                await _captureDevice.StartAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private Task OnPixelBufferArrivedAsync(PixelBufferScope bufferScope)
        {
            try
            {
                // Extract image into Bitmap
                byte[] imageBytes = bufferScope.Buffer.ExtractImage();
                using var ms = new MemoryStream(imageBytes);
                var bmp = new Bitmap(ms);

                lock (_frameLock)
                {
                    _latestFrame?.Dispose();
                    _latestFrame = (Bitmap)bmp.Clone();
                }

                FrameArrived?.Invoke(bmp);
            }
            catch { }

            return Task.CompletedTask;
        }

        public Bitmap? GetLatestFrame()
        {
            lock (_frameLock)
            {
                return _latestFrame != null ? (Bitmap)_latestFrame.Clone() : null;
            }
        }

        public async Task StopCameraAsync()
        {
            if (_captureDevice != null)
            {
                try
                {
                    await _captureDevice.StopAsync();
                    await _captureDevice.DisposeAsync();
                }
                catch { }
                _captureDevice = null;
            }

            lock (_frameLock)
            {
                _latestFrame?.Dispose();
                _latestFrame = null;
            }
        }

        public void Dispose()
        {
            _ = StopCameraAsync();
        }
    }
}
