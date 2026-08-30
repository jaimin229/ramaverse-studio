using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using RamaverseStudio.Audio;
using RamaverseStudio.Models;

namespace RamaverseStudio.UI
{
    public partial class AudioFiltersDialog : Window
    {
        private readonly AudioEngine _audioEngine;
        private readonly AudioFilterSettings _settings;
        private readonly DispatcherTimer _meterTimer;

        public AudioFiltersDialog(AudioEngine audioEngine)
        {
            InitializeComponent();
            _audioEngine = audioEngine;
            _settings = audioEngine.FilterSettings;

            LoadSettingsToUI();

            _meterTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
            };
            _meterTimer.Tick += OnMeterTick;
            _meterTimer.Start();

            Closed += (s, e) => _meterTimer.Stop();
        }

        private void LoadSettingsToUI()
        {
            // Voice Changer
            ChkVoiceChanger.IsChecked = _settings.VoiceChangerEnabled;
            SliderPitch.Value = _settings.PitchShiftSemitones;
            SliderRobotMod.Value = _settings.RobotModFrequencyHz;
            SliderDistortion.Value = _settings.DistortionDrive;
            ChkBandpass.IsChecked = _settings.BandpassEnabled;
            UpdateDspLabels();

            // Noise Gate & Suppression
            ChkNoiseSuppression.IsChecked = _settings.NoiseSuppressionEnabled;
            SliderNoiseSuppression.Value = _settings.NoiseSuppressionAmountDb;
            TxtNoiseSuppressionVal.Text = $"{_settings.NoiseSuppressionAmountDb:F0} dB";

            ChkNoiseGate.IsChecked = _settings.NoiseGateEnabled;
            SliderGateThreshold.Value = _settings.GateThresholdDb;
            SliderGateAttack.Value = _settings.GateAttackMs;
            SliderGateRelease.Value = _settings.GateReleaseMs;
            TxtGateThresholdVal.Text = $"{_settings.GateThresholdDb:F0} dB";
            TxtGateAttackVal.Text = $"{_settings.GateAttackMs:F0} ms";
            TxtGateReleaseVal.Text = $"{_settings.GateReleaseMs:F0} ms";

            // EQ
            ChkEq.IsChecked = _settings.EqEnabled;
            SliderEqLow.Value = _settings.EqLowGainDb;
            SliderEqMid.Value = _settings.EqMidGainDb;
            SliderEqHigh.Value = _settings.EqHighGainDb;
            TxtEqLowVal.Text = $"{_settings.EqLowGainDb:+0.0;-0.0;0.0} dB";
            TxtEqMidVal.Text = $"{_settings.EqMidGainDb:+0.0;-0.0;0.0} dB";
            TxtEqHighVal.Text = $"{_settings.EqHighGainDb:+0.0;-0.0;0.0} dB";

            // Compressor & Limiter
            ChkCompressor.IsChecked = _settings.CompressorEnabled;
            SliderCompThreshold.Value = _settings.CompThresholdDb;
            SliderCompRatio.Value = _settings.CompRatio;
            SliderCompMakeup.Value = _settings.CompMakeupGainDb;
            TxtCompThresholdVal.Text = $"{_settings.CompThresholdDb:F0} dB";
            TxtCompRatioVal.Text = $"{_settings.CompRatio:F0}:1";
            TxtCompMakeupVal.Text = $"+{_settings.CompMakeupGainDb:F0} dB";

            ChkLimiter.IsChecked = _settings.LimiterEnabled;
            SliderLimiterThreshold.Value = _settings.LimiterThresholdDb;
            TxtLimiterThresholdVal.Text = $"{_settings.LimiterThresholdDb:F1} dB";
        }

        private void OnMeterTick(object? sender, EventArgs e)
        {
            LiveMicMeter.LevelDb = _audioEngine.CurrentPeakDb;
            LiveMicMeter.PeakHoldDb = _audioEngine.PeakHoldDb;
            TxtLiveDb.Text = $"{_audioEngine.CurrentPeakDb:F1} dB";
        }

        private void OnVoiceChangerToggled(object sender, RoutedEventArgs e)
        {
            _settings.VoiceChangerEnabled = ChkVoiceChanger.IsChecked == true;
        }

        private void OnPresetClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag && Enum.TryParse<VoiceChangerPreset>(tag, out var preset))
            {
                _settings.SelectedPreset = preset;
                LoadSettingsToUI();
            }
        }

        private void OnDspParamChanged(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;

            _settings.PitchShiftSemitones = SliderPitch.Value;
            _settings.RobotModFrequencyHz = SliderRobotMod.Value;
            _settings.DistortionDrive = SliderDistortion.Value;
            _settings.BandpassEnabled = ChkBandpass.IsChecked == true;

            UpdateDspLabels();
        }

        private void UpdateDspLabels()
        {
            TxtPitchVal.Text = $"{SliderPitch.Value:+0.0;-0.0;0.0} st";
            TxtRobotVal.Text = $"{SliderRobotMod.Value:F0} Hz";
            TxtDistortionVal.Text = $"{SliderDistortion.Value:F1}";
        }

        private void OnFilterParamChanged(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;

            _settings.NoiseSuppressionEnabled = ChkNoiseSuppression.IsChecked == true;
            _settings.NoiseSuppressionAmountDb = SliderNoiseSuppression.Value;
            TxtNoiseSuppressionVal.Text = $"{SliderNoiseSuppression.Value:F0} dB";

            _settings.NoiseGateEnabled = ChkNoiseGate.IsChecked == true;
            _settings.GateThresholdDb = SliderGateThreshold.Value;
            _settings.GateAttackMs = SliderGateAttack.Value;
            _settings.GateReleaseMs = SliderGateRelease.Value;
            TxtGateThresholdVal.Text = $"{SliderGateThreshold.Value:F0} dB";
            TxtGateAttackVal.Text = $"{SliderGateAttack.Value:F0} ms";
            TxtGateReleaseVal.Text = $"{SliderGateRelease.Value:F0} ms";

            _settings.EqEnabled = ChkEq.IsChecked == true;
            _settings.EqLowGainDb = SliderEqLow.Value;
            _settings.EqMidGainDb = SliderEqMid.Value;
            _settings.EqHighGainDb = SliderEqHigh.Value;
            TxtEqLowVal.Text = $"{SliderEqLow.Value:+0.0;-0.0;0.0} dB";
            TxtEqMidVal.Text = $"{SliderEqMid.Value:+0.0;-0.0;0.0} dB";
            TxtEqHighVal.Text = $"{SliderEqHigh.Value:+0.0;-0.0;0.0} dB";

            _settings.CompressorEnabled = ChkCompressor.IsChecked == true;
            _settings.CompThresholdDb = SliderCompThreshold.Value;
            _settings.CompRatio = SliderCompRatio.Value;
            _settings.CompMakeupGainDb = SliderCompMakeup.Value;
            TxtCompThresholdVal.Text = $"{SliderCompThreshold.Value:F0} dB";
            TxtCompRatioVal.Text = $"{SliderCompRatio.Value:F0}:1";
            TxtCompMakeupVal.Text = $"+{SliderCompMakeup.Value:F0} dB";

            _settings.LimiterEnabled = ChkLimiter.IsChecked == true;
            _settings.LimiterThresholdDb = SliderLimiterThreshold.Value;
            TxtLimiterThresholdVal.Text = $"{SliderLimiterThreshold.Value:F1} dB";
        }

        private void OnDoneClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
