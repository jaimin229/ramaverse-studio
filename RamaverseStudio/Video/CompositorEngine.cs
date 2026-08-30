using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RamaverseStudio.Models;

namespace RamaverseStudio.Video
{
    public class CompositorEngine : IDisposable
    {
        private int _canvasWidth = 1920;
        private int _canvasHeight = 1080;
        private int _targetFps = 60;

        private Thread? _renderThread;
        private bool _isRunning = false;

        // Active Scene reference
        public Scene? CurrentScene { get; set; }

        // Camera helper cache
        public CameraCaptureHelper CameraHelper { get; } = new CameraCaptureHelper();
        public PhoneCameraReceiver PhoneCamera { get; } = new PhoneCameraReceiver();

        // Image file & Text caches
        private readonly ConcurrentDictionary<string, Bitmap> _imageCache = new ConcurrentDictionary<string, Bitmap>();
        private readonly Dictionary<string, Bitmap> _textCache = new Dictionary<string, Bitmap>();
        private readonly Dictionary<string, Bitmap> _colorCache = new Dictionary<string, Bitmap>();

        // Reusable persistent frame rendering buffers (Eliminates ~480MB/s GC pressure)
        private Bitmap? _canvasBitmap;
        private Graphics? _canvasGraphics;
        private byte[]? _pixelBytesBuffer;

        // Live preview WriteableBitmap (UI accessible)
        public WriteableBitmap PreviewBitmap { get; private set; }
        private readonly Dispatcher _uiDispatcher;

        // Telemetry
        public double ActualFps { get; private set; } = 0;
        public double FrameTimeMs { get; private set; } = 0;
        public long TotalFramesRendered { get; private set; } = 0;
        public long DroppedFrames { get; private set; } = 0;

        // Output Frame Event for Recording & Streaming Engines (Raw BGRA32, width, height, stride)
        public event Action<byte[], int, int, int>? FrameComposited;

        private readonly object _renderLock = new object();

        public int CanvasWidth => _canvasWidth;
        public int CanvasHeight => _canvasHeight;

