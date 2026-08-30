using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RamaverseStudio.Models
{
    public enum VoiceChangerPreset
    {
        Original,
        DeepVoice,
        HighVoice,
        Man,
        Woman,
        Boy,
        Girl,
        Robot,
        Alien,
        Radio,
        Megaphone,
        Custom
    }

    public class AudioFilterSettings : INotifyPropertyChanged
    {
        // 1. Noise Suppression
        private bool _noiseSuppressionEnabled = true;
        private double _noiseSuppressionAmountDb = -30.0; // dB

        // 2. Noise Gate
        private bool _noiseGateEnabled = true;
        private double _gateThresholdDb = -45.0; // -60 to -10 dB
        private double _gateAttackMs = 15.0;     // 1 to 100 ms
        private double _gateHoldMs = 50.0;       // 10 to 500 ms
        private double _gateReleaseMs = 150.0;   // 20 to 1000 ms

        // 3. 3-Band Equalizer
        private bool _eqEnabled = true;
        private double _eqLowGainDb = 1.5;   // -15 to +15 dB (100 Hz Shelf)
        private double _eqMidGainDb = 0.0;   // -15 to +15 dB (1.2 kHz Peak)
        private double _eqHighGainDb = 2.0;  // -15 to +15 dB (8 kHz Shelf)

        // 4. Compressor
        private bool _compressorEnabled = true;
        private double _compThresholdDb = -18.0; // -40 to 0 dB
        private double _compRatio = 4.0;         // 1:1 to 20:1
        private double _compAttackMs = 20.0;     // 1 to 100 ms
        private double _compReleaseMs = 120.0;   // 20 to 1000 ms
        private double _compMakeupGainDb = 3.0;  // 0 to 24 dB

        // 5. Limiter
        private bool _limiterEnabled = true;
        private double _limiterThresholdDb = -1.0; // -12 to 0 dB
        private double _limiterReleaseMs = 60.0;

        // 6. Master Mic Gain
        private double _inputGainDb = 0.0; // -20 to +20 dB
        private bool _isMuted = false;

        // 7. Voice Changer
        private bool _voiceChangerEnabled = false;
        private VoiceChangerPreset _selectedPreset = VoiceChangerPreset.Original;
        private double _pitchShiftSemitones = 0.0; // -12 to +12
        private double _robotModFrequencyHz = 50.0; // 20 to 500 Hz
        private double _formantShiftRatio = 1.0;   // 0.5 to 2.0
        private double _distortionDrive = 0.0;     // 0.0 to 10.0
        private double _bandpassLowHz = 300.0;     // 50 to 1000 Hz
        private double _bandpassHighHz = 3400.0;   // 1000 to 12000 Hz
        private bool _bandpassEnabled = false;

        // Public Properties
        public bool NoiseSuppressionEnabled { get => _noiseSuppressionEnabled; set => SetField(ref _noiseSuppressionEnabled, value); }
        public double NoiseSuppressionAmountDb { get => _noiseSuppressionAmountDb; set => SetField(ref _noiseSuppressionAmountDb, value); }

        public bool NoiseGateEnabled { get => _noiseGateEnabled; set => SetField(ref _noiseGateEnabled, value); }
        public double GateThresholdDb { get => _gateThresholdDb; set => SetField(ref _gateThresholdDb, value); }
        public double GateAttackMs { get => _gateAttackMs; set => SetField(ref _gateAttackMs, value); }
        public double GateHoldMs { get => _gateHoldMs; set => SetField(ref _gateHoldMs, value); }
        public double GateReleaseMs { get => _gateReleaseMs; set => SetField(ref _gateReleaseMs, value); }

        public bool EqEnabled { get => _eqEnabled; set => SetField(ref _eqEnabled, value); }
        public double EqLowGainDb { get => _eqLowGainDb; set => SetField(ref _eqLowGainDb, value); }
        public double EqMidGainDb { get => _eqMidGainDb; set => SetField(ref _eqMidGainDb, value); }
        public double EqHighGainDb { get => _eqHighGainDb; set => SetField(ref _eqHighGainDb, value); }

        public bool CompressorEnabled { get => _compressorEnabled; set => SetField(ref _compressorEnabled, value); }
        public double CompThresholdDb { get => _compThresholdDb; set => SetField(ref _compThresholdDb, value); }
        public double CompRatio { get => _compRatio; set => SetField(ref _compRatio, value); }
        public double CompAttackMs { get => _compAttackMs; set => SetField(ref _compAttackMs, value); }
        public double CompReleaseMs { get => _compReleaseMs; set => SetField(ref _compReleaseMs, value); }
        public double CompMakeupGainDb { get => _compMakeupGainDb; set => SetField(ref _compMakeupGainDb, value); }

        public bool LimiterEnabled { get => _limiterEnabled; set => SetField(ref _limiterEnabled, value); }
        public double LimiterThresholdDb { get => _limiterThresholdDb; set => SetField(ref _limiterThresholdDb, value); }
        public double LimiterReleaseMs { get => _limiterReleaseMs; set => SetField(ref _limiterReleaseMs, value); }

        public double InputGainDb { get => _inputGainDb; set => SetField(ref _inputGainDb, value); }
        public bool IsMuted { get => _isMuted; set => SetField(ref _isMuted, value); }

        public bool VoiceChangerEnabled { get => _voiceChangerEnabled; set => SetField(ref _voiceChangerEnabled, value); }
        public VoiceChangerPreset SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (SetField(ref _selectedPreset, value))
                {
                    ApplyPreset(value);
                }
            }
        }
        [System.Text.Json.Serialization.JsonIgnore]
        public VoiceChangerPreset VoiceChangerPreset { get => _selectedPreset; set => SelectedPreset = value; }

        public void CopyFrom(AudioFilterSettings other)
        {
            if (other == null) return;
            NoiseSuppressionEnabled = other.NoiseSuppressionEnabled;
            NoiseSuppressionAmountDb = other.NoiseSuppressionAmountDb;
            NoiseGateEnabled = other.NoiseGateEnabled;
            GateThresholdDb = other.GateThresholdDb;
            GateAttackMs = other.GateAttackMs;
            GateHoldMs = other.GateHoldMs;
            GateReleaseMs = other.GateReleaseMs;
            EqEnabled = other.EqEnabled;
            EqLowGainDb = other.EqLowGainDb;
            EqMidGainDb = other.EqMidGainDb;
            EqHighGainDb = other.EqHighGainDb;
            CompressorEnabled = other.CompressorEnabled;
            CompThresholdDb = other.CompThresholdDb;
            CompRatio = other.CompRatio;
            CompAttackMs = other.CompAttackMs;
            CompReleaseMs = other.CompReleaseMs;
            CompMakeupGainDb = other.CompMakeupGainDb;
            LimiterEnabled = other.LimiterEnabled;
            LimiterThresholdDb = other.LimiterThresholdDb;
            LimiterReleaseMs = other.LimiterReleaseMs;
            InputGainDb = other.InputGainDb;
            IsMuted = other.IsMuted;
            VoiceChangerEnabled = other.VoiceChangerEnabled;
            SelectedPreset = other.SelectedPreset;
            PitchShiftSemitones = other.PitchShiftSemitones;
            RobotModFrequencyHz = other.RobotModFrequencyHz;
            FormantShiftRatio = other.FormantShiftRatio;
            DistortionDrive = other.DistortionDrive;
            BandpassEnabled = other.BandpassEnabled;
        }

        public double PitchShiftSemitones { get => _pitchShiftSemitones; set => SetField(ref _pitchShiftSemitones, value); }
        public double RobotModFrequencyHz { get => _robotModFrequencyHz; set => SetField(ref _robotModFrequencyHz, value); }
        public double FormantShiftRatio { get => _formantShiftRatio; set => SetField(ref _formantShiftRatio, value); }
        public double DistortionDrive { get => _distortionDrive; set => SetField(ref _distortionDrive, value); }
        public double BandpassLowHz { get => _bandpassLowHz; set => SetField(ref _bandpassLowHz, value); }
        public double BandpassHighHz { get => _bandpassHighHz; set => SetField(ref _bandpassHighHz, value); }
        public bool BandpassEnabled { get => _bandpassEnabled; set => SetField(ref _bandpassEnabled, value); }

        public void ApplyPreset(VoiceChangerPreset preset)
        {
            _selectedPreset = preset;
            switch (preset)
            {
                case VoiceChangerPreset.Original:
                    VoiceChangerEnabled = false;
                    PitchShiftSemitones = 0;
                    RobotModFrequencyHz = 0;
                    DistortionDrive = 0;
                    BandpassEnabled = false;
                    break;
                case VoiceChangerPreset.DeepVoice:
                    VoiceChangerEnabled = true;
                    PitchShiftSemitones = -4.5;
                    RobotModFrequencyHz = 0;
                    DistortionDrive = 0.5;
                    BandpassEnabled = false;
                    break;
                case VoiceChangerPreset.HighVoice:
                    VoiceChangerEnabled = true;
                    PitchShiftSemitones = +5.0;
                    RobotModFrequencyHz = 0;
                    DistortionDrive = 0;
                    BandpassEnabled = false;
                    break;
                case VoiceChangerPreset.Man:
                    VoiceChangerEnabled = true;
                    PitchShiftSemitones = -3.0;
                    RobotModFrequencyHz = 0;
                    DistortionDrive = 0.2;
                    BandpassEnabled = false;
                    break;
                case VoiceChangerPreset.Woman:
                    VoiceChangerEnabled = true;
                    PitchShiftSemitones = +3.5;
                    RobotModFrequencyHz = 0;
                    DistortionDrive = 0;
                    BandpassEnabled = false;
                    break;
                case VoiceChangerPreset.Boy:
                    VoiceChangerEnabled = true;
                    PitchShiftSemitones = +2.5;
                    RobotModFrequencyHz = 0;
                    DistortionDrive = 0;
                    BandpassEnabled = false;
                    break;
                case VoiceChangerPreset.Girl:
                    VoiceChangerEnabled = true;
                    PitchShiftSemitones = +4.5;
                    RobotModFrequencyHz = 0;
                    DistortionDrive = 0;
                    BandpassEnabled = false;
                    break;
                case VoiceChangerPreset.Robot:
                    VoiceChangerEnabled = true;
                    PitchShiftSemitones = -2.0;
                    RobotModFrequencyHz = 65.0;
                    DistortionDrive = 1.2;
                    BandpassEnabled = false;
                    break;
                case VoiceChangerPreset.Alien:
                    VoiceChangerEnabled = true;
                    PitchShiftSemitones = +6.0;
                    RobotModFrequencyHz = 140.0;
                    DistortionDrive = 0.8;
                    BandpassEnabled = false;
                    break;
                case VoiceChangerPreset.Radio:
                    VoiceChangerEnabled = true;
                    PitchShiftSemitones = 0.0;
                    RobotModFrequencyHz = 0;
                    DistortionDrive = 1.8;
                    BandpassEnabled = true;
                    BandpassLowHz = 400.0;
                    BandpassHighHz = 3200.0;
                    break;
                case VoiceChangerPreset.Megaphone:
                    VoiceChangerEnabled = true;
                    PitchShiftSemitones = +1.0;
                    RobotModFrequencyHz = 0;
                    DistortionDrive = 4.5;
                    BandpassEnabled = true;
                    BandpassLowHz = 600.0;
                    BandpassHighHz = 2400.0;
                    break;
                case VoiceChangerPreset.Custom:
                    VoiceChangerEnabled = true;
                    break;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
