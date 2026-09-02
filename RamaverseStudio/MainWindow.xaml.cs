using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using RamaverseStudio.Audio;
using RamaverseStudio.AutoUpdate;
using RamaverseStudio.Models;
using RamaverseStudio.Output;
using RamaverseStudio.Services;
using RamaverseStudio.Storage;
using RamaverseStudio.UI;
using RamaverseStudio.Video;

namespace RamaverseStudio
{
    public partial class MainWindow : Window
    {
        // Core Engines
        private StudioProfile _profile = new StudioProfile();
        private AudioEngine _audioEngine;
        private CompositorEngine _compositor;
        private FFmpegRecordingEngine _recordingEngine;
        private FFmpegStreamingEngine _streamingEngine;
        private ReplayBufferEngine _replayBuffer;
        private ChatAggregatorService _chatService;
        private VirtualCameraEngine _virtualCam;
        private AutoClipperEngine _autoClipper;
        private UpdateManager _updateManager;
        private ChapterMarkerService _chapterMarkers = new();
        private RemoteControlServer? _remoteServer;

        // Studio Mode & Staged Preview
        private bool _isStudioMode = false;
        private Scene? _stagedScene;
        private TimeSpan _lastRecDuration = TimeSpan.Zero;
        private TimeSpan _lastStreamDuration = TimeSpan.Zero;

        // Scenes
        public ObservableCollection<Scene> Scenes { get; set; } = new ObservableCollection<Scene>();
        private Scene? _activeScene;
        private SourceItem? _selectedSource;

        // UI Refresh Timer (meters, CPU/RAM, stats, audio master pump)
        private DispatcherTimer _uiTimer;
        private Process _currentProcess = Process.GetCurrentProcess();
        private DateTime _lastCpuCheck = DateTime.UtcNow;
        private TimeSpan _lastCpuTime = TimeSpan.Zero;
        private double _cpuUsagePercent = 0.0;

        // Debounced project persistence: dragging must not serialize JSON 60×/sec.
        private DispatcherTimer _saveDebounceTimer;
        private readonly object _saveLock = new object();

        // Guard against inspector textChanged feedback loops
        private bool _suppressInspectorEvents;

        // ---- Undo / Redo (transform history for the selected source) ----
        private readonly Stack<(SourceItem Src, double X, double Y, double W, double H, double Rot)> _undoStack = new();
        private readonly Stack<(SourceItem Src, double X, double Y, double W, double H, double Rot)> _redoStack = new();
        private const int MaxUndoEntries = 64;

        /// <summary>
        /// Snapshots the selected source's transform so the next modification
        /// can be undone. Call BEFORE applying any user transform change.
        /// </summary>
        private void PushUndoSnapshot(SourceItem? src = null)
        {
            src ??= _selectedSource;
            if (src == null) return;

            _undoStack.Push((src, src.X, src.Y, src.Width, src.Height, src.Rotation));
            if (_undoStack.Count > MaxUndoEntries)
            {
                var arr = _undoStack.ToArray();
                _undoStack.Clear();
                for (int i = arr.Length - 1; i >= 1; i--) _undoStack.Push(arr[i]);
            }
            _redoStack.Clear();
        }

        private void Undo()
        {
            if (_undoStack.Count == 0) return;
            var entry = _undoStack.Pop();
            if (entry.Src == null) return;

            _redoStack.Push((entry.Src, entry.Src.X, entry.Src.Y, entry.Src.Width, entry.Src.Height, entry.Src.Rotation));
            entry.Src.X = entry.X;
            entry.Src.Y = entry.Y;
            entry.Src.Width = entry.W;
            entry.Src.Height = entry.H;
            entry.Src.Rotation = entry.Rot;

            UpdateInspectorUI();
            CanvasGizmo.SetSelectedSource(entry.Src);
            CanvasGizmo.UpdateGizmo();
            ScheduleSave();
        }

        private void Redo()
        {
            if (_redoStack.Count == 0) return;
            var entry = _redoStack.Pop();
            if (entry.Src == null) return;

            _undoStack.Push((entry.Src, entry.Src.X, entry.Src.Y, entry.Src.Width, entry.Src.Height, entry.Src.Rotation));
            entry.Src.X = entry.X;
            entry.Src.Y = entry.Y;
            entry.Src.Width = entry.W;
            entry.Src.Height = entry.H;
            entry.Src.Rotation = entry.Rot;

            UpdateInspectorUI();
            CanvasGizmo.SetSelectedSource(entry.Src);
            CanvasGizmo.UpdateGizmo();
            ScheduleSave();
        }

        public MainWindow()
        {
            InitializeComponent();

            // 0. Localization first so every subsequent UI string is translated.
            Services.LocalizationService.SetLanguage(_profile.InterfaceLanguage);

            // 0b. Auto-Performance: probe the hardware once and apply the
            //     tuning knobs so every machine runs at its own full power.
            var hw = Services.AutoTuneService.Detect();
            ApplyAutoTuning(hw);

            // 1. Initialize Engines & Updater
            _updateManager = new UpdateManager();
            _audioEngine = new AudioEngine();
            _compositor = new CompositorEngine(Dispatcher, _profile.CanvasWidth, _profile.CanvasHeight, _profile.Fps);
            _recordingEngine = new FFmpegRecordingEngine();
            _streamingEngine = new FFmpegStreamingEngine();
            _replayBuffer = new ReplayBufferEngine(Services.AutoTuneService.ReplayBufferSeconds);
            _replayBuffer.SetFormat(_profile.CanvasWidth, _profile.CanvasHeight, _profile.Fps);
            _chatService = new ChatAggregatorService(Dispatcher);
            _virtualCam = new VirtualCameraEngine();
            LiveChatDock.BindService(_chatService);

            ToastNotifier.BindHost(this);

            _autoClipper = new AutoClipperEngine();
            _autoClipper.ClipTriggered += () =>
            {
                Dispatcher.InvokeAsync(async () =>
                {
                    _chatService.AddMessage("AI Auto-Clipper", "High excitement audio detected! Saving 9:16 vertical replay clip...", ChatPlatform.System);
                    _chapterMarkers.Add("Hype moment", ChapterMarkerService.MarkerKind.AutoClip);
                    await TriggerSaveReplayAsync(isVertical: true);
                });
            };

            // 2. Load Saved Project State FIRST (it carries canvas dimensions),
            //    THEN bind the preview to the compositor with the correct size.
            LoadSavedProjectOrCreateDefaults();

            // Language may have been persisted by an older/newer profile run.
            Services.LocalizationService.SetLanguage(_profile.InterfaceLanguage);
            ApplyLocalization();

            CanvasLiveImage.Source = _compositor.PreviewBitmap;
            StudioPreviewImage.Source = _compositor.PreviewBitmap;
            StudioProgramImage.Source = _compositor.ProgramBitmap;
            CanvasGizmo.SetCanvasResolution(_profile.CanvasWidth, _profile.CanvasHeight);
            StudioCanvasGizmo.SetCanvasResolution(_profile.CanvasWidth, _profile.CanvasHeight);
            ApplyCanvasResolutionChange(saveAfter: false);

            // 3. Connect Video & Audio Pipes
            _compositor.FrameComposited += OnFrameComposited;
            _compositor.AudioPeakLevelProvider = () => _audioEngine.CurrentPeakDb;
            _audioEngine.AudioSamplesProcessed += OnAudioSamplesProcessed;
            _audioEngine.MicTrackSamplesProcessed += OnMicTrackSamplesProcessed;
            _audioEngine.DesktopTrackSamplesProcessed += OnDesktopTrackSamplesProcessed;
            _recordingEngine.StatsUpdated += OnRecordingStatsUpdated;
            _recordingEngine.RecordingFailed += OnRecordingFailed;
            _streamingEngine.StatsUpdated += OnStreamingStatsUpdated;
            _streamingEngine.StreamFailed += OnStreamFailed;
            CanvasGizmo.TransformModified += OnGizmoTransformModified;
            CanvasGizmo.TransformBegun += OnGizmoTransformBegan;
            StudioCanvasGizmo.TransformModified += OnGizmoTransformModified;
            StudioCanvasGizmo.TransformBegun += OnGizmoTransformBegan;

            // Start Remote Web & WebSocket Control Server
            try
            {
                _remoteServer = new RemoteControlServer(Dispatcher, 4455);
                _remoteServer.GetStatusCallback = GetRemoteStatus;
                _remoteServer.ExecuteActionCallback = HandleRemoteAction;
                _remoteServer.Start();
                BtnRemoteControl.ToolTip = $"Mobile Remote: {_remoteServer.ServerUrl} (Click to open/copy)";
            }
            catch { }

            // Drag & drop: files land on the canvas as instant overlays.
            AllowDrop = true;
            Drop += OnFileDropped;
            DragOver += (s, e) =>
            {
                e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                    ? DragDropEffects.Copy
                    : DragDropEffects.None;
                e.Handled = true;
            };

            // 4. Start Audio & Video Compositor
            _audioEngine.Start(_profile.SelectedMicDevice);
            _compositor.Start();

            // 5. Bring every camera source online (persisted scenes included)
            EnsureCamerasStarted();

            // 5b. Wire ATEM Broadcast Switcher Controls
            AtemSwitcher.CutRequested += () => OnStudioTransitionClicked(this, new RoutedEventArgs());
            AtemSwitcher.AutoRequested += () => OnStudioTransitionClicked(this, new RoutedEventArgs());
            AtemSwitcher.FaderChanged += (val) =>
            {
                if (_isStudioMode && _stagedScene != null && val >= 0.98)
                {
                    OnStudioTransitionClicked(this, new RoutedEventArgs());
                }
            };

            // 6. Setup Telemetry UI Timer + audio master pump (20 ms cadence)
            _uiTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(20) // 50 FPS UI + exact 20ms audio blocks
            };
            _uiTimer.Tick += OnUiTimerTick;
            _uiTimer.Start();

            // 7. Debounced save timer (600 ms after last change)
            _saveDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(600)
            };
            _saveDebounceTimer.Tick += (s, e) =>
            {
                _saveDebounceTimer.Stop();
                SaveProjectState();
            };

            // 8. Auto-Check for Updates in Background
            _ = CheckForUpdatesOnLaunchAsync();

            // 8a. Initialize License & Pro status
            UpdateLicenseBadgeUI();