        public CompositorEngine(Dispatcher uiDispatcher, int width = 1920, int height = 1080, int fps = 60)
        {
            _uiDispatcher = uiDispatcher;
            _canvasWidth = width;
            _canvasHeight = height;
            _targetFps = fps;

            InitReusableCanvas(width, height);
            PreviewBitmap = new WriteableBitmap(_canvasWidth, _canvasHeight, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
        }

        private void InitReusableCanvas(int w, int h)
        {
            _canvasGraphics?.Dispose();
            _canvasBitmap?.Dispose();

            _canvasBitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            _canvasGraphics = Graphics.FromImage(_canvasBitmap);
            _canvasGraphics.CompositingMode = CompositingMode.SourceOver;
            _canvasGraphics.CompositingQuality = CompositingQuality.HighSpeed;
            _canvasGraphics.InterpolationMode = InterpolationMode.Bilinear;
            _canvasGraphics.SmoothingMode = SmoothingMode.HighSpeed;
            _canvasGraphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            _pixelBytesBuffer = new byte[w * h * 4];
        }

        public void SetCanvasDimensions(int width, int height, int fps = 60)
        {
            lock (_renderLock)
            {
                _canvasWidth = Math.Max(320, width);
                _canvasHeight = Math.Max(240, height);
                _targetFps = Math.Clamp(fps, 10, 120);

                InitReusableCanvas(_canvasWidth, _canvasHeight);

                _uiDispatcher.Invoke(() =>
                {
                    PreviewBitmap = new WriteableBitmap(_canvasWidth, _canvasHeight, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
                });
            }
        }

        public void SetCanvasResolution(int width, int height, int fps = 60) => SetCanvasDimensions(width, height, fps);

        public Bitmap CaptureStillFrame()
        {
            lock (_renderLock)
            {
                var bmp = new Bitmap(_canvasWidth, _canvasHeight, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(System.Drawing.Color.Black);
                    if (CurrentScene != null)
                    {
                        var sources = CurrentScene.Sources.OrderBy(s => s.ZIndex).ToList();
                        foreach (var src in sources)
                        {
                            if (!src.IsVisible) continue;
                            RenderSource(g, src, _canvasWidth, _canvasHeight);
                        }
                    }
                }
                return bmp;
            }
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _renderThread = new Thread(RenderLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
                Name = "RamaverseCompositorThread"
            };
            _renderThread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
            try
            {
                _renderThread?.Join(1000);
            }
            catch { }
            _renderThread = null;
        }

        private void RenderLoop()
        {
            var stopwatch = Stopwatch.StartNew();
            int frameCounter = 0;
            long fpsTimer = stopwatch.ElapsedMilliseconds;

            while (_isRunning)
            {
                long startMs = stopwatch.ElapsedMilliseconds;
                double frameIntervalMs = 1000.0 / _targetFps;

                RenderSingleFrame();

                long elapsedMs = stopwatch.ElapsedMilliseconds - startMs;
                FrameTimeMs = elapsedMs;
                TotalFramesRendered++;
                frameCounter++;

                if (stopwatch.ElapsedMilliseconds - fpsTimer >= 1000)
                {
                    ActualFps = frameCounter * 1000.0 / (stopwatch.ElapsedMilliseconds - fpsTimer);
                    frameCounter = 0;
                    fpsTimer = stopwatch.ElapsedMilliseconds;
                }

                double sleepMs = frameIntervalMs - elapsedMs;
                if (sleepMs > 1.0)
                {
                    Thread.Sleep((int)sleepMs);
                }
                else if (sleepMs < -frameIntervalMs)
                {
                    DroppedFrames++;
                }
            }
        }

        private void RenderSingleFrame()
        {
            lock (_renderLock)
            {
                if (_canvasBitmap == null || _canvasGraphics == null || _pixelBytesBuffer == null)
                    return;

                int w = _canvasWidth;
                int h = _canvasHeight;

                // Background fill
                _canvasGraphics.Clear(System.Drawing.Color.FromArgb(15, 17, 23));

                var scene = CurrentScene;
                if (scene != null)
                {
                    var sources = scene.Sources;
                    for (int i = 0; i < sources.Count; i++)
                    {
                        var src = sources[i];
                        if (!src.IsVisible) continue;

                        RenderSource(_canvasGraphics, src, w, h);
                    }
                }

                // Copy to WriteableBitmap and dispatch to output listeners
                BitmapData bmpData = _canvasBitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int byteCount = bmpData.Stride * h;
                    if (_pixelBytesBuffer.Length < byteCount)
                    {
                        _pixelBytesBuffer = new byte[byteCount];
                    }

                    System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, _pixelBytesBuffer, 0, byteCount);

                    // Update UI WriteableBitmap
                    _uiDispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (PreviewBitmap.PixelWidth == w && PreviewBitmap.PixelHeight == h)
                            {
                                PreviewBitmap.WritePixels(new Int32Rect(0, 0, w, h), _pixelBytesBuffer, bmpData.Stride, 0);
                            }
                        }
                        catch { }
                    }), DispatcherPriority.Render);

