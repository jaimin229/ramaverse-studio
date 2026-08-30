using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RamaverseStudio.Audio;
using RamaverseStudio.Models;

namespace RamaverseStudio.UI
{
    public partial class SettingsWindow : Window
    {
        private readonly StudioProfile _profile;
        public StudioProfile Profile => _profile;
        private bool _isKeyRevealed = true;

        public SettingsWindow(StudioProfile profile)
        {
            InitializeComponent();
            _profile = profile;

            LoadSettingsToUI();
        }

        private void LoadSettingsToUI()
        {
            // Canvas format
            ComboCanvasFormat.SelectedIndex = _profile.CanvasFormat switch
            {
                CanvasFormat.Horizontal16x9 => (_profile.CanvasWidth > 2000 ? 3 : 0),
                CanvasFormat.Vertical9x16 => 1,
                CanvasFormat.Square1x1 => 2,
                _ => 0
            };

            // FPS
            ComboFps.SelectedIndex = _profile.Fps switch
            {
                60 => 0,
                30 => 1,
                24 => 2,
                _ => 0
            };

            // Encoder
            ComboEncoder.SelectedIndex = _profile.Encoder switch
            {
                VideoEncoder.AutoHardware => 0,
                VideoEncoder.NvidiaNvencH264 => 1,
                VideoEncoder.NvidiaNvencHevc => 2,
                VideoEncoder.AmdAmfH264 => 3,
                VideoEncoder.IntelQsvH264 => 4,
                VideoEncoder.SoftwareX264 => 5,
                VideoEncoder.SoftwareSvtAv1 => 6,
                _ => 0
            };

            // Recording
            TxtRecordingFolder.Text = _profile.RecordingDirectory;
            ComboRecFormat.SelectedIndex = _profile.RecFormat switch
            {
                RecordingFormat.MP4 => 0,
                RecordingFormat.MKV => 1,
                RecordingFormat.MOV => 2,
                RecordingFormat.WebM => 3,
                _ => 0
            };

            ComboRecBitrate.SelectedIndex = _profile.RecordingBitrateKbps switch
            {
                35000 => 0,
                16000 => 1,
                12000 => 2,
                8000 => 3,
                5000 => 4,
                _ => 2
            };

            // Primary Streaming
            TxtRtmpUrl.Text = string.IsNullOrWhiteSpace(_profile.RtmpServerUrl) ? "rtmp://a.rtmp.youtube.com/live2" : _profile.RtmpServerUrl;
            TxtStreamKey.Text = _profile.StreamKey;
            if (_profile.StreamPlatform.Contains("YouTube")) ComboStreamPlatform.SelectedIndex = 0;
            else if (_profile.StreamPlatform.Contains("Twitch")) ComboStreamPlatform.SelectedIndex = 1;
            else if (_profile.StreamPlatform.Contains("Kick")) ComboStreamPlatform.SelectedIndex = 2;
            else ComboStreamPlatform.SelectedIndex = 3;

            ComboStreamBitrate.SelectedIndex = _profile.StreamBitrateKbps switch
            {
                8000 => 0,
                6000 => 1,
                4500 => 2,
                3000 => 3,
                _ => 1
            };

            // Dual Streaming (Vertical)
            ChkDualStreaming.IsChecked = _profile.DualStreamingEnabled;
            PanelDualStreamOptions.IsEnabled = _profile.DualStreamingEnabled;
            TxtSecRtmpUrl.Text = string.IsNullOrWhiteSpace(_profile.SecondaryRtmpServerUrl) ? "rtmp://live.tiktok.com/app" : _profile.SecondaryRtmpServerUrl;
            TxtSecStreamKey.Text = _profile.SecondaryStreamKey;

            if (_profile.SecondaryStreamPlatform.Contains("TikTok")) ComboSecPlatform.SelectedIndex = 0;
            else if (_profile.SecondaryStreamPlatform.Contains("Instagram")) ComboSecPlatform.SelectedIndex = 1;
            else if (_profile.SecondaryStreamPlatform.Contains("Shorts")) ComboSecPlatform.SelectedIndex = 2;
            else ComboSecPlatform.SelectedIndex = 3;

            ComboSecLayoutMode.SelectedIndex = _profile.SecondaryLayoutMode == "LetterboxPad" ? 1 : 0;

            // Audio Devices
            var mics = AudioEngine.GetMicrophoneDevices();
            ComboMicDevices.Items.Clear();
            foreach (var m in mics) ComboMicDevices.Items.Add(m);
            if (ComboMicDevices.Items.Count > 0) ComboMicDevices.SelectedIndex = 0;

            var outs = AudioEngine.GetOutputDevices();
            ComboOutputDevices.Items.Clear();
            foreach (var o in outs) ComboOutputDevices.Items.Add(o);
            if (ComboOutputDevices.Items.Count > 0) ComboOutputDevices.SelectedIndex = 0;
        }

        private void OnDualStreamToggled(object sender, RoutedEventArgs e)
        {
            if (PanelDualStreamOptions != null)
            {
                PanelDualStreamOptions.IsEnabled = ChkDualStreaming.IsChecked == true;
            }
        }

        private void OnSecPlatformChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TxtSecRtmpUrl == null) return;

