using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using RamaverseStudio.Models;
using RamaverseStudio.Video;

namespace RamaverseStudio.UI
{
    public partial class AddSourceDialog : Window
    {
        public SourceItem? CreatedSource { get; private set; }
        private Color _selectedSolidColor = Color.FromRgb(18, 18, 18);

        private List<ScreenInfo> _displays = new List<ScreenInfo>();
        private List<WindowInfo> _windows = new List<WindowInfo>();
        private List<CameraDeviceInfo> _cameras = new List<CameraDeviceInfo>();

        public AddSourceDialog()
        {
            InitializeComponent();
            LoadDevices();
            BuildColorPresets();
        }

        private void LoadDevices()
        {
            // Displays
            _displays = ScreenCaptureHelper.GetDisplays();
            ComboDisplays.Items.Clear();
            foreach (var d in _displays)
            {
                ComboDisplays.Items.Add(d.Name);
            }
            if (ComboDisplays.Items.Count > 0) ComboDisplays.SelectedIndex = 0;

            // Windows
            RefreshWindowsList();

            // Cameras
            _cameras = CameraCaptureHelper.GetAvailableCameras();
            ComboCameras.Items.Clear();
            foreach (var c in _cameras)
            {
                ComboCameras.Items.Add(c.Name);
            }
            if (ComboCameras.Items.Count > 0)
            {
                ComboCameras.SelectedIndex = 0;
            }
            else
            {
                ComboCameras.Items.Add("(No physical camera detected / Virtual)");
                ComboCameras.SelectedIndex = 0;
            }
        }

        private void RefreshWindowsList()
        {
            _windows = WindowCaptureHelper.GetCapturableWindows();
            ComboWindows.Items.Clear();
            foreach (var w in _windows)
            {
                ComboWindows.Items.Add(w.Title);
            }
            if (ComboWindows.Items.Count > 0) ComboWindows.SelectedIndex = 0;
        }

        private void BuildColorPresets()
        {
            ColorPresetsWrap.Children.Clear();
            var colors = new[]
            {
                Color.FromRgb(0, 0, 0),       // Pure Black
                Color.FromRgb(18, 18, 18),    // Obsidian
                Color.FromRgb(36, 36, 36),    // Dark Gray
                Color.FromRgb(70, 70, 70),    // Mid Gray
                Color.FromRgb(140, 140, 140), // Silver
                Color.FromRgb(255, 255, 255)  // Pure White
            };

            foreach (var col in colors)
            {
                var btn = new Button
                {
                    Width = 34,
                    Height = 34,
                    Margin = new Thickness(3),
                    Background = new SolidColorBrush(col),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                    BorderThickness = new Thickness(1),
                    Tag = col
                };
                btn.Click += (s, e) =>
                {
                    if (s is Button b && b.Tag is Color c)
                    {
                        _selectedSolidColor = c;
                    }
                };
                ColorPresetsWrap.Children.Add(btn);
            }
        }

        private void OnSourceTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SourceTypeListBox.SelectedItem is not ListBoxItem selectedItem) return;
            string tag = selectedItem.Tag?.ToString() ?? "";

            PanelDisplayCapture.Visibility = Visibility.Collapsed;
            PanelWindowCapture.Visibility = Visibility.Collapsed;
            PanelBrowserSource.Visibility = Visibility.Collapsed;
            PanelCameraCapture.Visibility = Visibility.Collapsed;
            PanelPhoneCamera.Visibility = Visibility.Collapsed;
            PanelMediaFile.Visibility = Visibility.Collapsed;
            PanelTextOverlay.Visibility = Visibility.Collapsed;
            PanelColorSource.Visibility = Visibility.Collapsed;
            PanelAudioVisualizer.Visibility = Visibility.Collapsed;

