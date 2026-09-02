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
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RamaverseStudio.Models;

namespace RamaverseStudio.Video
{
    /// <summary>
    /// 60 FPS multi-layer compositor. Renders the active scene into a GDI+ canvas,
    /// copies the result into a pooled reference-counted frame, updates the live
    /// preview WriteableBitmap and fans the frame out to recorder / streamer /
    /// replay buffer without any mid-write mutation (each consumer gets its own
    /// reference).
    /// </summary>
    public class CompositorEngine : IDisposable
    {
        private int _canvasWidth = 1920;
        private int _canvasHeight = 1080;
        private int _targetFps = 60;

        private Thread? _renderThread;
        private volatile bool _isRunning = false;

        // Active Scene reference
        private Scene? _currentSceneField;
        public Scene? CurrentScene
        {
            get => _currentSceneField;
            set
            {
                if (ReferenceEquals(_currentSceneField, value) || value == null)
                {
                    _currentSceneField = value;
                    return;
                }

                // Snapshot the outgoing frame so we can transition into the new
                // scene.
                if (_currentSceneField != null && _canvasBitmap != null)
                {
                    try
                    {
                        _previousSceneSnapshot?.Dispose();
                        _previousSceneSnapshot = new Bitmap(_canvasBitmap);
                        BeginTransition(value);
                    }
                    catch { }
                }

                _currentSceneField = value;
            }
        }

        public void BeginTransition(Scene? scene)
        {
            if (scene == null)
            {
                _transitionDurationSec = 0.3;
                _activeTransitionType = TransitionType.CrossFade;
            }
            else
            {
                _activeTransitionType = scene.TransitionEffect;
                _transitionDurationSec = Math.Clamp(scene.TransitionDurationMs / 1000.0, 0.0, 5.0);
            }

            if (_activeTransitionType == TransitionType.Cut || _transitionDurationSec <= 0.001)
            {
                _transitionActive = false;
                _transitionProgress = 1.0;
                _previousSceneSnapshot?.Dispose();
                _previousSceneSnapshot = null;
            }
            else
            {
                _transitionProgress = 0.0;
                _transitionActive = true;
            }
        }

        // Camera helper cache
        public CameraCaptureHelper CameraHelper { get; } = new CameraCaptureHelper();
        public PhoneCameraReceiver PhoneCamera { get; } = new PhoneCameraReceiver();
        public BrowserSourceRenderer BrowserRenderer { get; }
        private string? _activeCameraId;
        private TransitionType _activeTransitionType = TransitionType.CrossFade;

        // Image file & Text caches
        private readonly ConcurrentDictionary<string, Bitmap> _imageCache = new();
        private readonly ConcurrentDictionary<string, Bitmap> _textCache = new();
        private readonly ConcurrentDictionary<string, Bitmap> _colorCache = new();

        // Reusable persistent frame rendering buffers (Eliminates ~480MB/s GC pressure)
        private Bitmap? _canvasBitmap;
        private Graphics? _canvasGraphics;

        // Pooled output frames: the compositor rents, consumers release.
        private readonly VideoFramePool _framePool = new();

        // Snapshot of the ordered sources, refreshed once per frame (thread-safe reads of an ObservableCollection)
        private readonly List<SourceItem> _renderList = new();

        // ---- Scene transitions (cross-fade) ----
        private Bitmap? _previousSceneSnapshot;   // last fully-rendered frame before the switch
        private double _transitionProgress = 1.0; // 0 → 1 during the fade
        private double _transitionDurationSec = 0.3;
        private bool _transitionActive;
        private double _frameDeltaSeconds = 1.0 / 60.0; // seconds between rendered frames

        // Live preview & program WriteableBitmaps (UI accessible)
        public WriteableBitmap PreviewBitmap { get; private set; }
        public WriteableBitmap ProgramBitmap { get; private set; }
        private readonly Dispatcher _uiDispatcher;