            switch (ComboSecPlatform.SelectedIndex)
            {
                case 0: // TikTok
                    TxtSecRtmpUrl.Text = "rtmp://live.tiktok.com/app";
                    break;
                case 1: // Instagram
                    TxtSecRtmpUrl.Text = "rtmps://live-upload.instagram.com:443/rtmp";
                    break;
                case 2: // YouTube Shorts
                    TxtSecRtmpUrl.Text = "rtmp://a.rtmp.youtube.com/live2";
                    break;
            }
        }

        private void OnBrowseFolderClicked(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Select Recording Destination Folder",
                InitialDirectory = _profile.RecordingDirectory
            };
            if (dlg.ShowDialog() == true)
            {
                TxtRecordingFolder.Text = dlg.FolderName;
            }
        }

        private void OnStreamPlatformChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TxtRtmpUrl == null) return;

            switch (ComboStreamPlatform.SelectedIndex)
            {
                case 0: // YouTube
                    TxtRtmpUrl.Text = "rtmp://a.rtmp.youtube.com/live2";
                    break;
                case 1: // Twitch
                    TxtRtmpUrl.Text = "rtmp://live.twitch.tv/app";
                    break;
                case 2: // Kick
                    TxtRtmpUrl.Text = "rtmps://fa723fc1b171.global-contribute.live-video.net";
                    break;
            }
        }

        private void OnToggleKeyMaskClicked(object sender, RoutedEventArgs e)
        {
            _isKeyRevealed = !_isKeyRevealed;
            BtnToggleKeyMask.Content = _isKeyRevealed ? "👁 Hide" : "👁 Show";
        }

        private void OnOpenYouTubeDashboardClicked(object sender, RoutedEventArgs e)
        {
            OpenUrlInBrowser("https://studio.youtube.com/channel/live");
        }

        private void OnOpenTwitchDashboardClicked(object sender, RoutedEventArgs e)
        {
            OpenUrlInBrowser("https://dashboard.twitch.tv/settings/stream");
        }

        private void OnOpenKickDashboardClicked(object sender, RoutedEventArgs e)
        {
            OpenUrlInBrowser("https://kick.com/dashboard/stream");
        }

        private static void OpenUrlInBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open browser: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            // Canvas & Video
            switch (ComboCanvasFormat.SelectedIndex)
            {
                case 0:
                    _profile.CanvasFormat = CanvasFormat.Horizontal16x9;
                    _profile.CanvasWidth = 1920;
                    _profile.CanvasHeight = 1080;
                    break;
                case 1:
                    _profile.CanvasFormat = CanvasFormat.Vertical9x16;
                    _profile.CanvasWidth = 1080;
                    _profile.CanvasHeight = 1920;
                    break;
                case 2:
                    _profile.CanvasFormat = CanvasFormat.Square1x1;
                    _profile.CanvasWidth = 1080;
                    _profile.CanvasHeight = 1080;
                    break;
                case 3:
                    _profile.CanvasFormat = CanvasFormat.Horizontal16x9;
                    _profile.CanvasWidth = 3840;
                    _profile.CanvasHeight = 2160;
                    break;
            }

            _profile.Fps = ComboFps.SelectedIndex switch
            {
                0 => 60,
                1 => 30,
                2 => 24,
                _ => 60
            };

            _profile.Encoder = ComboEncoder.SelectedIndex switch
            {
                0 => VideoEncoder.AutoHardware,
                1 => VideoEncoder.NvidiaNvencH264,
                2 => VideoEncoder.NvidiaNvencHevc,
                3 => VideoEncoder.AmdAmfH264,
                4 => VideoEncoder.IntelQsvH264,
                5 => VideoEncoder.SoftwareX264,
                6 => VideoEncoder.SoftwareSvtAv1,
                _ => VideoEncoder.AutoHardware
            };

            // Recording
            if (!string.IsNullOrWhiteSpace(TxtRecordingFolder.Text))
            {
                _profile.RecordingDirectory = TxtRecordingFolder.Text.Trim();
            }

            _profile.RecFormat = ComboRecFormat.SelectedIndex switch
            {
                0 => RecordingFormat.MP4,
                1 => RecordingFormat.MKV,
                2 => RecordingFormat.MOV,
                3 => RecordingFormat.WebM,
                _ => RecordingFormat.MP4
            };

            _profile.RecordingBitrateKbps = ComboRecBitrate.SelectedIndex switch
            {
                0 => 35000,
                1 => 16000,
                2 => 12000,
                3 => 8000,
                4 => 5000,
                _ => 12000
            };

            // Primary Streaming
            _profile.StreamPlatform = ((ComboBoxItem)ComboStreamPlatform.SelectedItem).Content.ToString() ?? "";
            _profile.RtmpServerUrl = TxtRtmpUrl.Text.Trim();
            _profile.StreamKey = TxtStreamKey.Text.Trim();
            _profile.StreamBitrateKbps = ComboStreamBitrate.SelectedIndex switch
            {
                0 => 8000,
                1 => 6000,
                2 => 4500,
                3 => 3000,
                _ => 6000
            };

            // Dual Streaming (Vertical)
            _profile.DualStreamingEnabled = ChkDualStreaming.IsChecked == true;
            _profile.SecondaryStreamPlatform = ((ComboBoxItem)ComboSecPlatform.SelectedItem).Content.ToString() ?? "";
            _profile.SecondaryRtmpServerUrl = TxtSecRtmpUrl.Text.Trim();
            _profile.SecondaryStreamKey = TxtSecStreamKey.Text.Trim();
            _profile.SecondaryLayoutMode = ComboSecLayoutMode.SelectedIndex == 1 ? "LetterboxPad" : "CenterCrop";

            DialogResult = true;
            Close();
        }

        private async void OnCheckUpdatesNowClicked(object sender, RoutedEventArgs e)
        {
            var updateManager = new AutoUpdate.UpdateManager();
            var update = await updateManager.CheckForUpdatesAsync();

            if (update != null)
            {
                var dlg = new UpdateDialog(update, updateManager) { Owner = this };
                dlg.ShowDialog();
            }
            else
            {
                MessageBox.Show($"You are running the latest version of Ramaverse Studio (v{AutoUpdate.UpdateManager.CurrentVersion}).", "No Updates Available", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