            switch (tag)
            {
                case "DisplayCapture":
                    PanelDisplayCapture.Visibility = Visibility.Visible;
                    TxtSourceName.Text = "Display Capture";
                    break;
                case "WindowCapture":
                    PanelWindowCapture.Visibility = Visibility.Visible;
                    TxtSourceName.Text = "Window Capture";
                    break;
                case "BrowserSource":
                    PanelBrowserSource.Visibility = Visibility.Visible;
                    TxtSourceName.Text = "Browser Overlay";
                    break;
                case "VideoCaptureDevice":
                    PanelCameraCapture.Visibility = Visibility.Visible;
                    TxtSourceName.Text = "Webcam / Camera";
                    break;
                case "PhoneCamera":
                    PanelPhoneCamera.Visibility = Visibility.Visible;
                    TxtSourceName.Text = "Phone Camera Stream";
                    break;
                case "MediaFile":
                    PanelMediaFile.Visibility = Visibility.Visible;
                    TxtSourceName.Text = "Media Video File";
                    break;
                case "ImageOverlay":
                    PanelMediaFile.Visibility = Visibility.Visible;
                    TxtSourceName.Text = "Image Overlay";
                    break;
                case "TextOverlay":
                    PanelTextOverlay.Visibility = Visibility.Visible;
                    TxtSourceName.Text = "Text Overlay";
                    break;
                case "ColorSource":
                    PanelColorSource.Visibility = Visibility.Visible;
                    TxtSourceName.Text = "Solid Background";
                    break;
                case "AudioVisualizer":
                    PanelAudioVisualizer.Visibility = Visibility.Visible;
                    TxtSourceName.Text = "Audio Spectrum Visualizer";
                    break;
            }
        }

        private void OnRefreshWindowsClicked(object sender, RoutedEventArgs e)
        {
            RefreshWindowsList();
        }

        private void OnBrowseFileClicked(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Media or Image File",
                Filter = "All Supported Files|*.mp4;*.mkv;*.mov;*.avi;*.webm;*.png;*.jpg;*.jpeg;*.webp;*.gif|Video Files|*.mp4;*.mkv;*.mov;*.avi;*.webm|Image Files|*.png;*.jpg;*.jpeg;*.webp;*.gif|All Files|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                TxtFilePath.Text = dlg.FileName;
                // Suggest a friendly default name from the file
                try
                {
                    string stem = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                    if (!string.IsNullOrWhiteSpace(stem) &&
                        (string.IsNullOrWhiteSpace(TxtSourceName.Text) ||
                         TxtSourceName.Text == "Image Overlay" ||
                         TxtSourceName.Text == "Media Video File" ||
                         TxtSourceName.Text == "Display Capture" ||
                         TxtSourceName.Text == "Window Capture" ||
                         TxtSourceName.Text == "Webcam / Camera" ||
                         TxtSourceName.Text == "Phone Camera Stream" ||
                         TxtSourceName.Text == "Text Overlay" ||
                         TxtSourceName.Text == "Solid Background" ||
                         TxtSourceName.Text == "Audio Spectrum Visualizer"))
                    {
                        TxtSourceName.Text = stem;
                    }
                }
                catch { }
            }
        }

        private void OnAddSourceClicked(object sender, RoutedEventArgs e)
        {
            if (SourceTypeListBox.SelectedItem is not ListBoxItem selectedItem) return;
            string tag = selectedItem.Tag?.ToString() ?? "DisplayCapture";

            // Validate file-based sources before creating a dead layer
            if ((tag == "ImageOverlay" || tag == "MediaFile") && string.IsNullOrWhiteSpace(TxtFilePath.Text))
            {
                MessageBox.Show(this, "Please pick a file first using the Browse... button.", "File Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var src = new SourceItem
            {
                Name = string.IsNullOrWhiteSpace(TxtSourceName.Text) ? "New Source" : TxtSourceName.Text.Trim()
            };

            switch (tag)
            {
                case "DisplayCapture":
                    src.Type = SourceType.DisplayCapture;
                    src.DisplayIndex = ComboDisplays.SelectedIndex >= 0 ? ComboDisplays.SelectedIndex : 0;
                    src.CaptureCursor = ChkCaptureCursor.IsChecked == true;
                    src.Width = 1920;
                    src.Height = 1080;
                    break;

                case "WindowCapture":
                    src.Type = SourceType.WindowCapture;
                    if (ComboWindows.SelectedIndex >= 0 && ComboWindows.SelectedIndex < _windows.Count)
                    {
                        var win = _windows[ComboWindows.SelectedIndex];
                        src.WindowHandle = win.Handle;
                        src.WindowTitle = win.Title;
                        src.Width = Math.Max(320, win.Bounds.Width);
                        src.Height = Math.Max(240, win.Bounds.Height);
                    }
                    break;

                case "BrowserSource":
                    src.Type = SourceType.BrowserSource;
                    src.BrowserUrl = string.IsNullOrWhiteSpace(TxtBrowserUrl.Text) ? "https://streamlabs.com" : TxtBrowserUrl.Text.Trim();
                    if (int.TryParse(TxtBrowserWidth.Text, out int bw)) src.BrowserWidth = bw;
                    if (int.TryParse(TxtBrowserHeight.Text, out int bh)) src.BrowserHeight = bh;
                    src.Width = src.BrowserWidth;
                    src.Height = src.BrowserHeight;
                    src.CustomCss = TxtCustomCss.Text;
                    src.RefreshOnSceneActive = ChkBrowserRefreshOnScene.IsChecked == true;
                    break;

                case "VideoCaptureDevice":
                    src.Type = SourceType.VideoCaptureDevice;
                    if (ComboCameras.SelectedIndex >= 0 && ComboCameras.SelectedIndex < _cameras.Count)
                    {
                        var cam = _cameras[ComboCameras.SelectedIndex];
                        src.CameraDeviceId = cam.Id;
                        src.CameraDeviceName = cam.Name;
                    }
                    src.HorizontalFlip = ChkCameraFlipH.IsChecked == true;
                    src.ChromaKeyEnabled = ChkCameraChromaKey.IsChecked == true;
                    src.Width = 640;
                    src.Height = 360;
                    src.X = 1240;
                    src.Y = 680;
                    break;

                case "PhoneCamera":
                    src.Type = SourceType.PhoneCamera;
                    src.PhoneStreamUrl = string.IsNullOrWhiteSpace(TxtPhoneUrl.Text) ? "http://192.168.1.100:8080/video" : TxtPhoneUrl.Text.Trim();
                    src.HorizontalFlip = ChkPhoneFlipH.IsChecked == true;
                    src.Width = 640;
                    src.Height = 360;
                    src.X = 1240;
                    src.Y = 680;
                    break;

                case "MediaFile":
                    src.Type = SourceType.MediaFile;
                    src.FilePath = TxtFilePath.Text;
                    src.LoopMedia = ChkLoopMedia.IsChecked == true;
                    src.Width = 1920;
                    src.Height = 1080;
                    break;

                case "ImageOverlay":
                    src.Type = SourceType.ImageOverlay;
                    src.FilePath = TxtFilePath.Text;
                    src.Width = 500;
                    src.Height = 500;
                    src.X = 100;
                    src.Y = 100;
                    break;

                case "TextOverlay":
                    src.Type = SourceType.TextOverlay;
                    src.TextContent = TxtOverlayContent.Text;
                    src.FontFamily = TxtFontFamily.Text;
                    if (double.TryParse(TxtFontSize.Text, out double fsize)) src.FontSize = fsize;
                    src.IsBold = ChkBold.IsChecked == true;
                    src.IsItalic = ChkItalic.IsChecked == true;
                    src.Width = 800;
                    src.Height = 120;
                    src.X = 560;
                    src.Y = 880;
                    break;

                case "ColorSource":
                    src.Type = SourceType.ColorSource;
                    src.SolidColor = _selectedSolidColor;
                    src.Width = 1920;
                    src.Height = 1080;
                    break;

                case "AudioVisualizer":
                    src.Type = SourceType.AudioVisualizer;
                    src.Width = 600;
                    src.Height = 120;
                    src.X = 660;
                    src.Y = 880;
                    break;
            }

            CreatedSource = src;
            DialogResult = true;
            Close();
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