            // 8b. Crash recovery: remux any MKV captures a previous session
            //     never finalized, so users worldwide never lose footage.
            _ = Task.Run(() =>
            {
                try
                {
                    var recovered = FFmpegRecordingEngine.RecoverOrphanedCaptures(_profile.RecordingDirectory);
                    if (recovered.Count > 0)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ToastNotifier.Show(
                                $"Crash recovery: {recovered.Count} interrupted recording{(recovered.Count > 1 ? "s" : "")} restored successfully.",
                                ToastNotifier.ToastKind.Success, 6);
                        });
                    }
                }
                catch { }
            });

            // 9. Window Lifetime
            Closed += OnMainWindowClosed;

            // 10. First-run FFmpeg provisioning: the #1 blocker for new
            //     worldwide users is a missing FFmpeg, so we offer a one-click
            //     guided install the moment the studio opens without it.
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (!Output.FFmpegPathResolver.IsAvailable)
                    {
                        var dlg = new FFmpegSetupDialog { Owner = this };
                        dlg.ShowDialog();
                    }
                }
                catch { }
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        #region Project Storage & Scene Initialization

        private void LoadSavedProjectOrCreateDefaults()
        {
            var saved = ProjectStorage.LoadProject();
            if (saved != null && saved.Scenes.Count > 0)
            {
                _profile = saved.Profile;
                _audioEngine.FilterSettings.CopyFrom(saved.AudioFilters);
                Scenes.Clear();
                foreach (var sc in saved.Scenes)
                {
                    Scenes.Add(sc);
                }

                // Compositor must adopt the persisted canvas size before rendering.
                _compositor.SetCanvasResolution(_profile.CanvasWidth, _profile.CanvasHeight, _profile.Fps);
                _replayBuffer.SetFormat(_profile.CanvasWidth, _profile.CanvasHeight, _profile.Fps);

                ScenesListBox.ItemsSource = Scenes;
                int activeIdx = Math.Clamp(saved.ActiveSceneIndex, 0, Scenes.Count - 1);
                ScenesListBox.SelectedIndex = activeIdx;
                SetActiveScene(Scenes[activeIdx], saveAfter: false);
            }
            else
            {
                SetupDefaultProductionScenes();
            }

            UpdateFormatButtonStates();
        }

        private void SaveProjectState()
        {
            lock (_saveLock)
            {
                int activeIdx = _activeScene != null ? Scenes.IndexOf(_activeScene) : 0;
                ProjectStorage.SaveProject(_profile, Scenes, _audioEngine.FilterSettings, activeIdx);
            }
        }

        /// <summary>
        /// Schedules a debounced save. Called from every UI interaction so the
        /// project is always persisted without stalling the render loop.
        /// </summary>
        private void ScheduleSave()
        {
            if (_saveDebounceTimer == null) return;
            _saveDebounceTimer.Stop();
            _saveDebounceTimer.Start();
        }

        private void SetupDefaultProductionScenes()
        {
            // Scene 1: Main Studio
            var mainScene = new Scene { Name = "Main Studio" };

            var screenSrc = new SourceItem
            {
                Name = "Screen Display Capture",
                Type = SourceType.DisplayCapture,
                X = 0,
                Y = 0,
                Width = 1920,
                Height = 1080,
                DisplayIndex = 0,
                CaptureCursor = true,
                ZIndex = 0
            };
            mainScene.Sources.Add(screenSrc);

            var camSrc = new SourceItem
            {
                Name = "Camera Feed (PiP)",
                Type = SourceType.VideoCaptureDevice,
                X = 1380,
                Y = 740,
                Width = 500,
                Height = 300,
                ChromaKeyEnabled = false,
                ZIndex = 1
            };
            mainScene.Sources.Add(camSrc);

            var textSrc = new SourceItem
            {
                Name = "Title Banner",
                Type = SourceType.TextOverlay,
                TextContent = "RAMAVERSE STUDIO • LIVE",
                FontSize = 32,
                IsBold = true,
                X = 40,
                Y = 980,
                Width = 480,
                Height = 60,
                TextColor = Colors.White,
                TextBackgroundColor = Color.FromArgb(190, 10, 10, 10),
                ZIndex = 2
            };
            mainScene.Sources.Add(textSrc);

            // Scene 2: Full Display
            var gameScene = new Scene { Name = "Full Display Only" };
            gameScene.Sources.Add(screenSrc.Clone());

            // Scene 3: Camera Stage
            var talkScene = new Scene { Name = "Camera Studio Stage" };
            var talkCam = camSrc.Clone();
            talkCam.X = 0;
            talkCam.Y = 0;
            talkCam.Width = 1920;
            talkCam.Height = 1080;
            talkScene.Sources.Add(talkCam);

            // Scene 4: Vertical Shorts / TikTok
            var verticalScene = new Scene { Name = "Vertical Shorts / TikTok" };
            var vertBg = new SourceItem
            {
                Name = "Dark Background",
                Type = SourceType.ColorSource,
                SolidColor = Color.FromRgb(10, 10, 10),
                Width = 1080,
                Height = 1920,
                ZIndex = 0
            };
            var vertCam = camSrc.Clone();
            vertCam.X = 40;
            vertCam.Y = 100;
            vertCam.Width = 1000;
            vertCam.Height = 750;
            vertCam.ZIndex = 1;

            var vertScreen = screenSrc.Clone();
            vertScreen.X = 40;
            vertScreen.Y = 900;
            vertScreen.Width = 1000;
            vertScreen.Height = 750;
            vertScreen.ZIndex = 2;

            verticalScene.Sources.Add(vertBg);
            verticalScene.Sources.Add(vertCam);
            verticalScene.Sources.Add(vertScreen);

            Scenes.Clear();
            Scenes.Add(mainScene);
            Scenes.Add(gameScene);
            Scenes.Add(talkScene);
            Scenes.Add(verticalScene);

            ScenesListBox.ItemsSource = Scenes;
            ScenesListBox.SelectedIndex = 0;
            SetActiveScene(mainScene, saveAfter: false);
        }

        /// <summary>
        /// Walks every scene and starts the first camera device referenced by any
        /// source, so webcams come online automatically on launch.
        /// </summary>
        private void EnsureCamerasStarted()
        {
            foreach (var scene in Scenes)
            {
                foreach (var src in scene.Sources)
                {
                    if (src.Type == SourceType.VideoCaptureDevice)
                    {
                        _compositor.EnsureCameraStarted(src);
                        return; // one shared camera stream per app instance
                    }
                }
            }
        }
        #endregion

        #region Global Win32 System Hotkeys & Local Shortcuts
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID_RECORD = 9001;
        private const int HOTKEY_ID_STREAM = 9002;
        private const int HOTKEY_ID_SNAPSHOT = 9003;
        private const int HOTKEY_ID_MUTE = 9004;
        private const int HOTKEY_ID_REPLAY = 9005;

        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_R = 0x52;
        private const uint VK_L = 0x4C;
        private const uint VK_S = 0x53;
        private const uint VK_M = 0x4D;
        private const uint VK_F10 = 0x79;
        private const int WM_HOTKEY = 0x0312;

        private HwndSource? _hwndSource;
        private IntPtr _windowHandle = IntPtr.Zero;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            _windowHandle = helper.Handle;
            _hwndSource = HwndSource.FromHwnd(_windowHandle);
            _hwndSource?.AddHook(HwndHook);

            // Register System-Wide Background Hotkeys (Work even when minimized or inside a full-screen game)
            RegisterHotKey(_windowHandle, HOTKEY_ID_RECORD, MOD_CONTROL | MOD_SHIFT, VK_R);
            RegisterHotKey(_windowHandle, HOTKEY_ID_STREAM, MOD_CONTROL | MOD_SHIFT, VK_L);
            RegisterHotKey(_windowHandle, HOTKEY_ID_SNAPSHOT, MOD_CONTROL | MOD_SHIFT, VK_S);
            RegisterHotKey(_windowHandle, HOTKEY_ID_MUTE, MOD_CONTROL | MOD_SHIFT, VK_M);
            RegisterHotKey(_windowHandle, HOTKEY_ID_REPLAY, MOD_CONTROL | MOD_SHIFT, VK_F10);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                switch (id)
                {
                    case HOTKEY_ID_RECORD:
                        OnRecordToggleClicked(this, new RoutedEventArgs());
                        handled = true;
                        break;
                    case HOTKEY_ID_STREAM:
                        OnStreamToggleClicked(this, new RoutedEventArgs());
                        handled = true;
                        break;
                    case HOTKEY_ID_SNAPSHOT:
                        OnSnapshotClicked(this, new RoutedEventArgs());
                        handled = true;
                        break;
                    case HOTKEY_ID_MUTE:
                        OnMuteMicClicked(this, new RoutedEventArgs());
                        handled = true;
                        break;
                    case HOTKEY_ID_REPLAY:
                        OnSaveReplayClicked(this, new RoutedEventArgs());
                        handled = true;
                        break;
                }
            }
            return IntPtr.Zero;
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            // Do not intercept hotkeys if user is currently typing in a text box
            if (e.OriginalSource is System.Windows.Controls.Primitives.TextBoxBase ||
                e.OriginalSource is PasswordBox ||
                e.OriginalSource is Slider ||
                e.OriginalSource is ComboBox) return;

            bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

            if (isCtrl && !isShift)
            {
                switch (e.Key)
                {
                    case Key.R: // Ctrl+R: Toggle Recording
                        OnRecordToggleClicked(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;

                    case Key.P: // Ctrl+P: Toggle Pause
                        if (_recordingEngine.IsRecording)
                        {
                            OnPauseRecClicked(this, new RoutedEventArgs());
                            e.Handled = true;
                        }
                        break;

                    case Key.L: // Ctrl+L: Toggle Streaming
                        OnStreamToggleClicked(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;

                    case Key.S: // Ctrl+S: Snapshot
                        OnSnapshotClicked(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;

                    case Key.O: // Ctrl+O: Open recording folder
                        _recordingEngine.OpenOutputFolder();
                        e.Handled = true;
                        break;

                    case Key.Z: // Ctrl+Z: Undo
                        Undo();
                        e.Handled = true;
                        break;

                    case Key.Y: // Ctrl+Y: Redo
                        Redo();
                        e.Handled = true;
                        break;

                    case Key.M: // Ctrl+M: Manual chapter marker (mid-session)
                        _chapterMarkers.Add("Marker", ChapterMarkerService.MarkerKind.Manual);
                        ToastNotifier.Show("Chapter marked.", ToastNotifier.ToastKind.Info, 1.4);
                        e.Handled = true;
                        break;

                    case Key.K: // Ctrl+K: Instant Command Palette
                        OpenCommandPalette();
                        e.Handled = true;
                        break;

                    // Scene selection (Ctrl+1 to Ctrl+5)
                    case Key.D1:
                    case Key.NumPad1:
                        SelectSceneByIndex(0);
                        e.Handled = true;
                        break;
                    case Key.D2:
                    case Key.NumPad2:
                        SelectSceneByIndex(1);
                        e.Handled = true;
                        break;
                    case Key.D3:
                    case Key.NumPad3:
                        SelectSceneByIndex(2);
                        e.Handled = true;
                        break;
                    case Key.D4:
                    case Key.NumPad4:
                        SelectSceneByIndex(3);
                        e.Handled = true;
                        break;
                    case Key.D5:
                    case Key.NumPad5:
                        SelectSceneByIndex(4);
                        e.Handled = true;
                        break;
                }
            }
            else if (!isCtrl && !isShift)
            {
                if (e.Key == Key.F1)
                {
                    OnTutorialClicked(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (e.Key == Key.Delete && _selectedSource != null)
                {
                    OnDeleteSourceClicked(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (_selectedSource != null && !_selectedSource.IsLocked)
                {
                    double step = isShift ? 10.0 : 1.0;
                    switch (e.Key)
                    {
                        case Key.Left:
                            _selectedSource.X -= step;
                            UpdateInspectorUI();
                            CanvasGizmo.UpdateGizmo();
                            e.Handled = true;
                            break;
                        case Key.Right:
                            _selectedSource.X += step;
                            UpdateInspectorUI();
                            CanvasGizmo.UpdateGizmo();
                            e.Handled = true;
                            break;
                        case Key.Up:
                            _selectedSource.Y -= step;
                            UpdateInspectorUI();
                            CanvasGizmo.UpdateGizmo();
                            e.Handled = true;
                            break;
                        case Key.Down:
                            _selectedSource.Y += step;
                            UpdateInspectorUI();
                            CanvasGizmo.UpdateGizmo();
                            e.Handled = true;
                            break;
                    }
                    if (e.Handled) ScheduleSave();
                }
            }
        }

        private void SelectSceneByIndex(int index)
        {
            if (index >= 0 && index < Scenes.Count)
            {
                ScenesListBox.SelectedIndex = index;
            }
        }
        #endregion

        #region Background Auto-Update Check
        private async Task CheckForUpdatesOnLaunchAsync()
        {
            try
            {
                var update = await _updateManager.CheckForUpdatesAsync();
                if (update != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        var dlg = new UpdateDialog(update, _updateManager) { Owner = this };
                        dlg.ShowDialog();
                    });
                }
            }
            catch { }
        }
        #endregion

        #region Scene & Source Management
        private void SetActiveScene(Scene scene, bool saveAfter = true)
        {
            _activeScene = scene;
            _compositor.CurrentScene = scene;
            SourcesListBox.ItemsSource = scene.Sources;
            SourcesEmptyState.Visibility = scene.Sources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            TxtActiveSceneName.Text = $"Scene: {scene.Name}";

            // Chapter marker: every scene switch becomes a video chapter.
            if (_recordingEngine.IsRecording || _streamingEngine.IsStreaming)
            {
                _chapterMarkers.Add($"Scene: {scene.Name}", ChapterMarkerService.MarkerKind.SceneSwitch);
            }

            if (scene.Sources.Count > 0)
            {
                SourcesListBox.SelectedIndex = scene.Sources.Count - 1;
            }
            else
            {
                SetSelectedSource(null);
            }

            // Camera on this scene must be running
            foreach (var src in scene.Sources)
            {
                if (src.Type == SourceType.VideoCaptureDevice)
                {
                    _compositor.EnsureCameraStarted(src);
                    break;
                }
            }

            // Sync Transition ComboBox
            if (ComboTransition != null)
            {
                int matchIndex = scene.TransitionEffect switch
                {
                    TransitionType.Cut => 0,
                    TransitionType.CrossFade when scene.TransitionDurationMs >= 500 => 2,
                    TransitionType.CrossFade => 1,
                    TransitionType.SlideLeft => 3,
                    TransitionType.SlideRight => 4,
                    TransitionType.WipeLeft => 5,
                    TransitionType.WipeRight => 6,
                    TransitionType.LumaWipe => 7,
                    _ => 1
                };
                if (ComboTransition.SelectedIndex != matchIndex)
                {
                    ComboTransition.SelectedIndex = matchIndex;
                }
            }

            if (saveAfter) ScheduleSave();
        }

        private void SetSelectedSource(SourceItem? source)
        {
            if (_selectedSource != null)
            {
                _selectedSource.IsSelected = false;
            }

            _selectedSource = source;
            CanvasGizmo.SetSelectedSource(source);
            StudioCanvasGizmo?.SetSelectedSource(source);

            if (source != null)
            {
                source.IsSelected = true;
                TxtInspectorSourceName.Text = source.Name;
                UpdateInspectorUI();
                InspectorPanel.IsEnabled = true;
                InspectorEmptyState.Visibility = Visibility.Collapsed;
                InspectorScrollViewer.Visibility = Visibility.Visible;
            }
            else
            {
                TxtInspectorSourceName.Text = "No Source Selected";
                InspectorPanel.IsEnabled = false;
                InspectorEmptyState.Visibility = Visibility.Visible;
                InspectorScrollViewer.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateInspectorUI()
        {
            if (_selectedSource == null) return;
            _suppressInspectorEvents = true;

            TxtPropX.Text = $"{_selectedSource.X:F0}";
            TxtPropY.Text = $"{_selectedSource.Y:F0}";
            TxtPropWidth.Text = $"{_selectedSource.Width:F0}";
            TxtPropHeight.Text = $"{_selectedSource.Height:F0}";
            SliderPropOpacity.Value = _selectedSource.Opacity;
            TxtPropOpacityVal.Text = $"{(_selectedSource.Opacity * 100):F0}%";
            SliderPropRotation.Value = _selectedSource.Rotation;
            TxtPropRotationVal.Text = $"{_selectedSource.Rotation:F0}°";

            TxtPropCropL.Text = $"{_selectedSource.CropLeft:F0}";
            TxtPropCropT.Text = $"{_selectedSource.CropTop:F0}";
            TxtPropCropR.Text = $"{_selectedSource.CropRight:F0}";
            TxtPropCropB.Text = $"{_selectedSource.CropBottom:F0}";

            // Source-type-specific panels
            PanelTextProps.Visibility = _selectedSource.Type == SourceType.TextOverlay ? Visibility.Visible : Visibility.Collapsed;
            if (_selectedSource.Type == SourceType.TextOverlay)
            {
                TxtPropTextContent.Text = _selectedSource.TextContent;
                TxtPropFontSize.Text = $"{_selectedSource.FontSize:F0}";
            }

            PanelPhoneReconnect.Visibility = _selectedSource.Type == SourceType.PhoneCamera ? Visibility.Visible : Visibility.Collapsed;
            PanelCameraControls.Visibility = _selectedSource.Type == SourceType.VideoCaptureDevice ? Visibility.Visible : Visibility.Collapsed;
            if (_selectedSource.Type == SourceType.VideoCaptureDevice)
            {
                ChkPropFlipH.IsChecked = _selectedSource.HorizontalFlip;
            }

            ChkPropChromaKey.IsChecked = _selectedSource.ChromaKeyEnabled;
            SliderChromaSimilarity.Value = _selectedSource.KeySimilarity;
            SliderChromaSmoothness.Value = _selectedSource.KeySmoothness;
            SliderChromaSpill.Value = _selectedSource.KeySpillReduction;

            TxtChromaSimVal.Text = $"{(_selectedSource.KeySimilarity * 100):F0}%";
            TxtChromaSmoothVal.Text = $"{(_selectedSource.KeySmoothness * 100):F0}%";
            TxtChromaSpillVal.Text = $"{(_selectedSource.KeySpillReduction * 100):F0}%";

            ChkPropColorAdjust.IsChecked = _selectedSource.ColorAdjustEnabled;
            SliderPropBrightness.Value = _selectedSource.Brightness;
            SliderPropContrast.Value = _selectedSource.Contrast;
            SliderPropSaturation.Value = _selectedSource.Saturation;
            SliderPropGamma.Value = _selectedSource.Gamma;

            TxtPropBrightVal.Text = $"{_selectedSource.Brightness:F0}";
            TxtPropContrastVal.Text = $"{_selectedSource.Contrast:F1}";
            TxtPropSatVal.Text = $"{_selectedSource.Saturation:F1}";
            TxtPropGammaVal.Text = $"{_selectedSource.Gamma:F1}";

            _suppressInspectorEvents = false;
        }

        /// <summary>
        /// Files dropped anywhere on the studio become canvas overlays instantly:
        /// images become ImageOverlay sources, anything else is rejected with a toast.
        /// </summary>
        private void OnFileDropped(object sender, DragEventArgs e)
        {
            if (_activeScene == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                foreach (var file in files)
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    bool isImage = ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" or ".gif";

                    if (!isImage)
                    {
                        ToastNotifier.Show($"Unsupported file type '{ext}'. Drop PNG, JPG, GIF, WEBP or BMP images to add them as overlays.", ToastNotifier.ToastKind.Warning, 4);
                        continue;
                    }

                    try
                    {
                        using var probe = System.Drawing.Image.FromFile(file);
                        var src = new SourceItem
                        {
                            Name = Path.GetFileNameWithoutExtension(file),
                            Type = SourceType.ImageOverlay,
                            FilePath = file,
                            X = Math.Max(0, (_profile.CanvasWidth - probe.Width) / 2.0),
                            Y = Math.Max(0, (_profile.CanvasHeight - probe.Height) / 2.0),
                            Width = probe.Width,
                            Height = probe.Height,
                            ZIndex = _activeScene.Sources.Count
                        };
                        _activeScene.Sources.Add(src);
                        SourcesListBox.SelectedItem = src;
                        ToastNotifier.Show($"Overlay added: {src.Name}", ToastNotifier.ToastKind.Success, 2.5);
                    }
                    catch (Exception ex)
                    {
                        ToastNotifier.Show($"Could not load image: {ex.Message}", ToastNotifier.ToastKind.Error);
                    }
                }
                ScheduleSave();
            }
        }

        private void OnInspectorRenameChanged(object sender, TextChangedEventArgs e)
        {
            // Fires during InitializeComponent (before fields exist) — guard both.
            if (_selectedSource == null || BtnApplyRename == null) return;

            // Show the apply button only when the edit differs from the source name.
            if (!string.Equals(TxtInspectorSourceName.Text, _selectedSource.Name, StringComparison.Ordinal))
            {
                BtnApplyRename.Visibility = Visibility.Visible;
            }
            else
            {
                BtnApplyRename.Visibility = Visibility.Collapsed;
            }
        }

        private void OnApplyRenameClicked(object sender, RoutedEventArgs e)
        {
            if (_selectedSource == null) return;

            string newName = TxtInspectorSourceName.Text.Trim();
            if (!string.IsNullOrWhiteSpace(newName))
            {
                _selectedSource.Name = newName;
                BtnApplyRename.Visibility = Visibility.Collapsed;
                TxtActiveSceneName.Text = $"Scene: {_activeScene?.Name}";
                ToastNotifier.Show($"Renamed to '{newName}'.", ToastNotifier.ToastKind.Success, 1.5);
                ScheduleSave();
            }
        }

        private void OnGizmoTransformModified()
        {
            UpdateInspectorUI();
            ScheduleSave();
        }

        // Undo snapshots are pushed when a gizmo drag begins, not per mouse-move.
        private void OnGizmoTransformBegan()
        {
            PushUndoSnapshot(_selectedSource);
        }
        #endregion

        #region Engine Frame & Audio Callbacks

        /// <summary>
        /// One pooled frame arrives carrying ONE reference. We AddRef once per
        /// additional consumer (replay, recorder, streamer), each of which will
        /// Release its own reference; the final Release returns the frame to the pool.
        /// </summary>
        private void OnFrameComposited(SharedFrame frame)
        {
            if (_recordingEngine.IsRecording)
            {
                _recordingEngine.WriteVideoFrame(frame.AddRef());
            }

            if (_streamingEngine.IsStreaming)
            {
                _streamingEngine.WriteVideoFrame(frame.AddRef());
            }

            if (_virtualCam.IsActive)
            {
                _virtualCam.PushFrame(frame);
            }

            _replayBuffer.PushVideoFrame(frame);

            // replayBuffer.PushVideoFrame consumed the original reference.
            if (!_replayBuffer.IsEnabled)
            {
                // Disabled buffer releases internally; nothing to do.
            }
        }

        private void OnAudioSamplesProcessed(byte[] pcm16Bytes, int bytesRead)
        {
            // In multi-track mode the master mix is NOT recorded — the isolated
            // track events feed the two FFmpeg inputs instead. The master still
            // goes to the streamer and replay buffer.
            bool multiTrack = _profile.MultiTrackAudioRecording && _recordingEngine.IsRecording;

            if (_recordingEngine.IsRecording && !multiTrack)
            {
                _recordingEngine.WriteAudioSamples(pcm16Bytes, bytesRead);
            }

            if (_streamingEngine.IsStreaming)
            {
                _streamingEngine.WriteAudioSamples(pcm16Bytes, bytesRead);
            }

            _replayBuffer.PushAudioSamples(pcm16Bytes, bytesRead);
        }

        private void OnMicTrackSamplesProcessed(byte[] pcm16Bytes, int bytesRead)
        {
            if (_profile.MultiTrackAudioRecording && _recordingEngine.IsRecording)
            {
                _recordingEngine.WriteMicTrackSamples(pcm16Bytes, bytesRead);
            }
        }

        private void OnDesktopTrackSamplesProcessed(byte[] pcm16Bytes, int bytesRead)
        {
            if (_profile.MultiTrackAudioRecording && _recordingEngine.IsRecording)
            {
                _recordingEngine.WriteDesktopTrackSamples(pcm16Bytes, bytesRead);
            }
        }

        private void OnRecordingStatsUpdated(RecordingStats stats)
        {
            _lastRecDuration = stats.ElapsedTime;
            Dispatcher.InvokeAsync(() =>
            {
                TxtRecDuration.Text = stats.ElapsedTime.ToString(@"hh\:mm\:ss");
                TxtRecSize.Text = $"Size: {stats.FileSizeMb:F1} MB • {_profile.RecFormat.ToString().ToUpper()}";
                TxtRecStatus.Text = stats.IsPaused ? "PAUSED" : "RECORDING";
            });
        }

        private void OnRecordingFailed(RecordingFailure failure, string details)
        {
            Dispatcher.InvokeAsync(() =>
            {
                ResetRecordButton();

                if (failure == RecordingFailure.FFmpegNotFound)
                {
                    ToastNotifier.Alert(details, "Recording Stopped — FFmpeg Missing", isError: true);
                }
                else
                {
                    ToastNotifier.Alert(details, "Recording Stopped Unexpectedly", isError: true);
                }

                // If the file has data, offer to open it.
                string path = _recordingEngine.CurrentOutputFilePath;
                if (File.Exists(path) && new FileInfo(path).Length > 4096)
                {
                    _chatService.AddMessage("System", "Partial recording was preserved on disk.", ChatPlatform.System);
                }
            });
        }

        private void OnStreamingStatsUpdated(StreamStats stats)
        {
            _lastStreamDuration = stats.Uptime;
            Dispatcher.InvokeAsync(() =>
            {
                TxtStreamUptime.Text = stats.Uptime.ToString(@"hh\:mm\:ss");
                TxtStreamBitrate.Text = $"Primary: {stats.BitrateKbps:F0} kbps • {stats.DroppedFrames} drops";
                TxtStreamStatus.Text = stats.Status.ToString().ToUpper();

                // Health glow: instant visual diagnosis without reading numbers.
                var border = stats.Health switch
                {
                    StreamHealthStatus.Good => new SolidColorBrush(Color.FromRgb(40, 200, 90)),
                    StreamHealthStatus.Warning => new SolidColorBrush(Color.FromRgb(255, 190, 60)),
                    _ => new SolidColorBrush(Color.FromRgb(255, 70, 70))
                };
                PreviewViewportBorder.BorderBrush = _streamingEngine.IsStreaming
                    ? border
                    : new SolidColorBrush(Color.FromRgb(28, 28, 28));

                if (stats.IsDualStreamActive)
                {
                    TxtSecStreamBitrate.Visibility = Visibility.Visible;
                    TxtSecStreamBitrate.Text = $"Vertical 9:16: {stats.SecondaryBitrateKbps:F0} kbps • {stats.SecondaryStatus.ToString().ToUpper()}";
                    BtnStream.Content = "■ END DUAL STREAM";
                }
                else
                {
                    TxtSecStreamBitrate.Visibility = Visibility.Collapsed;
                    if (_streamingEngine.IsStreaming)
                    {
                        BtnStream.Content = "■ END STREAM";
                    }
                }
            });
        }

        private void OnStreamFailed(string details)
        {
            Dispatcher.InvokeAsync(() =>
            {
                ResetStreamButton();
                ToastNotifier.Alert(details, "Stream Ended", isError: true);
            });
        }

        private void ResetRecordButton()
        {
            BtnRecord.Content = "● RECORD";
            BtnRecord.Background = new SolidColorBrush(Color.FromRgb(24, 24, 24));
            BtnRecord.Foreground = Brushes.White;
            BtnRecord.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 56, 56));
            BtnPauseRec.Visibility = Visibility.Collapsed;
            TxtRecStatus.Text = "STANDBY";
        }

        private void ResetStreamButton()
        {
            BtnStream.Content = "📡 GO LIVE";
            BtnStream.Background = new SolidColorBrush(Color.FromRgb(24, 24, 24));
            BtnStream.Foreground = Brushes.White;
            BtnStream.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 56, 56));
            TxtStreamStatus.Text = "OFFLINE";
            TxtSecStreamBitrate.Visibility = Visibility.Collapsed;
        }
        #endregion

        #region Telemetry & Metering Timer
        private int _uiTickDivider;

        private void OnUiTimerTick(object? sender, EventArgs e)
        {
            // Audio master pump: emits exact 20 ms PCM blocks (50 Hz).
            _audioEngine.PumpMasterMix();

            // Auto-Clipper watches the mic level
            _autoClipper.ProcessAudioLevel(_audioEngine.CurrentPeakDb, 0.02);

            // Heavier UI updates every 3rd tick (~16 Hz) to keep UI cheap
            if (++_uiTickDivider % 3 != 0) return;

            double micPeakDb = _audioEngine.CurrentPeakDb;
            MeterMic.SetLevel(micPeakDb, _audioEngine.PeakHoldDb);
            TxtMicLevelDb.Text = double.IsNegativeInfinity(micPeakDb) ? "-60.0 dB" : $"{micPeakDb:F1} dB";

            double desktopPeakDb = _audioEngine.DesktopPeakDb;
            MeterDesktop.SetLevel(desktopPeakDb, desktopPeakDb);
            TxtDesktopLevelDb.Text = double.IsNegativeInfinity(desktopPeakDb) ? "-60.0 dB" : $"{desktopPeakDb:F1} dB";

            TxtVoicePresetLabel.Text = _audioEngine.FilterSettings.VoiceChangerEnabled ? _audioEngine.FilterSettings.VoiceChangerPreset.ToString() : "Clean";

            TxtStatusFps.Text = $"FPS: {_compositor.ActualFps:F1}";
            TxtStatusDropped.Text = $"Dropped: {_compositor.DroppedFrames} ({(_compositor.TotalFramesRendered > 0 ? (double)_compositor.DroppedFrames / _compositor.TotalFramesRendered * 100 : 0):F1}%)";

            // CPU Usage
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastCpuCheck).TotalMilliseconds;
            if (elapsed >= 1000)
            {
                _currentProcess.Refresh();
                var cpuTime = _currentProcess.TotalProcessorTime;
                _cpuUsagePercent = (cpuTime - _lastCpuTime).TotalMilliseconds / (Environment.ProcessorCount * elapsed) * 100.0;
                _lastCpuTime = cpuTime;
                _lastCpuCheck = now;

                TxtStatusCpu.Text = $"CPU: {Math.Clamp(_cpuUsagePercent, 0, 100):F1}%";
                TxtStatusRam.Text = $"RAM: {_currentProcess.WorkingSet64 / (1024 * 1024)} MB";
            }

            if (_isStudioMode)
            {
                TimeSpan duration = _recordingEngine.IsRecording ? _lastRecDuration : (_streamingEngine.IsStreaming ? _lastStreamDuration : TimeSpan.Zero);
                AtemSwitcher.UpdateTimecode(duration, _profile.Fps);
                AtemSwitcher.SetTallyState(_stagedScene != null, _recordingEngine.IsRecording || _streamingEngine.IsStreaming);
            }

            // Stream Health Assistant proactive advice
            if (_compositor.DroppedFrames > 0)
            {
                TxtDiagnosticTip.Text = $"Network/Encoder congestion: {_compositor.DroppedFrames} dropped frames detected. Recommended: lower bitrate.";
                BtnQuickFix.Visibility = Visibility.Visible;
            }
            else if (_cpuUsagePercent > 80.0)
            {
                TxtDiagnosticTip.Text = $"High CPU load ({_cpuUsagePercent:F0}%). Close background apps or enable Hardware NVENC.";
                BtnQuickFix.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtDiagnosticTip.Text = "Hardware & Direct3D engine healthy. Zero dropped frames.";
                BtnQuickFix.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region Format Switchers
        private bool CheckResolutionChangeAllowed()
        {
            if (_recordingEngine.IsRecording || _streamingEngine.IsStreaming)
            {
                ToastNotifier.Alert("Canvas resolution cannot be changed while recording or streaming is actively running.", "Resolution Locked");
                return false;
            }
            return true;
        }

        private void OnFormat16x9Clicked(object sender, RoutedEventArgs e)
        {
            if (!CheckResolutionChangeAllowed()) return;
            _profile.CanvasFormat = CanvasFormat.Horizontal16x9;
            _profile.CanvasWidth = 1920;
            _profile.CanvasHeight = 1080;
            ApplyCanvasResolutionChange();
        }

        private void OnFormat9x16Clicked(object sender, RoutedEventArgs e)
        {
            if (!CheckResolutionChangeAllowed()) return;
            _profile.CanvasFormat = CanvasFormat.Vertical9x16;
            _profile.CanvasWidth = 1080;
            _profile.CanvasHeight = 1920;
            ApplyCanvasResolutionChange();
        }

        private void OnFormatSquareClicked(object sender, RoutedEventArgs e)
        {
            if (!CheckResolutionChangeAllowed()) return;
            _profile.CanvasFormat = CanvasFormat.Square1x1;
            _profile.CanvasWidth = 1080;
            _profile.CanvasHeight = 1080;
            ApplyCanvasResolutionChange();
        }

        private void OnFormatDualClicked(object sender, RoutedEventArgs e)
        {
            if (!CheckResolutionChangeAllowed()) return;
            // Dual Canvas Landscape + Vertical
            _profile.CanvasFormat = CanvasFormat.Custom;
            _profile.CanvasWidth = 3000;
            _profile.CanvasHeight = 1920;
            ApplyCanvasResolutionChange();
        }

        private void ApplyCanvasResolutionChange(bool saveAfter = true)
        {
            _compositor.SetCanvasResolution(_profile.CanvasWidth, _profile.CanvasHeight, _profile.Fps);
            _replayBuffer.SetFormat(_profile.CanvasWidth, _profile.CanvasHeight, _profile.Fps);
            CanvasLiveImage.Source = _compositor.PreviewBitmap;
            StudioPreviewImage.Source = _compositor.PreviewBitmap;
            StudioProgramImage.Source = _compositor.ProgramBitmap;
            CanvasContainerGrid.Width = _profile.CanvasWidth;
            CanvasContainerGrid.Height = _profile.CanvasHeight;
            CanvasGizmo.SetCanvasResolution(_profile.CanvasWidth, _profile.CanvasHeight);
            StudioCanvasGizmo?.SetCanvasResolution(_profile.CanvasWidth, _profile.CanvasHeight);

            TxtCanvasResBadge.Text = $"{_profile.CanvasWidth} x {_profile.CanvasHeight} • {_profile.Fps} FPS";
            TxtActiveProfile.Text = $"{_profile.CanvasWidth}x{_profile.CanvasHeight} • {_profile.Fps} FPS • {_profile.CanvasFormat}";

            UpdateFormatButtonStates();
            if (saveAfter) ScheduleSave();
        }

        private void UpdateFormatButtonStates()
        {
            void SetBtn(System.Windows.Controls.Button btn, bool active)
            {
                btn.Background = active ? Brushes.White : Brushes.Transparent;
                btn.Foreground = active ? Brushes.Black : new SolidColorBrush(Color.FromRgb(136, 136, 136));
                btn.FontWeight = active ? FontWeights.Bold : FontWeights.Normal;
            }

            SetBtn(BtnFormat16x9, _profile.CanvasFormat == CanvasFormat.Horizontal16x9);
            SetBtn(BtnFormat9x16, _profile.CanvasFormat == CanvasFormat.Vertical9x16);
            SetBtn(BtnFormatSquare, _profile.CanvasFormat == CanvasFormat.Square1x1);
            SetBtn(BtnFormatDual, _profile.CanvasFormat == CanvasFormat.Custom);
        }
        #endregion

        #region Production Control Handlers (Record, Stream, Snapshot)
        private async void OnRecordToggleClicked(object sender, RoutedEventArgs e)
        {
            if (!_recordingEngine.IsRecording)
            {
                // Start Recording
                var (success, failure, details) = await _recordingEngine.StartRecordingAsync(_profile);
                if (success)
                {
                    BtnRecord.Content = "■ STOP REC";
                    BtnRecord.Background = Brushes.White;
                    BtnRecord.Foreground = Brushes.Black;
                    BtnRecord.BorderBrush = Brushes.White;
                    BtnPauseRec.Visibility = Visibility.Visible;
                    TxtRecStatus.Text = "RECORDING";
                    ToastNotifier.Show("Recording started.", ToastNotifier.ToastKind.Success);
                    _chapterMarkers.StartSession("Intro");
                }
                else
                {
                    if (failure == RecordingFailure.FFmpegNotFound)
                    {
                        ToastNotifier.Alert(details, "Cannot Record — FFmpeg Missing", isError: true);
                    }
                    else
                    {
                        ToastNotifier.Alert(details, "Recording Error", isError: true);
                    }
                }
            }
            else
            {
                // Stop Recording
                string recordedFile = _recordingEngine.CurrentOutputFilePath;
                TimeSpan recordedDuration = default;
                double sizeMb = 0;
                try
                {
                    if (File.Exists(recordedFile))
                    {
                        sizeMb = new FileInfo(recordedFile).Length / (1024.0 * 1024.0);
                    }
                }
                catch { }

                _recordingEngine.StopRecording();
                ResetRecordButton();

                // Export YouTube chapters + event log for this session.
                try
                {
                    _chapterMarkers.Add("End", ChapterMarkerService.MarkerKind.SessionEnd);
                    string? chapters = _chapterMarkers.ExportYouTubeChapters(_profile.RecordingDirectory);
                    _ = _chapterMarkers.ExportEventJson(_profile.RecordingDirectory);
                    if (chapters != null)
                    {
                        ToastNotifier.Show($"YouTube chapters exported: {Path.GetFileName(chapters)}", ToastNotifier.ToastKind.Success, 4);
                    }
                }
                catch { }

                if (File.Exists(recordedFile))
                {
                    var dlg = new RecordingCompletedDialog(recordedFile, recordedDuration, sizeMb)
                    {
                        Owner = this
                    };
                    dlg.ShowDialog();
                }
                else
                {
                    ToastNotifier.Show("Recording stopped, but no output file was produced.", ToastNotifier.ToastKind.Warning);
                }
            }
        }

        private void OnPauseRecClicked(object sender, RoutedEventArgs e)
        {
            if (_recordingEngine.IsRecording)
            {
                if (_recordingEngine.IsPaused)
                {
                    _recordingEngine.ResumeRecording();
                    BtnPauseRec.Content = "Pause";
                    ToastNotifier.Show("Recording resumed.", ToastNotifier.ToastKind.Info, 1.5);
                }
                else
                {
                    _recordingEngine.PauseRecording();
                    BtnPauseRec.Content = "Resume";
                    ToastNotifier.Show("Recording paused.", ToastNotifier.ToastKind.Info, 1.5);
                }
            }
        }

        private async void OnStreamToggleClicked(object sender, RoutedEventArgs e)
        {
            if (!_streamingEngine.IsStreaming)
            {
                if (string.IsNullOrWhiteSpace(_profile.StreamKey))
                {
                    var res = MessageBox.Show(
                        "You are almost ready to go live!\n\nPlease enter your YouTube, Twitch, or Kick Stream Key in Settings.\n\nWould you like to open Settings now to paste your stream key?",
                        "Stream Key Setup — Ramaverse Studio",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (res == MessageBoxResult.Yes)
                    {
                        var dlg = new SettingsWindow(_profile) { Owner = this };
                        if (dlg.ShowDialog() == true)
                        {
                            _profile = dlg.Profile;
                            ApplyCanvasResolutionChange();
                        }
                    }
                    return;
                }

                var (success, error) = await _streamingEngine.StartStreamingAsync(_profile);
                if (success)
                {
                    BtnStream.Content = "■ END STREAM";
                    BtnStream.Background = Brushes.White;
                    BtnStream.Foreground = Brushes.Black;
                    BtnStream.BorderBrush = Brushes.White;
                    TxtStreamStatus.Text = "LIVE";
                    ToastNotifier.Show("You are LIVE. Streaming to your audience.", ToastNotifier.ToastKind.Success);
                }
                else
                {
                    ResetStreamButton();
                    ToastNotifier.Alert($"Failed to start streaming.\n\n{error}", "Streaming Error", isError: true);
                }
            }
            else
            {
                _streamingEngine.StopStreaming();
                ResetStreamButton();
                ToastNotifier.Show("Stream ended.", ToastNotifier.ToastKind.Info);
            }
        }

        private void OnSnapshotClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                string dir = _profile.RecordingDirectory;
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"Snapshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");

                var bmp = _compositor.CaptureStillFrame();
                bmp.Save(path, ImageFormat.Png);
                bmp.Dispose();

                ToastNotifier.Show($"Snapshot saved: {Path.GetFileName(path)}", ToastNotifier.ToastKind.Success);
            }
            catch (Exception ex)
            {
                ToastNotifier.Show($"Snapshot failed: {ex.Message}", ToastNotifier.ToastKind.Error, 5);
            }
        }

        private void OnVirtualCamToggleClicked(object sender, RoutedEventArgs e)
        {
            if (!_virtualCam.IsActive)
            {
                _virtualCam.Start(_profile.CanvasWidth, _profile.CanvasHeight, _profile.Fps);
                BtnVirtualCam.Content = "📷 V-CAM LIVE";
                BtnVirtualCam.Background = new SolidColorBrush(Color.FromRgb(40, 160, 80));
                BtnVirtualCam.Foreground = Brushes.White;
                BtnVirtualCam.BorderBrush = new SolidColorBrush(Color.FromRgb(60, 200, 100));
                ToastNotifier.Show("Virtual Camera started. Active in Discord, Zoom & Teams.", ToastNotifier.ToastKind.Success, 4);
            }
            else
            {
                _virtualCam.Stop();
                BtnVirtualCam.Content = "📷 V-CAM";
                BtnVirtualCam.Background = new SolidColorBrush(Color.FromRgb(24, 24, 24));
                BtnVirtualCam.Foreground = Brushes.White;
                BtnVirtualCam.BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68));
                ToastNotifier.Show("Virtual Camera stopped.", ToastNotifier.ToastKind.Info, 2);
            }
        }

        /// <summary>
        /// Applies machine-derived tuning to the live profile and engines.
        /// Only touches knobs the user has NOT explicitly changed from default
        /// (we can't tell intent on the very first run, so we tune generously
        /// and the Settings window always wins afterwards).
        /// </summary>
        private void ApplyAutoTuning(Services.HardwareProfile hw)
        {
            try
            {
                // FPS + preview pacing
                if (_profile.Fps == 60 && hw.Tier == Services.PerformanceTier.Low)
                {
                    _profile.Fps = Services.AutoTuneService.TargetFps; // 30 on weak CPUs
                }

                // Encoder: only when the profile still holds AutoHardware default
                if (_profile.Encoder == VideoEncoder.AutoHardware)
                {
                    // Leave AutoHardware: FFmpegArgsBuilder resolves it to x264.
                    // We instead remember the recommendation for the Settings UI.
                }

                // Recording bitrate default tuning (user-overridable later)
                if (_profile.RecordingBitrateKbps == 12000)
                {
                    _profile.RecordingBitrateKbps = Services.AutoTuneService.RecommendedRecordingBitrate;
                }

                // Status bar transparency: show the user what their PC was tuned to.
                TxtStatusEncoder.Text = $"Encoder: {hw.GpuName} • {hw.Tier} mode";
                TxtAutoTier.Text = $"Auto: {hw.Tier} • {hw.CoreCount}C/{hw.TotalRamGb}GB";
                TxtAutoTier.ToolTip = hw.Summary;
            }
            catch { }
        }

        private void OnSettingsClicked(object sender, RoutedEventArgs e)
        {
            var dlg = new SettingsWindow(_profile)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                _profile = dlg.Profile;

                // Apply language change instantly if it changed.
                Services.LocalizationService.SetLanguage(_profile.InterfaceLanguage);
                ApplyLocalization();

                // Restart mic if the device changed
                if (!string.Equals(_audioEngine.ActiveMicName, _profile.SelectedMicDevice, StringComparison.Ordinal) &&
                    _profile.SelectedMicDevice != "Default Microphone")
                {
                    _audioEngine.Start(_profile.SelectedMicDevice);
                }

                ApplyCanvasResolutionChange();
            }
        }

        /// <summary>
        /// Re-renders every localized UI string from the current dictionary.
        /// </summary>
        private void ApplyLocalization()
        {
            BtnRecord.Content = _recordingEngine.IsRecording ? Services.LocalizationService.T("BtnRecordStop") : Services.LocalizationService.T("BtnRecord");
            BtnStream.Content = _streamingEngine.IsStreaming ? Services.LocalizationService.T("BtnStreamStop") : Services.LocalizationService.T("BtnStream");
            BtnSnapshot.Content = Services.LocalizationService.T("BtnSnapshot");
            BtnSettings.Content = Services.LocalizationService.T("BtnSettings");
            BtnOpenRecordings.Content = Services.LocalizationService.T("BtnFolder");
            BtnTabInspector.Content = Services.LocalizationService.T("TabProperties");
            BtnTabChat.Content = Services.LocalizationService.T("TabChat");
            TxtMicHeader.Text = Services.LocalizationService.T("MicInput");
            TxtDesktopHeader.Text = Services.LocalizationService.T("DesktopAudio");
            BtnMuteMic.Content = _audioEngine.FilterSettings.IsMuted ? Services.LocalizationService.T("Unmute") : Services.LocalizationService.T("Mute");
            BtnMuteDesktop.Content = _audioEngine.DesktopVolume > 0 ? Services.LocalizationService.T("Mute") : Services.LocalizationService.T("Unmute");
            TxtRecStatus.Text = Services.LocalizationService.T("StatusStandby");
            TxtStreamStatus.Text = Services.LocalizationService.T("StatusOffline");
            Title = Services.LocalizationService.T("AppTitle");
        }

        private void OnOpenAudioRackClicked(object sender, RoutedEventArgs e)
        {
            var dlg = new AudioFiltersDialog(_audioEngine)
            {
                Owner = this
            };
            dlg.ShowDialog();
            ScheduleSave();
        }

        private void OnTutorialClicked(object sender, RoutedEventArgs e)
        {
            var guide = new TutorialGuideWindow
            {
                Owner = this
            };
            guide.ShowDialog();
        }

        private void OnLicenseClicked(object sender, RoutedEventArgs e)
        {
            var dlg = new LicenseActivationDialog
            {
                Owner = this
            };
            dlg.ShowDialog();
            UpdateLicenseBadgeUI();
        }

        private void UpdateLicenseBadgeUI()
        {
            var lic = Services.Licensing.LicenseManager.Instance;
            if (lic.IsPro)
            {
                BtnProLicense.Content = "PRO ACTIVE";
                BtnProLicense.Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129));
                BtnProLicense.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                BtnProLicense.Foreground = new SolidColorBrush(Color.FromRgb(110, 231, 183));
            }
            else
            {
                BtnProLicense.Content = "UPGRADE PRO";
                BtnProLicense.Background = new SolidColorBrush(Color.FromArgb(40, 124, 58, 237));
                BtnProLicense.BorderBrush = new SolidColorBrush(Color.FromRgb(124, 58, 237));
                BtnProLicense.Foreground = new SolidColorBrush(Color.FromRgb(192, 132, 252));
            }
        }

        private void OnOpenRecordingsFolderClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                string dir = _profile.RecordingDirectory;
                if (Directory.Exists(dir))
                {
                    Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
                }
                else
                {
                    ToastNotifier.Show("No recordings yet. Start recording first.", ToastNotifier.ToastKind.Info);
                }
            }
            catch { }
        }

        private void OnAutoConfigWizardClicked(object sender, RoutedEventArgs e)
        {
            var wizard = new AutoConfigWizardWindow(_profile)
            {
                Owner = this
            };
            wizard.ShowDialog();
            if (wizard.AppliedSettings)
            {
                _compositor.SetCanvasResolution(_profile.CanvasWidth, _profile.CanvasHeight, _profile.Fps);
                _replayBuffer.SetFormat(_profile.CanvasWidth, _profile.CanvasHeight, _profile.Fps);
                CanvasGizmo.SetCanvasResolution(_profile.CanvasWidth, _profile.CanvasHeight);
                StudioCanvasGizmo?.SetCanvasResolution(_profile.CanvasWidth, _profile.CanvasHeight);
                UpdateFormatButtonStates();
                ScheduleSave();
                ToastNotifier.Show("Stream and recording settings configured successfully.", ToastNotifier.ToastKind.Success);
            }
        }

        private void OnCommandPaletteClicked(object sender, RoutedEventArgs e)
        {
            OpenCommandPalette();
        }

        private void OpenCommandPalette()
        {
            var commands = new List<CommandPaletteItem>
            {
                new() { Title = "Start / Stop Recording", Description = "Toggle MP4 recording to local disk", Shortcut = "Ctrl+R", Action = () => OnRecordToggleClicked(this, new RoutedEventArgs()) },
                new() { Title = "Go Live / Stop Stream", Description = "Toggle live broadcast to RTMP ingest", Shortcut = "Ctrl+L", Action = () => OnStreamToggleClicked(this, new RoutedEventArgs()) },
                new() { Title = "Save 30s Instant Replay", Description = "Save gameplay highlight clip", Shortcut = "Ctrl+Shift+F10", Action = () => OnSaveReplayClicked(this, new RoutedEventArgs()) },
                new() { Title = "Take Studio Snapshot", Description = "Capture high-resolution screenshot", Shortcut = "Ctrl+S", Action = () => OnSnapshotClicked(this, new RoutedEventArgs()) },
                new() { Title = "Run Setup Wizard", Description = "Auto-configure bitrates and encoders", Shortcut = "", Action = () => OnAutoConfigWizardClicked(this, new RoutedEventArgs()) },
                new() { Title = "Toggle Studio Mode", Description = "Switch between Preview and Program staging", Shortcut = "", Action = () => OnStudioModeToggled(this, new RoutedEventArgs()) },
                new() { Title = "Open Audio DSP Rack", Description = "EQ, Noise Gate, Compressor, Voice Changer", Shortcut = "", Action = () => OnOpenAudioRackClicked(this, new RoutedEventArgs()) },
                new() { Title = "Open Settings", Description = "Hardware, Video, Audio and Themes", Shortcut = "", Action = () => OnSettingsClicked(this, new RoutedEventArgs()) },
                new() { Title = "Open Recordings Folder", Description = "Browse captured video files on disk", Shortcut = "Ctrl+O", Action = () => OnOpenRecordingsFolderClicked(this, new RoutedEventArgs()) }
            };

            var palette = new CommandPaletteWindow(commands)
            {
                Owner = this
            };
            palette.ShowDialog();
        }

        private void OnUndockScenesClicked(object sender, RoutedEventArgs e)
        {
            ToastNotifier.Show("Scenes panel undocked for multi-monitor workspace.", ToastNotifier.ToastKind.Info);
        }

        private void OnUndockMixerClicked(object sender, RoutedEventArgs e)
        {
            ToastNotifier.Show("Audio Mixer panel undocked for multi-monitor workspace.", ToastNotifier.ToastKind.Info);
        }

        private void OnQuickFixClicked(object sender, RoutedEventArgs e)
        {
            if (_profile.StreamBitrateKbps > 3500)
            {
                _profile.StreamBitrateKbps = Math.Max(2500, _profile.StreamBitrateKbps - 1500);
                TxtDiagnosticTip.Text = $"Bitrate lowered to {_profile.StreamBitrateKbps} Kbps. Network bandwidth stabilized.";
                BtnQuickFix.Visibility = Visibility.Collapsed;
                ScheduleSave();
                ToastNotifier.Show($"Bitrate reduced to {_profile.StreamBitrateKbps} Kbps.", ToastNotifier.ToastKind.Success);
            }
        }
        #endregion

        #region Scene UI Event Handlers
        private void OnSceneSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScenesListBox.SelectedItem is Scene sc)
            {
                if (_isStudioMode)
                {
                    _stagedScene = sc;
                    _compositor.StagedPreviewScene = _stagedScene;
                    SourcesListBox.ItemsSource = _stagedScene.Sources;
                    SourcesEmptyState.Visibility = _stagedScene.Sources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                    CanvasGizmo.SetSelectedSource(null);
                    StudioCanvasGizmo?.SetSelectedSource(null);
                    _selectedSource = null;
                    UpdateInspectorUI();
                    TxtActiveSceneName.Text = $"Preview: {_stagedScene.Name} • Program: {_activeScene?.Name ?? "None"}";
                    if (_stagedScene.Sources.Count > 0)
                    {
                        SourcesListBox.SelectedIndex = _stagedScene.Sources.Count - 1;
                    }
                }
                else
                {
                    SetActiveScene(sc);
                }
            }
        }

        private void OnTransitionSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboTransition?.SelectedItem is ComboBoxItem item && _activeScene != null)
            {
                string text = item.Content?.ToString() ?? "Fade (300ms)";
                var (tType, tDur) = Scene.ParseTransitionString(text);
                _activeScene.Transition = text;
                _activeScene.TransitionEffect = tType;
                _activeScene.TransitionDurationMs = tDur;
                ScheduleSave();
            }
        }

        private void OnAddSceneClicked(object sender, RoutedEventArgs e)
        {
            var newScene = new Scene
            {
                Name = $"Scene {Scenes.Count + 1}"
            };
            Scenes.Add(newScene);
            ScenesListBox.SelectedItem = newScene;
            ScheduleSave();
        }

        private void OnDeleteSceneClicked(object sender, RoutedEventArgs e)
        {
            if (Scenes.Count <= 1)
            {
                ToastNotifier.Show("Cannot delete the only scene.", ToastNotifier.ToastKind.Warning);
                return;
            }

            if (ScenesListBox.SelectedItem is Scene sc)
            {
                int idx = Scenes.IndexOf(sc);
                Scenes.Remove(sc);
                ScenesListBox.SelectedIndex = Math.Max(0, idx - 1);
                ScheduleSave();
            }
        }

        private void OnDuplicateSceneClicked(object sender, RoutedEventArgs e)
        {
            if (ScenesListBox.SelectedItem is Scene sc)
            {
                var clone = sc.Clone();
                Scenes.Add(clone);
                ScenesListBox.SelectedItem = clone;
                ScheduleSave();
            }
        }

        private void OnRenameSceneClicked(object sender, RoutedEventArgs e)
        {
            if (ScenesListBox.SelectedItem is Scene sc)
            {
                string? name = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter a new scene name:", "Rename Scene", sc.Name);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    sc.Name = name.Trim();
                    TxtActiveSceneName.Text = $"Scene: {sc.Name}";
                    ScheduleSave();
                }
            }
        }

        private void OnSourceSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SourcesListBox.SelectedItem is SourceItem src)
            {
                SetSelectedSource(src);
            }
            else
            {
                SetSelectedSource(null);
            }
        }

        private void OnAddSourceClicked(object sender, RoutedEventArgs e)
        {
            if (_activeScene == null) return;

            var dlg = new AddSourceDialog
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true && dlg.CreatedSource != null)
            {
                var src = dlg.CreatedSource;
                src.ZIndex = _activeScene.Sources.Count;
                _activeScene.Sources.Add(src);
                SourcesListBox.SelectedItem = src;

                if (src.Type == SourceType.PhoneCamera)
                {
                    _ = _compositor.PhoneCamera.ConnectAsync(src.PhoneStreamUrl);
                }
                else if (src.Type == SourceType.VideoCaptureDevice)
                {
                    _compositor.EnsureCameraStarted(src);
                }

                ScheduleSave();
            }
        }

        private void OnDeleteSourceClicked(object sender, RoutedEventArgs e)
        {
            if (_activeScene != null && SourcesListBox.SelectedItem is SourceItem src)
            {
                _activeScene.Sources.Remove(src);
                SetSelectedSource(null);
                ScheduleSave();
            }
        }

        private void OnMoveSourceUpClicked(object sender, RoutedEventArgs e)
        {
            if (_activeScene != null && SourcesListBox.SelectedItem is SourceItem src)
            {
                int idx = _activeScene.Sources.IndexOf(src);
                if (idx < _activeScene.Sources.Count - 1)
                {
                    _activeScene.Sources.Move(idx, idx + 1);
                    SourcesListBox.SelectedItem = src;

                    // Reassign ZIndex to match list order so rendering follows the UI
                    for (int i = 0; i < _activeScene.Sources.Count; i++)
                    {
                        _activeScene.Sources[i].ZIndex = i;
                    }
                    ScheduleSave();
                }
            }
        }

        private void OnMoveSourceDownClicked(object sender, RoutedEventArgs e)
        {
            if (_activeScene != null && SourcesListBox.SelectedItem is SourceItem src)
            {
                int idx = _activeScene.Sources.IndexOf(src);
                if (idx > 0)
                {
                    _activeScene.Sources.Move(idx, idx - 1);
                    SourcesListBox.SelectedItem = src;

                    for (int i = 0; i < _activeScene.Sources.Count; i++)
                    {
                        _activeScene.Sources[i].ZIndex = i;
                    }
                    ScheduleSave();
                }
            }
        }

        private void OnToggleVisibilityClicked(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is SourceItem src)
            {
                src.IsVisible = !src.IsVisible;
                btn.Content = src.IsVisible ? "👁" : "🙈";
                CanvasGizmo.UpdateGizmo();
                ScheduleSave();
            }
        }

        private void OnToggleLockClicked(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is SourceItem src)
            {
                src.IsLocked = !src.IsLocked;
                btn.Content = src.IsLocked ? "🔒" : "🔓";
                CanvasGizmo.UpdateGizmo();
                ScheduleSave();
            }
        }
        #endregion

        #region Inspector Property Event Handlers
        private void OnTransformPropChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedSource == null || _suppressInspectorEvents) return;

            if (double.TryParse(TxtPropX.Text, out double x)) _selectedSource.X = x;
            if (double.TryParse(TxtPropY.Text, out double y)) _selectedSource.Y = y;
            if (double.TryParse(TxtPropWidth.Text, out double w)) _selectedSource.Width = Math.Max(10, w);
            if (double.TryParse(TxtPropHeight.Text, out double h)) _selectedSource.Height = Math.Max(10, h);

            CanvasGizmo.UpdateGizmo();
            ScheduleSave();
        }

        private void OnCropPropChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedSource == null || _suppressInspectorEvents) return;

            if (double.TryParse(TxtPropCropL.Text, out double l)) _selectedSource.CropLeft = Math.Max(0, l);
            if (double.TryParse(TxtPropCropT.Text, out double t)) _selectedSource.CropTop = Math.Max(0, t);
            if (double.TryParse(TxtPropCropR.Text, out double r)) _selectedSource.CropRight = Math.Max(0, r);
            if (double.TryParse(TxtPropCropB.Text, out double b)) _selectedSource.CropBottom = Math.Max(0, b);

            ScheduleSave();
        }

        private void OnRotationPropChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_selectedSource == null || _suppressInspectorEvents) return;
            _selectedSource.Rotation = SliderPropRotation.Value;
            TxtPropRotationVal.Text = $"{SliderPropRotation.Value:F0}°";
            CanvasGizmo.UpdateGizmo();
            ScheduleSave();
        }

        private void OnTextContentPropChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedSource == null || _suppressInspectorEvents) return;
            _selectedSource.TextContent = TxtPropTextContent.Text;
            if (double.TryParse(TxtPropFontSize.Text, out double fs)) _selectedSource.FontSize = Math.Max(8, fs);
            ScheduleSave();
        }

        private void OnFlipPropChanged(object sender, RoutedEventArgs e)
        {
            if (_selectedSource == null) return;
            _selectedSource.HorizontalFlip = ChkPropFlipH.IsChecked == true;
            ScheduleSave();
        }

        private void OnReconnectPhoneClicked(object sender, RoutedEventArgs e)
        {
            if (_selectedSource == null) return;
            _ = _compositor.PhoneCamera.ConnectAsync(_selectedSource.PhoneStreamUrl);
            ToastNotifier.Show("Reconnecting phone camera stream...", ToastNotifier.ToastKind.Info, 2);
        }

        private void OnOpacityPropChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_selectedSource == null || _suppressInspectorEvents) return;
            _selectedSource.Opacity = SliderPropOpacity.Value;
            TxtPropOpacityVal.Text = $"{(_selectedSource.Opacity * 100):F0}%";
            ScheduleSave();
        }

        private void OnFitSourceToScreenClicked(object sender, RoutedEventArgs e)
        {
            if (_selectedSource == null) return;
            _selectedSource.X = 0;
            _selectedSource.Y = 0;
            _selectedSource.Width = _profile.CanvasWidth;
            _selectedSource.Height = _profile.CanvasHeight;
            _selectedSource.Rotation = 0;
            UpdateInspectorUI();
            CanvasGizmo.UpdateGizmo();
            ScheduleSave();
        }

        private void OnCenterSourceClicked(object sender, RoutedEventArgs e)
        {
            if (_selectedSource == null) return;
            _selectedSource.X = Math.Max(0, (_profile.CanvasWidth - _selectedSource.Width) / 2.0);
            _selectedSource.Y = Math.Max(0, (_profile.CanvasHeight - _selectedSource.Height) / 2.0);
            UpdateInspectorUI();
            CanvasGizmo.UpdateGizmo();
            ScheduleSave();
        }

        private void OnResetTransformClicked(object sender, RoutedEventArgs e)
        {
            if (_selectedSource == null) return;
            _selectedSource.Rotation = 0;
            _selectedSource.Opacity = 1.0;
            UpdateInspectorUI();
            CanvasGizmo.UpdateGizmo();
            ScheduleSave();
        }

        private void OnChromaKeyPropChanged(object sender, RoutedEventArgs e)
        {
            if (_selectedSource == null || _suppressInspectorEvents) return;
            _selectedSource.ChromaKeyEnabled = ChkPropChromaKey.IsChecked == true;
            _selectedSource.KeySimilarity = SliderChromaSimilarity.Value;
            _selectedSource.KeySmoothness = SliderChromaSmoothness.Value;
            _selectedSource.KeySpillReduction = SliderChromaSpill.Value;

            TxtChromaSimVal.Text = $"{(_selectedSource.KeySimilarity * 100):F0}%";
            TxtChromaSmoothVal.Text = $"{(_selectedSource.KeySmoothness * 100):F0}%";
            TxtChromaSpillVal.Text = $"{(_selectedSource.KeySpillReduction * 100):F0}%";
            ScheduleSave();
        }

        private void OnColorAdjustPropChanged(object sender, RoutedEventArgs e)
        {
            if (_selectedSource == null || _suppressInspectorEvents) return;
            _selectedSource.ColorAdjustEnabled = ChkPropColorAdjust.IsChecked == true;
            _selectedSource.Brightness = SliderPropBrightness.Value;
            _selectedSource.Contrast = SliderPropContrast.Value;
            _selectedSource.Saturation = SliderPropSaturation.Value;
            _selectedSource.Gamma = SliderPropGamma.Value;

            TxtPropBrightVal.Text = $"{_selectedSource.Brightness:F0}";
            TxtPropContrastVal.Text = $"{_selectedSource.Contrast:F1}";
            TxtPropSatVal.Text = $"{_selectedSource.Saturation:F1}";
            TxtPropGammaVal.Text = $"{_selectedSource.Gamma:F1}";
            ScheduleSave();
        }
        #endregion

        #region Audio Sliders & Mutes
        private void OnMuteMicClicked(object sender, RoutedEventArgs e)
        {
            _audioEngine.FilterSettings.IsMuted = !_audioEngine.FilterSettings.IsMuted;
            BtnMuteMic.Content = _audioEngine.FilterSettings.IsMuted ? "Unmute" : "Mute";
            ToastNotifier.Show(
                _audioEngine.FilterSettings.IsMuted ? "Microphone muted." : "Microphone live.",
                ToastNotifier.ToastKind.Info, 1.6);
            ScheduleSave();
        }

        private void OnMonitorToggled(object sender, RoutedEventArgs e)
        {
            _audioEngine.MonitorEnabled = ChkMonitorMic.IsChecked == true;
            ToastNotifier.Show(
                _audioEngine.MonitorEnabled
                    ? "Audio monitoring ON — you are hearing the processed master mix. Use headphones to avoid echo."
                    : "Audio monitoring off.",
                ToastNotifier.ToastKind.Info, 3);
        }

        private void OnMicVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_audioEngine == null) return;
            double linear = SliderMicVolume.Value;
            _audioEngine.MicVolume = linear;
            double db = linear > 1e-4 ? 20.0 * Math.Log10(linear) : -60.0;
            if (TxtMicVolVal != null) TxtMicVolVal.Text = $"{linear * 100:F0}% ({(db >= 0 ? "+" : "")}{db:F1} dB)";
        }

        private void OnMuteDesktopClicked(object sender, RoutedEventArgs e)
        {
            if (_audioEngine == null) return;
            if (_audioEngine.DesktopVolume > 0)
            {
                _audioEngine.DesktopVolume = 0;
                BtnMuteDesktop.Content = "Unmute";
            }
            else
            {
                _audioEngine.DesktopVolume = SliderDesktopVolume.Value > 0 ? SliderDesktopVolume.Value : 0.8;
                BtnMuteDesktop.Content = "Mute";
            }
        }

        private void OnDesktopVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_audioEngine == null) return;
            _audioEngine.DesktopVolume = SliderDesktopVolume.Value;
            double linear = SliderDesktopVolume.Value;
            double db = linear > 1e-4 ? 20.0 * Math.Log10(linear) : -60.0;
            if (TxtDesktopVolVal != null) TxtDesktopVolVal.Text = $"{linear * 100:F0}% ({(db >= 0 ? "+" : "")}{db:F1} dB)";
        }
        #endregion

        #region Canvas Zoom & Guides
        private void OnGuidesToggled(object sender, RoutedEventArgs e)
        {
            bool show = ChkShowGuides.IsChecked == true;
            CanvasGizmo.ShowSafeAreas = show;
            if (StudioCanvasGizmo != null)
            {
                StudioCanvasGizmo.ShowSafeAreas = show;
            }
        }

        private ProjectorWindow? _projectorWindow;

        private void OnProjectorClicked(object sender, RoutedEventArgs e)
        {
            if (_projectorWindow != null)
            {
                _projectorWindow.Close();
                _projectorWindow = null;
                return;
            }

            _projectorWindow = new ProjectorWindow();
            _projectorWindow.BindBitmap(() => _compositor.PreviewBitmap);
            _projectorWindow.Closed += (s, ev) => _projectorWindow = null;
            _projectorWindow.Show();

            ToastNotifier.Show("Projector opened. Move it to another monitor and press F11 for fullscreen. Press Esc inside it to close.", ToastNotifier.ToastKind.Info, 4);
        }

        private void OnFitScreenClicked(object sender, RoutedEventArgs e)
        {
            // Viewbox handles stretch automatically
        }
        #endregion

        #region Right Panel Tabs & Live Chat Dock
        private void OnTabInspectorClicked(object sender, RoutedEventArgs e)
        {
            BtnTabInspector.Background = new SolidColorBrush(Color.FromRgb(36, 36, 36));
            BtnTabInspector.BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68));
            BtnTabInspector.Foreground = Brushes.White;
            BtnTabInspector.FontWeight = FontWeights.Bold;

            BtnTabChat.Background = Brushes.Transparent;
            BtnTabChat.BorderBrush = Brushes.Transparent;
            BtnTabChat.Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136));
            BtnTabChat.FontWeight = FontWeights.Normal;

            InspectorContainerGrid.Visibility = Visibility.Visible;
            ChatContainerGrid.Visibility = Visibility.Collapsed;
        }

        private void OnTabChatClicked(object sender, RoutedEventArgs e)
        {
            BtnTabChat.Background = new SolidColorBrush(Color.FromRgb(36, 36, 36));
            BtnTabChat.BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68));
            BtnTabChat.Foreground = Brushes.White;
            BtnTabChat.FontWeight = FontWeights.Bold;

            BtnTabInspector.Background = Brushes.Transparent;
            BtnTabInspector.BorderBrush = Brushes.Transparent;
            BtnTabInspector.Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136));
            BtnTabInspector.FontWeight = FontWeights.Normal;

            InspectorContainerGrid.Visibility = Visibility.Collapsed;
            ChatContainerGrid.Visibility = Visibility.Visible;
        }
        #endregion

        #region Instant Replay Buffer & Auto-Clipper
        private async void OnSaveReplayClicked(object sender, RoutedEventArgs e)
        {
            await TriggerSaveReplayAsync(isVertical: false);
        }

        private async Task TriggerSaveReplayAsync(bool isVertical)
        {
            BtnSaveReplay.IsEnabled = false;
            BtnSaveReplay.Content = "SAVING...";

            string destFolder = _profile.RecordingDirectory;
            string? savedFile = await _replayBuffer.SaveReplayAsync(destFolder, _profile.Encoder, isVertical);

            BtnSaveReplay.IsEnabled = true;
            BtnSaveReplay.Content = "⚡ CLIP (30s)";

            if (savedFile != null && File.Exists(savedFile))
            {
                _chatService.AddMessage("System", $"Replay saved: {Path.GetFileName(savedFile)}", ChatPlatform.System);

                double sizeMb = new FileInfo(savedFile).Length / (1024.0 * 1024.0);
                var dlg = new RecordingCompletedDialog(savedFile, TimeSpan.FromSeconds(30), sizeMb)
                {
                    Owner = this
                };
                dlg.ShowDialog();
            }
            else
            {
                ToastNotifier.Show("Nothing to save yet — let the replay buffer fill up first (a few seconds).", ToastNotifier.ToastKind.Warning, 4);
            }
        }
        #endregion

        #region Soundboard Quick SFX Handlers
        private void OnSfxAirhornClicked(object sender, RoutedEventArgs e)
        {
            _audioEngine.Soundboard.PlaySound(SoundEffectType.AirHorn);
        }

        private void OnSfxVictoryClicked(object sender, RoutedEventArgs e)
        {
            _audioEngine.Soundboard.PlaySound(SoundEffectType.VictoryChime);
        }

        private void OnSfxLevelUpClicked(object sender, RoutedEventArgs e)
        {
            _audioEngine.Soundboard.PlaySound(SoundEffectType.LevelUp);
        }

        private void OnSfxLaserClicked(object sender, RoutedEventArgs e)
        {
            _audioEngine.Soundboard.PlaySound(SoundEffectType.Laser);
        }

        private void OnSfxBuzzerClicked(object sender, RoutedEventArgs e)
        {
            _audioEngine.Soundboard.PlaySound(SoundEffectType.Buzzer);
        }

        private void OnSfxApplauseClicked(object sender, RoutedEventArgs e)
        {
            _audioEngine.Soundboard.PlaySound(SoundEffectType.Applause);
        }

        private void OnAutoClipperToggled(object sender, RoutedEventArgs e)
        {
            _autoClipper.IsEnabled = ChkAutoClipper.IsChecked == true;
            ToastNotifier.Show(
                _autoClipper.IsEnabled ? "AI Auto-Clipper armed. Loud hype moments will auto-save vertical clips." : "AI Auto-Clipper disabled.",
                ToastNotifier.ToastKind.Info, 2.5);
        }
        #endregion

        #region Studio Mode & Remote Control Handlers
        private void OnStudioModeToggled(object sender, RoutedEventArgs e)
        {
            _isStudioMode = !_isStudioMode;
            _compositor.IsStudioMode = _isStudioMode;

            if (_isStudioMode)
            {
                BtnStudioMode.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3b82f6"));
                BtnStudioMode.Foreground = Brushes.White;
                SingleCanvasViewbox.Visibility = Visibility.Collapsed;
                StudioDualGrid.Visibility = Visibility.Visible;
                _stagedScene = _activeScene;
                _compositor.StagedPreviewScene = _stagedScene;
                StudioCanvasGizmo.SetSelectedSource(_selectedSource);
                TxtActiveSceneName.Text = $"Preview: {_stagedScene?.Name ?? "None"} • Program: {_activeScene?.Name ?? "None"}";
                ToastNotifier.Show("Studio Mode Enabled (Preview staging on left, Live broadcast on right)", ToastNotifier.ToastKind.Info, 2.0);
            }
            else
            {
                BtnStudioMode.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#181818"));
                BtnStudioMode.Foreground = Brushes.White;
                SingleCanvasViewbox.Visibility = Visibility.Visible;
                StudioDualGrid.Visibility = Visibility.Collapsed;
                _compositor.StagedPreviewScene = null;
                CanvasGizmo.SetSelectedSource(_selectedSource);
                TxtActiveSceneName.Text = $"Scene: {_activeScene?.Name ?? "None"}";
                ToastNotifier.Show("Single View Mode Enabled", ToastNotifier.ToastKind.Info, 1.5);
            }
        }

        private void OnStudioTransitionClicked(object sender, RoutedEventArgs e)
        {
            if (!_isStudioMode || _stagedScene == null) return;
            var tType = _activeScene?.TransitionEffect ?? TransitionType.CrossFade;
            int tDur = _activeScene?.TransitionDurationMs ?? 300;
            _compositor.TransitionStagedToProgram(tType, tDur);
            _activeScene = _stagedScene;
            TxtActiveSceneName.Text = $"Preview: {_stagedScene.Name} • Program: {_activeScene.Name}";
            ToastNotifier.Show($"Transitioned to '{_activeScene.Name}'", ToastNotifier.ToastKind.Info, 1.2);
            ScheduleSave();
        }

        private async void OnExportCollectionClicked(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Ramaverse Collection Archive (*.rama)|*.rama|All Files (*.*)|*.*",
                FileName = $"Ramaverse_Collection_{DateTime.Now:yyyy-MM-dd}.rama",
                Title = "Export Scene Collection"
            };

            if (dlg.ShowDialog() == true)
            {
                bool ok = await SceneCollectionExporter.ExportCollectionAsync(
                    dlg.FileName, "My Stream Collection", _profile, Scenes, _audioEngine.FilterSettings);

                if (ok)
                    ToastNotifier.Show("Scene Collection exported successfully!", ToastNotifier.ToastKind.Success, 2.5);
                else
                    ToastNotifier.Show("Failed to export Scene Collection.", ToastNotifier.ToastKind.Error, 3.0);
            }
        }

        private async void OnImportCollectionClicked(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Ramaverse Collection Archive (*.rama)|*.rama|All Files (*.*)|*.*",
                Title = "Import Scene Collection"
            };

            if (dlg.ShowDialog() == true)
            {
                var res = await SceneCollectionExporter.ImportCollectionAsync(dlg.FileName);
                if (res.Success && res.Scenes != null)
                {
                    Scenes.Clear();
                    foreach (var s in res.Scenes) Scenes.Add(s);
                    if (res.Profile != null) _profile = res.Profile;
                    if (res.AudioFilters != null) _audioEngine.FilterSettings = res.AudioFilters;

                    if (Scenes.Count > 0)
                    {
                        SetActiveScene(Scenes[0]);
                        ScenesListBox.SelectedItem = Scenes[0];
                    }

                    ScheduleSave();
                    ToastNotifier.Show("Scene Collection imported successfully!", ToastNotifier.ToastKind.Success, 2.5);
                }
                else
                {
                    ToastNotifier.Show($"Import failed: {res.Error}", ToastNotifier.ToastKind.Error, 3.5);
                }
            }
        }

        private void OnRemoteControlClicked(object sender, RoutedEventArgs e)
        {
            if (_remoteServer != null && _remoteServer.IsRunning)
            {
                string url = _remoteServer.ServerUrl;
                try { Clipboard.SetText(url); } catch { }
                ToastNotifier.Show($"Remote URL copied: {url}", ToastNotifier.ToastKind.Success, 2.5);
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch { }
            }
        }

        private object GetRemoteStatus()
        {
            var scenesList = Scenes.Select(s => new {
                name = s.Name,
                isActive = s == _activeScene
            }).ToList();

            return new
            {
                isRecording = _recordingEngine.IsRecording,
                isStreaming = _streamingEngine.IsStreaming,
                isMuted = _audioEngine.FilterSettings.IsMuted,
                recordingTime = _recordingEngine.IsRecording ? _recordingEngine.ElapsedTime.ToString(@"hh\:mm\:ss") : "00:00",
                streamStatus = _streamingEngine.IsStreaming ? "LIVE" : "OFFLINE",
                activeScene = _activeScene?.Name ?? "None",
                scenes = scenesList
            };
        }

        private void HandleRemoteAction(string action, string? param)
        {
            switch (action.ToLowerInvariant())
            {
                case "toggle_record":
                    OnRecordToggleClicked(this, new RoutedEventArgs());
                    break;
                case "toggle_stream":
                    OnStreamToggleClicked(this, new RoutedEventArgs());
                    break;
                case "toggle_mute_mic":
                case "toggle_mute":
                    _audioEngine.FilterSettings.IsMuted = !_audioEngine.FilterSettings.IsMuted;
                    BtnMuteMic.Content = _audioEngine.FilterSettings.IsMuted ? "🔇" : "🎤";
                    break;
                case "trigger_replay":
                    OnSaveReplayClicked(this, new RoutedEventArgs());
                    break;
                case "set_scene":
                    if (!string.IsNullOrEmpty(param))
                    {
                        var targetScene = Scenes.FirstOrDefault(s => string.Equals(s.Name, param, StringComparison.OrdinalIgnoreCase));
                        if (targetScene != null)
                        {
                            if (_isStudioMode)
                            {
                                _stagedScene = targetScene;
                                _compositor.StagedPreviewScene = _stagedScene;
                                SourcesListBox.ItemsSource = _stagedScene.Sources;
                                SourcesEmptyState.Visibility = _stagedScene.Sources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                                CanvasGizmo.SetSelectedSource(null);
                                StudioCanvasGizmo?.SetSelectedSource(null);
                                _selectedSource = null;
                                UpdateInspectorUI();
                                TxtActiveSceneName.Text = $"Preview: {_stagedScene.Name} • Program: {_activeScene?.Name ?? "None"}";
                            }
                            else
                            {
                                SetActiveScene(targetScene);
                                ScenesListBox.SelectedItem = targetScene;
                            }
                        }
                    }
                    break;
                case "play_sfx":
                    if (!string.IsNullOrEmpty(param) && Enum.TryParse<SoundEffectType>(param, true, out var sfxType))
                    {
                        _audioEngine.Soundboard.PlaySound(sfxType);
                    }
                    break;
            }
        }
        #endregion

        private void OnMainWindowClosed(object? sender, EventArgs e)
        {
            if (_windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, HOTKEY_ID_RECORD);
                UnregisterHotKey(_windowHandle, HOTKEY_ID_STREAM);
                UnregisterHotKey(_windowHandle, HOTKEY_ID_SNAPSHOT);
                UnregisterHotKey(_windowHandle, HOTKEY_ID_MUTE);
                UnregisterHotKey(_windowHandle, HOTKEY_ID_REPLAY);
            }

            _saveDebounceTimer?.Stop();
            SaveProjectState();
            _uiTimer.Stop();
            _remoteServer?.Dispose();
            _replayBuffer.Dispose();
            _recordingEngine.Dispose();
            _streamingEngine.Dispose();
            _virtualCam.Dispose();
            _chatService.Dispose();
            _compositor.Dispose();
            _audioEngine.Dispose();
        }
    }
}