        // Studio Mode (Preview staging vs Program live broadcast)
        public bool IsStudioMode { get; set; } = false;
        public Scene? StagedPreviewScene { get; set; }
        public Scene? ProgramScene => CurrentScene;

        private Bitmap? _stagedCanvasBitmap;
        private Graphics? _stagedCanvasGraphics;
        private readonly List<SourceItem> _stagedRenderList = new();

        // Telemetry
        public double ActualFps { get; private set; } = 0;
        public double FrameTimeMs { get; private set; } = 0;
        public long TotalFramesRendered { get; private set; } = 0;
        public long DroppedFrames { get; private set; } = 0;

        /// <summary>
        /// Fired once per rendered frame with a pooled frame carrying ONE reference.
        /// The handler must transfer that reference to its own consumer (and call
        /// AddRef when more than one consumer needs it).
        /// </summary>
        public event Action<SharedFrame>? FrameComposited;
        public Func<float>? AudioPeakLevelProvider { get; set; }

        private readonly float[] _visualizerBands = new float[24];
        private readonly Random _visRandom = new Random();

        private readonly object _renderLock = new object();

        public int CanvasWidth => _canvasWidth;
        public int CanvasHeight => _canvasHeight;

        public CompositorEngine(Dispatcher uiDispatcher, int width = 1920, int height = 1080, int fps = 60)
        {
            _uiDispatcher = uiDispatcher;
            _canvasWidth = width;
            _canvasHeight = height;
            _targetFps = fps;

            BrowserRenderer = new BrowserSourceRenderer(uiDispatcher);
            InitReusableCanvas(width, height);
            PreviewBitmap = new WriteableBitmap(_canvasWidth, _canvasHeight, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
            ProgramBitmap = new WriteableBitmap(_canvasWidth, _canvasHeight, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
        }

        private void InitReusableCanvas(int w, int h)
        {
            _canvasGraphics?.Dispose();
            _canvasBitmap?.Dispose();
            _stagedCanvasGraphics?.Dispose();
            _stagedCanvasBitmap?.Dispose();

            _canvasBitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            _canvasGraphics = Graphics.FromImage(_canvasBitmap);
            _canvasGraphics.CompositingMode = CompositingMode.SourceOver;
            _canvasGraphics.CompositingQuality = CompositingQuality.HighSpeed;
            _canvasGraphics.InterpolationMode = InterpolationMode.Bilinear;
            _canvasGraphics.SmoothingMode = SmoothingMode.HighSpeed;
            _canvasGraphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            _stagedCanvasBitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            _stagedCanvasGraphics = Graphics.FromImage(_stagedCanvasBitmap);
            _stagedCanvasGraphics.CompositingMode = CompositingMode.SourceOver;
            _stagedCanvasGraphics.CompositingQuality = CompositingQuality.HighSpeed;
            _stagedCanvasGraphics.InterpolationMode = InterpolationMode.Bilinear;
            _stagedCanvasGraphics.SmoothingMode = SmoothingMode.HighSpeed;
            _stagedCanvasGraphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
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
                    ProgramBitmap = new WriteableBitmap(_canvasWidth, _canvasHeight, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
                });
            }
        }

        public void SetCanvasResolution(int width, int height, int fps = 60) => SetCanvasDimensions(width, height, fps);

        public void TransitionStagedToProgram(TransitionType? transitionType = null, int? durationMs = null)
        {
            if (StagedPreviewScene == null) return;
            if (transitionType.HasValue) _activeTransitionType = transitionType.Value;
            if (durationMs.HasValue) _transitionDurationSec = Math.Clamp(durationMs.Value / 1000.0, 0.0, 5.0);
            CurrentScene = StagedPreviewScene;
        }