                    // Notify recording and streaming engines
                    FrameComposited?.Invoke(_pixelBytesBuffer, w, h, bmpData.Stride);
                }
                finally
                {
                    _canvasBitmap.UnlockBits(bmpData);
                }
            }
        }

        private void RenderSource(Graphics g, SourceItem src, int canvasWidth, int canvasHeight)
        {
            Bitmap? layerBmp = null;
            bool shouldDisposeBmp = false;

            try
            {
                switch (src.Type)
                {
                    case SourceType.DisplayCapture:
                        layerBmp = ScreenCaptureHelper.CaptureScreen(src.DisplayIndex, src.CaptureCursor);
                        shouldDisposeBmp = true;
                        break;

                    case SourceType.WindowCapture:
                        if (src.WindowHandle != IntPtr.Zero)
                        {
                            layerBmp = WindowCaptureHelper.CaptureWindow(src.WindowHandle);
                            shouldDisposeBmp = true;
                        }
                        break;

                    case SourceType.VideoCaptureDevice:
                        layerBmp = CameraHelper.GetLatestFrame();
                        shouldDisposeBmp = true;
                        if (layerBmp != null)
                        {
                            if (src.HorizontalFlip && src.VerticalFlip)
                                layerBmp.RotateFlip(RotateFlipType.RotateNoneFlipXY);
                            else if (src.HorizontalFlip)
                                layerBmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
                            else if (src.VerticalFlip)
                                layerBmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
                        }
                        break;

                    case SourceType.PhoneCamera:
                        layerBmp = PhoneCamera.GetLatestFrame();
                        shouldDisposeBmp = true;
                        if (layerBmp != null)
                        {
                            if (src.HorizontalFlip && src.VerticalFlip)
                                layerBmp.RotateFlip(RotateFlipType.RotateNoneFlipXY);
                            else if (src.HorizontalFlip)
                                layerBmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
                            else if (src.VerticalFlip)
                                layerBmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
                        }
                        break;

                    case SourceType.ImageOverlay:
                    case SourceType.MediaFile:
                        if (!string.IsNullOrWhiteSpace(src.FilePath) && File.Exists(src.FilePath))
                        {
                            if (!_imageCache.TryGetValue(src.FilePath, out layerBmp))
                            {
                                try
                                {
                                    using var stream = new FileStream(src.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                                    using var orig = new Bitmap(stream);
                                    layerBmp = new Bitmap(orig);
                                    _imageCache[src.FilePath] = layerBmp;
                                }
                                catch { }
                            }
                            shouldDisposeBmp = false;
                        }
                        break;

                    case SourceType.TextOverlay:
                        layerBmp = GetCachedTextBitmap(src);
                        shouldDisposeBmp = false;
                        break;

                    case SourceType.ColorSource:
                        layerBmp = GetCachedColorBitmap(src);
                        shouldDisposeBmp = false;
                        break;
                }

                if (layerBmp != null)
                {
                    // Apply Chroma Key
                    if (src.ChromaKeyEnabled)
                    {
                        if (!shouldDisposeBmp)
                        {
                            layerBmp = (Bitmap)layerBmp.Clone();
                            shouldDisposeBmp = true;
                        }
                        BitmapData data = layerBmp.LockBits(new Rectangle(0, 0, layerBmp.Width, layerBmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                        try
                        {
                            ChromaKeyFilter.ApplyChromaKey(data, src.KeyColor, src.KeySimilarity, src.KeySmoothness, src.KeySpillReduction);
                        }
                        finally
                        {
                            layerBmp.UnlockBits(data);
                        }
                    }

                    // Apply Color Adjustments
                    if (src.ColorAdjustEnabled)
                    {
                        if (!shouldDisposeBmp)
                        {
                            layerBmp = (Bitmap)layerBmp.Clone();
                            shouldDisposeBmp = true;
                        }
                        BitmapData data = layerBmp.LockBits(new Rectangle(0, 0, layerBmp.Width, layerBmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                        try
                        {
                            VideoProcAmpFilter.ApplyColorAdjustments(data, src.Brightness, src.Contrast, src.Hue, src.Saturation, src.Gamma);
                        }
                        finally
                        {
                            layerBmp.UnlockBits(data);
                        }
                    }

                    // Draw to canvas with transform & opacity
                    var state = g.Save();

                    float cx = (float)(src.X + src.Width / 2.0);
                    float cy = (float)(src.Y + src.Height / 2.0);

                    g.TranslateTransform(cx, cy);
                    if (Math.Abs(src.Rotation) > 0.01)
                    {
                        g.RotateTransform((float)src.Rotation);
                    }
                    g.TranslateTransform(-cx, -cy);

                    // Opacity matrix
                    ColorMatrix colMatrix = new ColorMatrix
                    {
                        Matrix33 = (float)src.Opacity
                    };
                    ImageAttributes imgAttr = new ImageAttributes();
                    imgAttr.SetColorMatrix(colMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                    Rectangle destRect = new Rectangle((int)src.X, (int)src.Y, (int)src.Width, (int)src.Height);
                    Rectangle srcRect = new Rectangle(
                        (int)src.CropLeft,
                        (int)src.CropTop,
                        (int)Math.Max(1, layerBmp.Width - src.CropLeft - src.CropRight),
                        (int)Math.Max(1, layerBmp.Height - src.CropTop - src.CropBottom));

                    g.DrawImage(layerBmp, destRect, srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height, GraphicsUnit.Pixel, imgAttr);

                    g.Restore(state);
                }
            }
            catch { }
            finally
            {
                if (shouldDisposeBmp)
                {
                    layerBmp?.Dispose();
                }
            }
        }

        private Bitmap GetCachedColorBitmap(SourceItem src)
        {
            string key = $"{src.SolidColor}_{src.Width}_{src.Height}";
            if (_colorCache.TryGetValue(key, out var cached)) return cached;

            int w = (int)Math.Max(10, src.Width);
            int h = (int)Math.Max(10, src.Height);
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics cg = Graphics.FromImage(bmp))
            {
                var col = System.Drawing.Color.FromArgb(src.SolidColor.A, src.SolidColor.R, src.SolidColor.G, src.SolidColor.B);
                using var brush = new SolidBrush(col);
                cg.FillRectangle(brush, 0, 0, w, h);
            }

            _colorCache[key] = bmp;
            return bmp;
        }

        private Bitmap GetCachedTextBitmap(SourceItem src)
        {
            string key = $"{src.TextContent}_{src.FontFamily}_{src.FontSize}_{src.IsBold}_{src.IsItalic}_{src.TextColor}_{src.TextBackgroundColor}_{src.TextOutlineColor}_{src.TextOutlineThickness}_{src.Width}_{src.Height}";
            if (_textCache.TryGetValue(key, out var cached)) return cached;

            int w = (int)Math.Max(50, src.Width);
            int h = (int)Math.Max(30, src.Height);
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                // Background box
                if (src.TextBackgroundColor.A > 0)
                {
                    var bgCol = System.Drawing.Color.FromArgb(src.TextBackgroundColor.A, src.TextBackgroundColor.R, src.TextBackgroundColor.G, src.TextBackgroundColor.B);
                    using var bgBrush = new SolidBrush(bgCol);
                    g.FillRectangle(bgBrush, 0, 0, w, h);
                }

                System.Drawing.FontStyle style = System.Drawing.FontStyle.Regular;
                if (src.IsBold) style |= System.Drawing.FontStyle.Bold;
                if (src.IsItalic) style |= System.Drawing.FontStyle.Italic;

                using var fontFamily = new System.Drawing.FontFamily(string.IsNullOrWhiteSpace(src.FontFamily) ? "Segoe UI" : src.FontFamily);
                using var font = new Font(fontFamily, (float)Math.Max(10, src.FontSize), style, GraphicsUnit.Pixel);
                using var path = new GraphicsPath();

                var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                path.AddString(src.TextContent ?? "", fontFamily, (int)style, (float)Math.Max(10, src.FontSize), new RectangleF(0, 0, w, h), format);

                // Outline
                if (src.TextOutlineThickness > 0 && src.TextOutlineColor.A > 0)
                {
                    var outCol = System.Drawing.Color.FromArgb(src.TextOutlineColor.A, src.TextOutlineColor.R, src.TextOutlineColor.G, src.TextOutlineColor.B);
                    using var pen = new Pen(outCol, (float)src.TextOutlineThickness) { LineJoin = LineJoin.Round };
                    g.DrawPath(pen, path);
                }

                // Text fill
                var textCol = System.Drawing.Color.FromArgb(src.TextColor.A, src.TextColor.R, src.TextColor.G, src.TextColor.B);
                using var textBrush = new SolidBrush(textCol);
                g.FillPath(textBrush, path);
            }

            _textCache[key] = bmp;
            return bmp;
        }

        public void Dispose()
        {
            Stop();
            CameraHelper.Dispose();
            foreach (var img in _imageCache.Values) img.Dispose();
            _imageCache.Clear();
            foreach (var txt in _textCache.Values) txt.Dispose();
            _textCache.Clear();
            foreach (var col in _colorCache.Values) col.Dispose();
            _colorCache.Clear();

            _canvasGraphics?.Dispose();
            _canvasBitmap?.Dispose();
        }
    }
}
