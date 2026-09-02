using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using RamaverseStudio.Models;

namespace RamaverseStudio.Video
{
    /// <summary>
    /// Offscreen Chromium-based Browser Source renderer using Microsoft WebView2.
    /// Supports transparent backgrounds for animated Streamlabs/StreamElements alerts,
    /// chat overlays, live timer widgets, and HTML5 web apps.
    /// </summary>
    public class BrowserSourceRenderer : IDisposable
    {
        private readonly ConcurrentDictionary<string, BrowserInstance> _instances = new();
        private readonly Dispatcher _dispatcher;
        private bool _isDisposed = false;

        public BrowserSourceRenderer(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        private class BrowserInstance : IDisposable
        {
            public string Id { get; set; } = "";
            public string CurrentUrl { get; set; } = "";
            public int Width { get; set; } = 1920;
            public int Height { get; set; } = 1080;
            public WebView2? WebView { get; set; }
            public Bitmap? LatestFrame { get; set; }
            public readonly object FrameLock = new object();
            public bool IsInitialized { get; set; }
            public bool IsRendering { get; set; }
            public DateTime LastRenderTime { get; set; } = DateTime.MinValue;

            public void Dispose()
            {
                lock (FrameLock)
                {
                    LatestFrame?.Dispose();
                    LatestFrame = null;
                }

                if (WebView != null)
                {
                    try
                    {
                        WebView.Dispose();
                    }
                    catch { }
                    WebView = null;
                }
            }
        }

        /// <summary>
        /// Retrieves the latest rendered transparent bitmap for a browser source.
        /// Thread-safe for the CompositorEngine render thread.
        /// </summary>
        public Bitmap? GetFrame(SourceItem source)
        {
            if (_isDisposed || source == null || string.IsNullOrWhiteSpace(source.BrowserUrl))
                return null;

            string id = source.Id;
            if (!_instances.TryGetValue(id, out var instance))
            {
                // Initialize new instance on UI dispatcher
                _dispatcher.BeginInvoke(() => EnsureInstanceCreated(source));
                return GeneratePlaceholder(source.BrowserUrl, source.Width, source.Height);
            }

            // Check if URL or dimensions changed
            if (instance.CurrentUrl != source.BrowserUrl ||
                instance.Width != (int)source.Width ||
                instance.Height != (int)source.Height)
            {
                _dispatcher.BeginInvoke(() => UpdateInstance(instance, source));
            }

            // Request next frame capture on UI dispatcher periodically (~30-60 FPS)
            if (instance.IsInitialized && !instance.IsRendering && (DateTime.UtcNow - instance.LastRenderTime).TotalMilliseconds >= 33)
            {
                instance.IsRendering = true;
                instance.LastRenderTime = DateTime.UtcNow;
                _dispatcher.BeginInvoke(() => CaptureFrameAsync(instance));
            }

            lock (instance.FrameLock)
            {
                if (instance.LatestFrame != null)
                {
                    // Return a clone for compositor thread safety
                    return (Bitmap)instance.LatestFrame.Clone();
                }
            }

            return GeneratePlaceholder(source.BrowserUrl, source.Width, source.Height);
        }

        private async void EnsureInstanceCreated(SourceItem source)
        {
            if (_isDisposed) return;

            string id = source.Id;
            if (_instances.ContainsKey(id)) return;

            var instance = new BrowserInstance
            {
                Id = id,
                CurrentUrl = source.BrowserUrl,
                Width = Math.Max(64, (int)source.Width),
                Height = Math.Max(64, (int)source.Height)
            };

            _instances[id] = instance;

            try
            {
                var webView = new WebView2
                {
                    Width = instance.Width,
                    Height = instance.Height,
                    DefaultBackgroundColor = System.Drawing.Color.Transparent
                };

                // Initialize WebView2 environment with persistent user data folder
                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "RamaverseStudio", "WebView2Data");
                Directory.CreateDirectory(userDataFolder);

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await webView.EnsureCoreWebView2Async(env);

                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                // Handle navigation
                if (Uri.TryCreate(source.BrowserUrl, UriKind.Absolute, out var uri))
                {
                    webView.Source = uri;
                }

                webView.NavigationCompleted += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(source.CustomCss) && webView.CoreWebView2 != null)
                    {
                        string js = $"const style = document.createElement('style'); style.innerHTML = `{source.CustomCss.Replace("`", "\\`")}`; document.head.appendChild(style);";
                        _ = webView.CoreWebView2.ExecuteScriptAsync(js);
                    }
                };

                instance.WebView = webView;
                instance.IsInitialized = true;
            }
            catch
            {
                instance.IsInitialized = false;
            }
        }

        private void UpdateInstance(BrowserInstance instance, SourceItem source)
        {
            instance.Width = Math.Max(64, (int)source.Width);
            instance.Height = Math.Max(64, (int)source.Height);

            if (instance.WebView != null)
            {
                instance.WebView.Width = instance.Width;
                instance.WebView.Height = instance.Height;

                if (instance.CurrentUrl != source.BrowserUrl && Uri.TryCreate(source.BrowserUrl, UriKind.Absolute, out var uri))
                {
                    instance.CurrentUrl = source.BrowserUrl;
                    instance.WebView.Source = uri;
                }
            }
        }

        private async void CaptureFrameAsync(BrowserInstance instance)
        {
            try
            {
                if (instance.WebView?.CoreWebView2 == null)
                {
                    instance.IsRendering = false;
                    return;
                }

                using var stream = new MemoryStream();
                await instance.WebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
                stream.Position = 0;

                using var rawBmp = new Bitmap(stream);
                var bmp32 = new Bitmap(rawBmp.Width, rawBmp.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp32))
                {
                    g.DrawImage(rawBmp, 0, 0, rawBmp.Width, rawBmp.Height);
                }

                lock (instance.FrameLock)
                {
                    instance.LatestFrame?.Dispose();
                    instance.LatestFrame = bmp32;
                }
            }
            catch
            {
            }
            finally
            {
                instance.IsRendering = false;
            }
        }

        private static Bitmap GeneratePlaceholder(string url, double width, double height)
        {
            int w = Math.Max(120, (int)width);
            int h = Math.Max(80, (int)height);
            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(System.Drawing.Color.FromArgb(40, 20, 20, 20));

            using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(80, 255, 255, 255), 1);
            g.DrawRectangle(pen, 1, 1, w - 2, h - 2);

            using var font = new Font(System.Drawing.FontFamily.GenericSansSerif, 10, System.Drawing.FontStyle.Regular);
            using var brush = new SolidBrush(System.Drawing.Color.FromArgb(180, 255, 255, 255));
            string label = $"🌐 Browser: {url}";
            if (label.Length > 40) label = label.Substring(0, 37) + "...";
            g.DrawString(label, font, brush, new PointF(10, 10));

            return bmp;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            foreach (var kvp in _instances)
            {
                kvp.Value.Dispose();
            }
            _instances.Clear();
        }
    }
}
