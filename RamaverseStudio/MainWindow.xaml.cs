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
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RamaverseStudio.Audio;
using RamaverseStudio.AutoUpdate;
using RamaverseStudio.Models;
using RamaverseStudio.Output;
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
        private UpdateManager _updateManager;

        // Scenes
        public ObservableCollection<Scene> Scenes { get; set; } = new ObservableCollection<Scene>();
        private Scene? _activeScene;
        private SourceItem? _selectedSource;

        // UI Refresh Timer (Audio VU meters, CPU/RAM, Recording Stats)
        private DispatcherTimer _uiTimer;
        private Process _currentProcess = Process.GetCurrentProcess();
        private DateTime _lastCpuCheck = DateTime.UtcNow;
        private TimeSpan _lastCpuTime = TimeSpan.Zero;
        private double _cpuUsagePercent = 0.0;

        public MainWindow()
        {
            InitializeComponent();

            // 1. Initialize Engines & Updater
            _updateManager = new UpdateManager();
            _audioEngine = new AudioEngine();
            _compositor = new CompositorEngine(Dispatcher, _profile.CanvasWidth, _profile.CanvasHeight, _profile.Fps);
            _recordingEngine = new FFmpegRecordingEngine();
            _streamingEngine = new FFmpegStreamingEngine();

            // 2. Bind Canvas Preview Bitmap
            CanvasLiveImage.Source = _compositor.PreviewBitmap;
            CanvasGizmo.SetCanvasResolution(_profile.CanvasWidth, _profile.CanvasHeight);

            // 3. Connect Video & Audio Pipes
            _compositor.FrameComposited += OnFrameComposited;
            _audioEngine.AudioSamplesProcessed += OnAudioSamplesProcessed;
            _recordingEngine.StatsUpdated += OnRecordingStatsUpdated;
            _streamingEngine.StatsUpdated += OnStreamingStatsUpdated;
            CanvasGizmo.TransformModified += OnGizmoTransformModified;

            // 4. Load Saved Project State or Setup Default Scenes
            LoadSavedProjectOrCreateDefaults();

            // 5. Start Audio & Video Compositor
            _audioEngine.Start();
            _compositor.Start();

            // 6. Setup Telemetry UI Timer
            _uiTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
            };
            _uiTimer.Tick += OnUiTimerTick;
            _uiTimer.Start();

            // 7. Auto-Check for Updates in Background
            _ = CheckForUpdatesOnLaunchAsync();

            // 8. Window Lifetime
            Closed += OnMainWindowClosed;
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

                ScenesListBox.ItemsSource = Scenes;
                int activeIdx = Math.Clamp(saved.ActiveSceneIndex, 0, Scenes.Count - 1);
                ScenesListBox.SelectedIndex = activeIdx;
                SetActiveScene(Scenes[activeIdx]);
            }
            else
            {
                SetupDefaultProductionScenes();
            }

            UpdateFormatButtonStates();
        }

        private void SaveProjectState()
        {
            int activeIdx = _activeScene != null ? Scenes.IndexOf(_activeScene) : 0;
            ProjectStorage.SaveProject(_profile, Scenes, _audioEngine.FilterSettings, activeIdx);
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
            SetActiveScene(mainScene);
        }
        #endregion

        #region Keyboard Shortcuts / Hotkeys
        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            // Do not intercept hotkeys if user is currently typing in a text box
            if (e.OriginalSource is TextBox) return;

            bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

            if (isCtrl)
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

                    // Scene selection (Ctrl+1 to Ctrl+9)
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
            else
            {
                if (_selectedSource != null && !_selectedSource.IsLocked)
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
                        case Key.Delete:
                            OnDeleteSourceClicked(this, new RoutedEventArgs());
                            e.Handled = true;
                            break;
                    }
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
        private void SetActiveScene(Scene scene)
        {
            _activeScene = scene;
            _compositor.CurrentScene = scene;
            SourcesListBox.ItemsSource = scene.Sources;

            TxtActiveSceneName.Text = $"Scene: {scene.Name}";

            if (scene.Sources.Count > 0)
            {
                SourcesListBox.SelectedIndex = scene.Sources.Count - 1;
            }
            else
            {
                SetSelectedSource(null);
            }

            SaveProjectState();
        }

        private void SetSelectedSource(SourceItem? source)
        {
            if (_selectedSource != null)
            {
                _selectedSource.IsSelected = false;
            }

            _selectedSource = source;
            CanvasGizmo.SetSelectedSource(source);

            if (source != null)
            {
                source.IsSelected = true;
                TxtInspectorSourceName.Text = source.Name;
                UpdateInspectorUI();
                InspectorPanel.IsEnabled = true;
            }
            else
            {
                TxtInspectorSourceName.Text = "No Source Selected";
                InspectorPanel.IsEnabled = false;
            }
        }

        private void UpdateInspectorUI()
        {
            if (_selectedSource == null) return;

            TxtPropX.Text = $"{_selectedSource.X:F0}";
            TxtPropY.Text = $"{_selectedSource.Y:F0}";
            TxtPropWidth.Text = $"{_selectedSource.Width:F0}";
            TxtPropHeight.Text = $"{_selectedSource.Height:F0}";

            SliderPropOpacity.Value = _selectedSource.Opacity;
            TxtPropOpacityVal.Text = $"{(_selectedSource.Opacity * 100):F0}%";

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
        }

        private void OnGizmoTransformModified()
        {
            UpdateInspectorUI();
            SaveProjectState();
        }
        #endregion

        #region Engine Frame & Audio Callbacks
        private void OnFrameComposited(byte[] bgraPixels, int width, int height, int stride)
        {
            if (_recordingEngine.IsRecording)
            {
                _recordingEngine.WriteVideoFrame(bgraPixels);
            }

            if (_streamingEngine.IsStreaming)
            {
                _streamingEngine.WriteVideoFrame(bgraPixels);
            }
        }

        private void OnAudioSamplesProcessed(byte[] pcm16Bytes, int bytesRead)
        {
            if (_recordingEngine.IsRecording)
            {
                _recordingEngine.WriteAudioSamples(pcm16Bytes, bytesRead);
            }

            if (_streamingEngine.IsStreaming)
            {
                _streamingEngine.WriteAudioSamples(pcm16Bytes, bytesRead);
            }
        }

        private void OnRecordingStatsUpdated(RecordingStats stats)
        {
            Dispatcher.InvokeAsync(() =>
            {
                TxtRecDuration.Text = stats.ElapsedTime.ToString(@"hh\:mm\:ss");
                TxtRecSize.Text = $"Size: {stats.FileSizeMb:F1} MB • {_profile.RecFormat.ToString().ToUpper()}";
                TxtRecStatus.Text = stats.IsPaused ? "PAUSED" : "RECORDING";
            });
        }

        private void OnStreamingStatsUpdated(StreamStats stats)
        {
            Dispatcher.InvokeAsync(() =>
            {
                TxtStreamUptime.Text = stats.Uptime.ToString(@"hh\:mm\:ss");
                TxtStreamBitrate.Text = $"Bitrate: {stats.BitrateKbps:F0} kbps • {stats.DroppedFrames} drops";
                TxtStreamStatus.Text = stats.Status.ToString().ToUpper();
            });
        }
        #endregion

        #region Telemetry & Metering Timer
        private void OnUiTimerTick(object? sender, EventArgs e)
        {
            // Update Mic VU Meter
            double micPeakDb = _audioEngine.CurrentPeakDb;
            double micRmsDb = _audioEngine.CurrentRmsDb;
            MeterMic.SetLevel(micPeakDb, _audioEngine.PeakHoldDb);
            TxtMicLevelDb.Text = double.IsNegativeInfinity(micPeakDb) ? "-60.0 dB" : $"{micPeakDb:F1} dB";

            // Update Desktop Audio VU Meter
            double desktopPeakDb = _audioEngine.DesktopPeakDb;
            MeterDesktop.SetLevel(desktopPeakDb, desktopPeakDb);
            TxtDesktopLevelDb.Text = double.IsNegativeInfinity(desktopPeakDb) ? "-60.0 dB" : $"{desktopPeakDb:F1} dB";

            // Update Voice Changer preset badge
            TxtVoicePresetLabel.Text = _audioEngine.FilterSettings.VoiceChangerEnabled ? _audioEngine.FilterSettings.VoiceChangerPreset.ToString() : "Clean";

            // Update Performance Telemetry (FPS, Drops, CPU, RAM)
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
            }

            TxtStatusCpu.Text = $"CPU: {Math.Clamp(_cpuUsagePercent, 0, 100):F1}%";
            TxtStatusRam.Text = $"RAM: {_currentProcess.WorkingSet64 / (1024 * 1024)} MB";
            TxtStatusEncoder.Text = $"Encoder: {_profile.Encoder}";
        }
        #endregion

        #region Format Switchers
        private void OnFormat16x9Clicked(object sender, RoutedEventArgs e)
        {
            _profile.CanvasFormat = CanvasFormat.Horizontal16x9;
            _profile.CanvasWidth = 1920;
            _profile.CanvasHeight = 1080;
            ApplyCanvasResolutionChange();
        }

        private void OnFormat9x16Clicked(object sender, RoutedEventArgs e)
        {
            _profile.CanvasFormat = CanvasFormat.Vertical9x16;
            _profile.CanvasWidth = 1080;
            _profile.CanvasHeight = 1920;
            ApplyCanvasResolutionChange();
        }

        private void OnFormatSquareClicked(object sender, RoutedEventArgs e)
        {
            _profile.CanvasFormat = CanvasFormat.Square1x1;
            _profile.CanvasWidth = 1080;
            _profile.CanvasHeight = 1080;
            ApplyCanvasResolutionChange();
        }

        private void OnFormatDualClicked(object sender, RoutedEventArgs e)
        {
            // Dual Canvas Landscape + Vertical
            _profile.CanvasFormat = CanvasFormat.Custom;
            _profile.CanvasWidth = 3000;
            _profile.CanvasHeight = 1920;
            ApplyCanvasResolutionChange();
        }

        private void ApplyCanvasResolutionChange()
        {
            _compositor.SetCanvasResolution(_profile.CanvasWidth, _profile.CanvasHeight);
            CanvasLiveImage.Source = _compositor.PreviewBitmap;
            CanvasContainerGrid.Width = _profile.CanvasWidth;
            CanvasContainerGrid.Height = _profile.CanvasHeight;
            CanvasGizmo.SetCanvasResolution(_profile.CanvasWidth, _profile.CanvasHeight);

            TxtCanvasResBadge.Text = $"{_profile.CanvasWidth} x {_profile.CanvasHeight} • {_profile.Fps} FPS";
            TxtActiveProfile.Text = $"{_profile.CanvasWidth}x{_profile.CanvasHeight} • {_profile.Fps} FPS • {_profile.CanvasFormat}";

            UpdateFormatButtonStates();
            SaveProjectState();
        }

        private void UpdateFormatButtonStates()
        {
            BtnFormat16x9.Background = _profile.CanvasFormat == CanvasFormat.Horizontal16x9 ? Brushes.White : Brushes.Transparent;
            BtnFormat16x9.Foreground = _profile.CanvasFormat == CanvasFormat.Horizontal16x9 ? Brushes.Black : new SolidColorBrush(Color.FromRgb(136, 136, 136));
            BtnFormat16x9.FontWeight = _profile.CanvasFormat == CanvasFormat.Horizontal16x9 ? FontWeights.Bold : FontWeights.Normal;

            BtnFormat9x16.Background = _profile.CanvasFormat == CanvasFormat.Vertical9x16 ? Brushes.White : Brushes.Transparent;
            BtnFormat9x16.Foreground = _profile.CanvasFormat == CanvasFormat.Vertical9x16 ? Brushes.Black : new SolidColorBrush(Color.FromRgb(136, 136, 136));
            BtnFormat9x16.FontWeight = _profile.CanvasFormat == CanvasFormat.Vertical9x16 ? FontWeights.Bold : FontWeights.Normal;

            BtnFormatSquare.Background = _profile.CanvasFormat == CanvasFormat.Square1x1 ? Brushes.White : Brushes.Transparent;
            BtnFormatSquare.Foreground = _profile.CanvasFormat == CanvasFormat.Square1x1 ? Brushes.Black : new SolidColorBrush(Color.FromRgb(136, 136, 136));
            BtnFormatSquare.FontWeight = _profile.CanvasFormat == CanvasFormat.Square1x1 ? FontWeights.Bold : FontWeights.Normal;

            BtnFormatDual.Background = _profile.CanvasFormat == CanvasFormat.Custom ? Brushes.White : Brushes.Transparent;
            BtnFormatDual.Foreground = _profile.CanvasFormat == CanvasFormat.Custom ? Brushes.Black : new SolidColorBrush(Color.FromRgb(136, 136, 136));
            BtnFormatDual.FontWeight = _profile.CanvasFormat == CanvasFormat.Custom ? FontWeights.Bold : FontWeights.Normal;
        }
        #endregion

        #region Production Control Handlers (Record, Stream, Snapshot)
        private async void OnRecordToggleClicked(object sender, RoutedEventArgs e)
        {
            if (!_recordingEngine.IsRecording)
            {
                // Start Recording
                bool success = await _recordingEngine.StartRecordingAsync(_profile);
                if (success)
                {
                    BtnRecord.Content = "■ STOP REC";
                    BtnRecord.Background = Brushes.White;
                    BtnRecord.Foreground = Brushes.Black;
                    BtnRecord.BorderBrush = Brushes.White;
                    BtnPauseRec.Visibility = Visibility.Visible;
                    TxtRecStatus.Text = "RECORDING";
                }
                else
                {
                    MessageBox.Show("Failed to start FFmpeg recording engine. Please verify your encoder settings.", "Recording Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // Stop Recording
                string recordedFile = _recordingEngine.CurrentOutputFilePath;
                _recordingEngine.StopRecording();

                BtnRecord.Content = "● RECORD";
                BtnRecord.Background = new SolidColorBrush(Color.FromRgb(24, 24, 24));
                BtnRecord.Foreground = Brushes.White;
                BtnRecord.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 56, 56));
                BtnPauseRec.Visibility = Visibility.Collapsed;
                TxtRecStatus.Text = "STANDBY";

                if (File.Exists(recordedFile))
                {
                    var dlg = new RecordingCompletedDialog(_recordingEngine, TimeSpan.Zero, 0)
                    {
                        Owner = this
                    };
                    dlg.ShowDialog();
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
                }
                else
                {
                    _recordingEngine.PauseRecording();
                    BtnPauseRec.Content = "Resume";
                }
            }
        }

        private async void OnStreamToggleClicked(object sender, RoutedEventArgs e)
        {
            if (!_streamingEngine.IsStreaming)
            {
                bool success = await _streamingEngine.StartStreamingAsync(_profile);
                if (success)
                {
                    BtnStream.Content = "■ END STREAM";
                    BtnStream.Background = Brushes.White;
                    BtnStream.Foreground = Brushes.Black;
                    BtnStream.BorderBrush = Brushes.White;
                    TxtStreamStatus.Text = "LIVE";
                }
                else
                {
                    MessageBox.Show("Failed to connect to RTMP streaming server. Please check your stream key & URL in Settings.", "Streaming Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                _streamingEngine.StopStreaming();
                BtnStream.Content = "📡 GO LIVE";
                BtnStream.Background = new SolidColorBrush(Color.FromRgb(24, 24, 24));
                BtnStream.Foreground = Brushes.White;
                BtnStream.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 56, 56));
                TxtStreamStatus.Text = "OFFLINE";
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

                MessageBox.Show($"Snapshot saved successfully:\n{path}", "Snapshot Captured", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save snapshot: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                ApplyCanvasResolutionChange();
            }
        }

        private void OnOpenAudioRackClicked(object sender, RoutedEventArgs e)
        {
            var dlg = new AudioFiltersDialog(_audioEngine)
            {
                Owner = this
            };
            dlg.ShowDialog();
            SaveProjectState();
        }
        #endregion

        #region Scene UI Event Handlers
        private void OnSceneSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScenesListBox.SelectedItem is Scene sc)
            {
                SetActiveScene(sc);
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
            SaveProjectState();
        }

        private void OnDeleteSceneClicked(object sender, RoutedEventArgs e)
        {
            if (Scenes.Count <= 1)
            {
                MessageBox.Show("Cannot delete the only scene.", "Ramaverse Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (ScenesListBox.SelectedItem is Scene sc)
            {
                int idx = Scenes.IndexOf(sc);
                Scenes.Remove(sc);
                ScenesListBox.SelectedIndex = Math.Max(0, idx - 1);
                SaveProjectState();
            }
        }

        private void OnDuplicateSceneClicked(object sender, RoutedEventArgs e)
        {
            if (ScenesListBox.SelectedItem is Scene sc)
            {
                var clone = sc.Clone();
                Scenes.Add(clone);
                ScenesListBox.SelectedItem = clone;
                SaveProjectState();
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

                SaveProjectState();
            }
        }

        private void OnDeleteSourceClicked(object sender, RoutedEventArgs e)
        {
            if (_activeScene != null && SourcesListBox.SelectedItem is SourceItem src)
            {
                _activeScene.Sources.Remove(src);
                SetSelectedSource(null);
                SaveProjectState();
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
                    SaveProjectState();
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
                    SaveProjectState();
                }
            }
        }

        private void OnToggleVisibilityClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SourceItem src)
            {
                src.IsVisible = !src.IsVisible;
                btn.Content = src.IsVisible ? "👁" : "🙈";
                CanvasGizmo.UpdateGizmo();
                SaveProjectState();
            }
        }

        private void OnToggleLockClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SourceItem src)
            {
                src.IsLocked = !src.IsLocked;
                btn.Content = src.IsLocked ? "🔒" : "🔓";
                CanvasGizmo.UpdateGizmo();
                SaveProjectState();
            }
        }
        #endregion

        #region Inspector Property Event Handlers
        private void OnTransformPropChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedSource == null) return;

            if (double.TryParse(TxtPropX.Text, out double x)) _selectedSource.X = x;
            if (double.TryParse(TxtPropY.Text, out double y)) _selectedSource.Y = y;
            if (double.TryParse(TxtPropWidth.Text, out double w)) _selectedSource.Width = Math.Max(10, w);
            if (double.TryParse(TxtPropHeight.Text, out double h)) _selectedSource.Height = Math.Max(10, h);

            CanvasGizmo.UpdateGizmo();
            SaveProjectState();
        }

        private void OnOpacityPropChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_selectedSource == null) return;
            _selectedSource.Opacity = SliderPropOpacity.Value;
            TxtPropOpacityVal.Text = $"{(_selectedSource.Opacity * 100):F0}%";
            SaveProjectState();
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
            SaveProjectState();
        }

        private void OnCenterSourceClicked(object sender, RoutedEventArgs e)
        {
            if (_selectedSource == null) return;
            _selectedSource.X = Math.Max(0, (_profile.CanvasWidth - _selectedSource.Width) / 2.0);
            _selectedSource.Y = Math.Max(0, (_profile.CanvasHeight - _selectedSource.Height) / 2.0);
            UpdateInspectorUI();
            CanvasGizmo.UpdateGizmo();
            SaveProjectState();
        }

        private void OnResetTransformClicked(object sender, RoutedEventArgs e)
        {
            if (_selectedSource == null) return;
            _selectedSource.Rotation = 0;
            _selectedSource.Opacity = 1.0;
            UpdateInspectorUI();
            CanvasGizmo.UpdateGizmo();
            SaveProjectState();
        }

        private void OnChromaKeyPropChanged(object sender, RoutedEventArgs e)
        {
            if (_selectedSource == null) return;
            _selectedSource.ChromaKeyEnabled = ChkPropChromaKey.IsChecked == true;
            _selectedSource.KeySimilarity = SliderChromaSimilarity.Value;
            _selectedSource.KeySmoothness = SliderChromaSmoothness.Value;
            _selectedSource.KeySpillReduction = SliderChromaSpill.Value;

            TxtChromaSimVal.Text = $"{(_selectedSource.KeySimilarity * 100):F0}%";
            TxtChromaSmoothVal.Text = $"{(_selectedSource.KeySmoothness * 100):F0}%";
            TxtChromaSpillVal.Text = $"{(_selectedSource.KeySpillReduction * 100):F0}%";
            SaveProjectState();
        }

        private void OnColorAdjustPropChanged(object sender, RoutedEventArgs e)
        {
            if (_selectedSource == null) return;
            _selectedSource.ColorAdjustEnabled = ChkPropColorAdjust.IsChecked == true;
            _selectedSource.Brightness = SliderPropBrightness.Value;
            _selectedSource.Contrast = SliderPropContrast.Value;
            _selectedSource.Saturation = SliderPropSaturation.Value;
            _selectedSource.Gamma = SliderPropGamma.Value;

            TxtPropBrightVal.Text = $"{_selectedSource.Brightness:F0}";
            TxtPropContrastVal.Text = $"{_selectedSource.Contrast:F1}";
            TxtPropSatVal.Text = $"{_selectedSource.Saturation:F1}";
            TxtPropGammaVal.Text = $"{_selectedSource.Gamma:F1}";
            SaveProjectState();
        }
        #endregion

        #region Audio Sliders & Mutes
        private void OnMuteMicClicked(object sender, RoutedEventArgs e)
        {
            _audioEngine.FilterSettings.IsMuted = !_audioEngine.FilterSettings.IsMuted;
            BtnMuteMic.Content = _audioEngine.FilterSettings.IsMuted ? "Unmute" : "Mute";
            SaveProjectState();
        }

        private void OnMicVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_audioEngine == null) return;
            double linear = SliderMicVolume.Value;
            _audioEngine.FilterSettings.InputGainDb = linear > 1e-4 ? 20.0 * Math.Log10(linear) : -60.0;
            if (TxtMicVolVal != null) TxtMicVolVal.Text = $"{(linear * 100):F0}%";
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
                _audioEngine.DesktopVolume = SliderDesktopVolume.Value;
                BtnMuteDesktop.Content = "Mute";
            }
        }

        private void OnDesktopVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_audioEngine == null) return;
            _audioEngine.DesktopVolume = SliderDesktopVolume.Value;
            if (TxtDesktopVolVal != null) TxtDesktopVolVal.Text = $"{(SliderDesktopVolume.Value * 100):F0}%";
        }
        #endregion

        #region Canvas Zoom & Guides
        private void OnGuidesToggled(object sender, RoutedEventArgs e)
        {
            CanvasGizmo.GuidesGrid.Visibility = ChkShowGuides.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnFitScreenClicked(object sender, RoutedEventArgs e)
        {
            // Viewbox handles stretch automatically
        }
        #endregion

        private void OnMainWindowClosed(object? sender, EventArgs e)
        {
            SaveProjectState();
            _uiTimer.Stop();
            _recordingEngine.Dispose();
            _streamingEngine.Dispose();
            _compositor.Dispose();
            _audioEngine.Dispose();
        }
    }
}