        /// <summary>
        /// Ensures the camera helper is streaming the device a scene source points at.
        /// Called when scenes/sources change so webcams actually come online.
        /// </summary>
        public void EnsureCameraStarted(SourceItem src)
        {
            if (src.Type != SourceType.VideoCaptureDevice || string.IsNullOrWhiteSpace(src.CameraDeviceId))
            {
                return;
            }

            if (_activeCameraId == src.CameraDeviceId && CameraHelper.IsRunning)
            {
                return;
            }

            _activeCameraId = src.CameraDeviceId;
            _ = CameraHelper.StartCameraByIdAsync(src.CameraDeviceId, src.CameraResolutionWidth, src.CameraResolutionHeight, src.CameraFps);
        }

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
            double lastFrameStartSec = 0;

            while (_isRunning)
            {
                long startMs = stopwatch.ElapsedMilliseconds;
                double frameIntervalMs = 1000.0 / _targetFps;
                double frameStartSec = startMs / 1000.0;
                _frameDeltaSeconds = Math.Max(0.001, frameStartSec - lastFrameStartSec);
                lastFrameStartSec = frameStartSec;

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
            SharedFrame? frame = null;

            lock (_renderLock)
            {
                if (_canvasBitmap == null || _canvasGraphics == null)
                    return;

                int w = _canvasWidth;
                int h = _canvasHeight;

                // Background fill
                _canvasGraphics.Clear(System.Drawing.Color.FromArgb(15, 17, 23));

                var scene = CurrentScene;
                if (scene != null)
                {
                    var sources = scene.Sources;

                    // Z-ordered rendering: the "Up/Down" layer buttons and ZIndex
                    // property must actually control draw order.
                    _renderList.Clear();
                    for (int i = 0; i < sources.Count; i++)
                    {
                        _renderList.Add(sources[i]);
                    }
                    _renderList.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));

                    for (int i = 0; i < _renderList.Count; i++)
                    {
                        var src = _renderList[i];
                        if (!src.IsVisible) continue;
                        RenderSource(_canvasGraphics, src, w, h);
                    }
                }

                // Scene transitions: blend the outgoing snapshot over the new
                // scene, according to active transition type.
                if (_transitionActive && _previousSceneSnapshot != null)
                {
                    _transitionProgress += _frameDeltaSeconds / Math.Max(0.05, _transitionDurationSec);
                    if (_transitionProgress >= 1.0)
                    {
                        _transitionProgress = 1.0;
                        _transitionActive = false;
                        _previousSceneSnapshot.Dispose();
                        _previousSceneSnapshot = null;
                    }
                    else
                    {
                        double p = Math.Clamp(_transitionProgress, 0.0, 1.0);
                        double smoothP = p * p * (3.0 - 2.0 * p); // smoothstep easing

                        switch (_activeTransitionType)
                        {
                            case TransitionType.SlideLeft:
                                int offsetX = (int)(-smoothP * w);
                                _canvasGraphics.DrawImage(_previousSceneSnapshot, offsetX, 0, w, h);
                                break;

                            case TransitionType.SlideRight:
                                int offsetRX = (int)(smoothP * w);
                                _canvasGraphics.DrawImage(_previousSceneSnapshot, offsetRX, 0, w, h);
                                break;

                            case TransitionType.WipeLeft:
                                int wipeW = (int)((1.0 - smoothP) * w);
                                if (wipeW > 0)
                                {
                                    _canvasGraphics.DrawImage(_previousSceneSnapshot,
                                        new Rectangle(0, 0, wipeW, h), 0, 0, wipeW, h, GraphicsUnit.Pixel);
                                }
                                break;

                            case TransitionType.WipeRight:
                                int wipeRW = (int)(smoothP * w);
                                int remW = w - wipeRW;
                                if (remW > 0)
                                {
                                    _canvasGraphics.DrawImage(_previousSceneSnapshot,
                                        new Rectangle(wipeRW, 0, remW, h), wipeRW, 0, remW, h, GraphicsUnit.Pixel);
                                }
                                break;

                            case TransitionType.Cut:
                                break;

                            case TransitionType.CrossFade:
                            case TransitionType.LumaWipe:
                            default:
                                float alpha = 1.0f - (float)smoothP;
                                var colMatrix = new ColorMatrix { Matrix33 = alpha };
                                using (var imgAttr = new ImageAttributes())
                                {
                                    imgAttr.SetColorMatrix(colMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                                    _canvasGraphics.DrawImage(_previousSceneSnapshot,
                                        new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, imgAttr);
                                }
                                break;
                        }
                    }
                }

                // Copy canvas into a pooled frame owned by this method (1 ref).
                frame = _framePool.Rent(w, h, initialRefs: 1);

                BitmapData bmpData = _canvasBitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int byteCount = bmpData.Stride * h;
                    System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, frame.Pixels, 0, byteCount);
                }
                finally
                {
                    _canvasBitmap.UnlockBits(bmpData);
                }

