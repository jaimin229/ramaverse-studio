using System;
using System.Windows;
using System.Windows.Media;
using RamaverseStudio.Models;

namespace RamaverseStudio.UI
{
    public partial class AutoConfigWizardWindow : Window
    {
        private int _currentStep = 1;
        private readonly StudioProfile _profile;

        public bool AppliedSettings { get; private set; } = false;

        public AutoConfigWizardWindow(StudioProfile profile)
        {
            InitializeComponent();
            _profile = profile;
            UpdateStepView();
        }

        private void OnNextClicked(object sender, RoutedEventArgs e)
        {
            if (_currentStep == 1)
            {
                _currentStep = 2;
                RunHardwareDetection();
                UpdateStepView();
            }
            else if (_currentStep == 2)
            {
                _currentStep = 3;
                PrepareSummary();
                UpdateStepView();
            }
            else if (_currentStep == 3)
            {
                ApplyConfiguration();
                AppliedSettings = true;
                Close();
            }
        }

        private void OnBackClicked(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 1)
            {
                _currentStep--;
                UpdateStepView();
            }
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UpdateStepView()
        {
            Step1Panel.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3Panel.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;

            var activeBrush = (Brush)FindResource("Brush.Accent");
            var cardBrush = (Brush)FindResource("Brush.ChassisCard");

            Step1Badge.Background = _currentStep >= 1 ? activeBrush : cardBrush;
            Step2Badge.Background = _currentStep >= 2 ? activeBrush : cardBrush;
            Step3Badge.Background = _currentStep >= 3 ? activeBrush : cardBrush;

            BtnBack.IsEnabled = _currentStep > 1;
            BtnNext.Content = _currentStep == 3 ? "Apply Settings" : "Next";
        }

        private void RunHardwareDetection()
        {
            int screenW = (int)SystemParameters.PrimaryScreenWidth;
            int screenH = (int)SystemParameters.PrimaryScreenHeight;
            TxtDisplayInfo.Text = $"{screenW}x{screenH} (Primary Display)";

            TxtEncoderInfo.Text = "Hardware Accelerated (NVIDIA NVENC / AMD AMF)";
            TxtAudioInfo.Text = "Default System Audio Capture (48 kHz Stereo)";
        }

        private void PrepareSummary()
        {
            if (RbGaming.IsChecked == true)
            {
                TxtSummaryCanvas.Text = "1920x1080 @ 60 FPS";
                TxtSummaryBitrate.Text = "6000 Kbps (CBR)";
                TxtSummaryEncoder.Text = "Hardware NVENC (High Framerate)";
            }
            else if (RbChatting.IsChecked == true)
            {
                TxtSummaryCanvas.Text = "1920x1080 @ 60 FPS";
                TxtSummaryBitrate.Text = "4500 Kbps (CBR)";
                TxtSummaryEncoder.Text = "Hardware Encoder (Balanced)";
            }
            else
            {
                TxtSummaryCanvas.Text = "1920x1080 @ 60 FPS";
                TxtSummaryBitrate.Text = "12000 Kbps (High Quality Recording)";
                TxtSummaryEncoder.Text = "Hardware NVENC (Ultra Quality MKV)";
            }
        }

        private void ApplyConfiguration()
        {
            _profile.CanvasWidth = 1920;
            _profile.CanvasHeight = 1080;
            _profile.Fps = 60;

            if (RbGaming.IsChecked == true)
            {
                _profile.StreamBitrateKbps = 6000;
                _profile.RecordingBitrateKbps = 8000;
                _profile.Encoder = VideoEncoder.AutoHardware;
            }
            else if (RbChatting.IsChecked == true)
            {
                _profile.StreamBitrateKbps = 4500;
                _profile.RecordingBitrateKbps = 6000;
                _profile.Encoder = VideoEncoder.AutoHardware;
            }
            else
            {
                _profile.StreamBitrateKbps = 6000;
                _profile.RecordingBitrateKbps = 12000;
                _profile.Encoder = VideoEncoder.AutoHardware;
                _profile.MultiTrackAudioRecording = true;
            }
        }
    }
}
