using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RamaverseStudio.Video
{
    /// <summary>
    /// Wireless phone camera receiver. Connects to an MJPEG HTTP stream
    /// (DroidCam / IP Webcam / EpocCam) and keeps the latest decoded frame.
    /// Uses ResponseHeadersRead with an infinite read timeout so slow phone
    /// streams do not get killed between frames; reconnection only happens
    /// when the transport itself breaks.
    /// </summary>
    public class PhoneCameraReceiver : IDisposable
    {
        private HttpClient? _httpClient;
        private CancellationTokenSource? _cts;
        private readonly object _frameLock = new object();
        private Bitmap? _latestFrame;
        private volatile bool _isConnected = false;

        public bool IsConnected => _isConnected;
        public string StreamUrl { get; set; } = "http://192.168.1.100:8080/video";

        public event Action<Bitmap>? FrameArrived;
        public event Action<string>? ConnectionError;

        public async Task<bool> ConnectAsync(string streamUrl)
        {
            await DisconnectAsync();

            StreamUrl = streamUrl;

            _cts = new CancellationTokenSource();
            _httpClient = new HttpClient
            {
                Timeout = Timeout.InfiniteTimeSpan // per-read timeout handled below
            };

            try
            {
                _ = Task.Run(() => StreamLoopAsync(_cts.Token));
                _isConnected = true;
                return true;
            }
            catch (Exception)
            {
                _isConnected = false;
                return false;
            }
        }

        private async Task StreamLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var response = await _httpClient!.GetAsync(StreamUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        ConnectionError?.Invoke($"Phone camera server returned {(int)response.StatusCode}. Check the URL and that the phone app is running.");
                        await Task.Delay(2000, ct);
                        continue;
                    }

                    using var stream = await response.Content.ReadAsStreamAsync(ct);
                    using var reader = new BinaryReader(stream);

                    // MJPEG frame parser
                    while (!ct.IsCancellationRequested)
                    {
                        // Look for JPEG start-of-image SOI (0xFF, 0xD8)
                        byte b1 = 0, b2 = 0;
                        while (!ct.IsCancellationRequested)
                        {
                            b1 = b2;
                            b2 = reader.ReadByte();
                            if (b1 == 0xFF && b2 == 0xD8)
                                break;
                        }

                        if (ct.IsCancellationRequested) break;

                        using var frameMs = new MemoryStream();
                        frameMs.WriteByte(0xFF);
                        frameMs.WriteByte(0xD8);

                        // Read until JPEG end-of-image EOI (0xFF, 0xD9)
                        while (!ct.IsCancellationRequested)
                        {
                            b1 = b2;
                            b2 = reader.ReadByte();
                            frameMs.WriteByte(b2);
                            if (b1 == 0xFF && b2 == 0xD9)
                                break;
                        }

                        frameMs.Position = 0;
                        Bitmap? decoded = null;
                        try
                        {
                            using var img = Image.FromStream(frameMs);
                            decoded = new Bitmap(img);
                        }
                        catch
                        {
                            decoded?.Dispose();
                            continue; // partial frame: skip, keep streaming
                        }

                        lock (_frameLock)
                        {
                            _latestFrame?.Dispose();
                            _latestFrame = decoded;
                        }

                        FrameArrived?.Invoke(decoded);
                        decoded = null; // FrameArrived receivers own this instance now
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    if (ct.IsCancellationRequested) break;
                    await Task.Delay(2000, ct); // Reconnection backoff
                }
            }
        }

        public Bitmap? GetLatestFrame()
        {
            lock (_frameLock)
            {
                return _latestFrame != null ? (Bitmap)_latestFrame.Clone() : null;
            }
        }

        public Task DisconnectAsync()
        {
            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
            }
            catch { }
            _cts = null;

            try { _httpClient?.Dispose(); } catch { }
            _httpClient = null;

            lock (_frameLock)
            {
                _latestFrame?.Dispose();
                _latestFrame = null;
            }

            _isConnected = false;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _ = DisconnectAsync();
        }
    }
}