                // If in Studio Mode, also render Staged Preview Scene
                if (IsStudioMode && StagedPreviewScene != null && _stagedCanvasGraphics != null && _stagedCanvasBitmap != null)
                {
                    _stagedCanvasGraphics.Clear(System.Drawing.Color.FromArgb(15, 17, 23));
                    _stagedRenderList.Clear();
                    for (int i = 0; i < StagedPreviewScene.Sources.Count; i++)
                    {
                        _stagedRenderList.Add(StagedPreviewScene.Sources[i]);
                    }
                    _stagedRenderList.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));

                    for (int i = 0; i < _stagedRenderList.Count; i++)
                    {
                        var src = _stagedRenderList[i];
                        if (!src.IsVisible) continue;
                        RenderSource(_stagedCanvasGraphics, src, w, h);
                    }
                }
            }

            if (frame == null) return;

            // Preview decimation: the canvas renders at full FPS but the preview
            // WriteableBitmap refreshes on a tier-derived cadence (every frame on
            // strong PCs, every other frame on weaker ones). At 1080p this halves
            // a ~400 MB/s memcpy with no visible difference on the throttled tier.
            int decimation = Math.Max(1, Services.AutoTuneService.PreviewDecimation);
            bool skipPreview = (_previewFrameCounter++ % decimation) != 0;

            // Backlog guard: if the UI thread has not yet consumed the previous
            // preview dispatch (modal dialog, slow render, minimized window),
            // drop this frame's preview instead of queueing more references —
            // an unbounded dispatch queue otherwise pins one pool frame per
            // entry and memory climbs without bound.
            skipPreview = skipPreview || System.Threading.Volatile.Read(ref _previewInFlight) > 0;

            if (!skipPreview)
            {
                // Preview: AddRef so the UI write is independent of downstream consumers.
                System.Threading.Interlocked.Increment(ref _previewInFlight);
                var previewFrame = frame.AddRef();
                bool isStudio = IsStudioMode;
                SharedFrame? stagedFrame = null;

                if (isStudio && _stagedCanvasBitmap != null)
                {
                    lock (_renderLock)
                    {
                        stagedFrame = _framePool.Rent(frame.Width, frame.Height, initialRefs: 1);
                        BitmapData sData = _stagedCanvasBitmap.LockBits(new Rectangle(0, 0, frame.Width, frame.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                        try
                        {
                            System.Runtime.InteropServices.Marshal.Copy(sData.Scan0, stagedFrame.Pixels, 0, sData.Stride * frame.Height);
                        }
                        finally
                        {
                            _stagedCanvasBitmap.UnlockBits(sData);
                        }
                    }
                }

                _uiDispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (isStudio && stagedFrame != null)
                        {
                            if (PreviewBitmap.PixelWidth == stagedFrame.Width && PreviewBitmap.PixelHeight == stagedFrame.Height)
                            {
                                PreviewBitmap.WritePixels(
                                    new System.Windows.Int32Rect(0, 0, stagedFrame.Width, stagedFrame.Height),
                                    stagedFrame.Pixels, stagedFrame.Stride, 0);
                            }
                            if (ProgramBitmap.PixelWidth == previewFrame.Width && ProgramBitmap.PixelHeight == previewFrame.Height)
                            {
                                ProgramBitmap.WritePixels(
                                    new System.Windows.Int32Rect(0, 0, previewFrame.Width, previewFrame.Height),
                                    previewFrame.Pixels, previewFrame.Stride, 0);
                            }
                        }
                        else
                        {
                            if (PreviewBitmap.PixelWidth == previewFrame.Width && PreviewBitmap.PixelHeight == previewFrame.Height)
                            {
                                PreviewBitmap.WritePixels(
                                    new System.Windows.Int32Rect(0, 0, previewFrame.Width, previewFrame.Height),
                                    previewFrame.Pixels, previewFrame.Stride, 0);
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        previewFrame.Release();
                        stagedFrame?.Release();
                        System.Threading.Interlocked.Decrement(ref _previewInFlight);
                    }
                }), DispatcherPriority.Render);
            }

            // Hand the caller's reference to the frame event consumers.
            FrameComposited?.Invoke(frame);
        }

        private long _previewFrameCounter;
        // 0/1 gate: at most one preview dispatch queued at a time. Touched from
        // the render thread (increment) and the UI thread (decrement).
        private int _previewInFlight;

        private void RenderSource(Graphics g, SourceItem src, int canvasWidth, int canvasHeight)
        {
            Bitmap? layerBmp = null;
            bool shouldDisposeBmp = false;

            try
            {
                switch (src.Type)
                {
                    case SourceType.DisplayCapture:
                        layerBmp = src.UseHardwareCapture
                            ? WgcCaptureHelper.CaptureScreenHardware(src.DisplayIndex, src.CaptureCursor)
                            : ScreenCaptureHelper.CaptureScreen(src.DisplayIndex, src.CaptureCursor);
                        shouldDisposeBmp = true;
                        break;

                    case SourceType.WindowCapture:
                        if (src.WindowHandle != IntPtr.Zero)
                        {
                            layerBmp = src.UseHardwareCapture
                                ? WgcCaptureHelper.CaptureWindowHardware(src.WindowHandle)
                                : WindowCaptureHelper.CaptureWindow(src.WindowHandle);
                            shouldDisposeBmp = true;
                        }
                        break;

                    case SourceType.BrowserSource:
                        layerBmp = BrowserRenderer.GetFrame(src);
                        shouldDisposeBmp = true;
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

                    case SourceType.AudioVisualizer:
                        layerBmp = RenderAudioVisualizer(src);
                        shouldDisposeBmp = true;
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

                    using (var imgAttr = new ImageAttributes())
                    {
                        if (src.Opacity < 1.0)
                        {
                            var colMatrix = new ColorMatrix { Matrix33 = (float)src.Opacity };
                            imgAttr.SetColorMatrix(colMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                        }

                        Rectangle destRect = new Rectangle((int)Math.Round(src.X), (int)Math.Round(src.Y), (int)Math.Round(src.Width), (int)Math.Round(src.Height));
                        int srcX = (int)Math.Clamp(src.CropLeft, 0, layerBmp.Width - 1);
                        int srcY = (int)Math.Clamp(src.CropTop, 0, layerBmp.Height - 1);
                        int srcW = Math.Max(1, (int)Math.Round(layerBmp.Width - src.CropLeft - src.CropRight));
                        int srcH = Math.Max(1, (int)Math.Round(layerBmp.Height - src.CropTop - src.CropBottom));
                        srcW = Math.Min(srcW, layerBmp.Width - srcX);
                        srcH = Math.Min(srcH, layerBmp.Height - srcY);

                        if (srcW > 0 && srcH > 0)
                        {
                            g.DrawImage(layerBmp, destRect, srcX, srcY, srcW, srcH, GraphicsUnit.Pixel, imgAttr);
                        }
                    }

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

            if (_colorCache.Count > 32)
            {
                foreach (var old in _colorCache.Keys.Take(_colorCache.Count - 8))
                {
                    if (_colorCache.TryRemove(old, out var dead)) dead.Dispose();
                }
            }

            return bmp;
        }

        /// <summary>
        /// Formats the live timer text for a TimerMode-enabled source. Returns
        /// null for Disabled (static content is used as-is).
        /// </summary>
        private static string? GetLiveTimerText(SourceItem src)
        {
            return src.SourceTimerMode switch
            {
                SourceItem.TimerMode.Countdown => FormatCountdown(src.TimerTargetUtc - DateTime.UtcNow),
                SourceItem.TimerMode.Stopwatch => FormatCountdown(DateTime.UtcNow - src.TimerStartUtc),
                SourceItem.TimerMode.Clock => DateTime.Now.ToString("HH:mm:ss"),
                _ => null
            };
        }

        private static string FormatCountdown(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            if (t.TotalHours >= 1)
                return $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
            return $"{t.Minutes:D2}:{t.Seconds:D2}";
        }

        private Bitmap GetCachedTextBitmap(SourceItem src)
        {
            // Live timer modes bypass the cache: content changes every second.
            string? liveText = GetLiveTimerText(src);
            string effectiveText = liveText ?? (src.TextContent ?? "");
            bool isLive = liveText != null;

            string key = $"{effectiveText}_{src.FontFamily}_{src.FontSize}_{src.IsBold}_{src.IsItalic}_{src.TextColor}_{src.TextBackgroundColor}_{src.TextOutlineColor}_{src.TextOutlineThickness}_{src.Width}_{src.Height}";
            if (!isLive && _textCache.TryGetValue(key, out var cached)) return cached;

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

                path.AddString(effectiveText, fontFamily, (int)style, (float)Math.Max(10, src.FontSize), new RectangleF(0, 0, w, h), format);

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

            // Evict aggressively once text cache grows (editing sessions or live timers)
            if (_textCache.Count > 32)
            {
                foreach (var old in _textCache.Keys.Take(_textCache.Count - 8))
                {
                    if (_textCache.TryRemove(old, out var dead)) dead.Dispose();
                }
            }

            return bmp;
        }

        private Bitmap RenderAudioVisualizer(SourceItem src)
        {
            int w = Math.Max(64, (int)src.Width);
            int h = Math.Max(32, (int)src.Height);
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);

            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.Transparent);

                float peakDb = AudioPeakLevelProvider?.Invoke() ?? -60.0f;
                float normLevel = Math.Clamp((peakDb + 60.0f) / 60.0f, 0.0f, 1.0f);

                int barCount = 20;
                float barWidth = (float)w / barCount;
                float spacing = Math.Max(2f, barWidth * 0.25f);
                float actualBarW = Math.Max(2f, barWidth - spacing);

                for (int i = 0; i < barCount; i++)
                {
                    float freqWeight = (float)Math.Sin((double)i / barCount * Math.PI);
                    float targetH = (normLevel * (0.25f + 0.75f * freqWeight + (float)(_visRandom.NextDouble() * 0.3 * normLevel))) * h;

                    _visualizerBands[i] = _visualizerBands[i] * 0.70f + targetH * 0.30f;
                    float barH = Math.Clamp(_visualizerBands[i], 3.0f, h);

                    float x = i * barWidth;
                    float y = h - barH;

                    using var brush = new SolidBrush(System.Drawing.Color.FromArgb(235, 255, 255, 255));
                    g.FillRectangle(brush, x, y, actualBarW, barH);
                }
            }

            return bmp;
        }

        public void Dispose()
        {
            Stop();
            BrowserRenderer.Dispose();
            CameraHelper.Dispose();
            PhoneCamera.Dispose();
            _previousSceneSnapshot?.Dispose();
            _previousSceneSnapshot = null;
            foreach (var img in _imageCache.Values) img.Dispose();
            _imageCache.Clear();
            foreach (var txt in _textCache.Values) txt.Dispose();
            _textCache.Clear();
            foreach (var col in _colorCache.Values) col.Dispose();
            _colorCache.Clear();

            _canvasGraphics?.Dispose();
            _canvasBitmap?.Dispose();
            _stagedCanvasGraphics?.Dispose();
            _stagedCanvasBitmap?.Dispose();
        }
    }
}
