using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace RamaverseStudio.Models
{
    public enum VideoEncoder
    {
        AutoHardware,
        NvidiaNvencH264,
        NvidiaNvencHevc,
        AmdAmfH264,
        IntelQsvH264,
        SoftwareX264,
        SoftwareX265,
        SoftwareSvtAv1
    }

    public enum RecordingFormat
    {
        MP4,
        MKV,
        MOV,
        WebM
    }

    public enum CreatorPreset
    {
        YouTube4K,
        YouTube1080p60,
        ShortsReelsTikTokVertical,
        TwitchLive1080p,
        HighQualityArchival,
        Custom
    }

    public class StudioProfile : INotifyPropertyChanged
    {
        private string _name = "Default Profile";
        private CanvasFormat _canvasFormat = CanvasFormat.Horizontal16x9;
        private int _canvasWidth = 1920;
        private int _canvasHeight = 1080;
        private int _fps = 60;

        // Recording Settings
        private string _recordingDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "RamaverseStudio");
        private RecordingFormat _recFormat = RecordingFormat.MP4;
        private VideoEncoder _encoder = VideoEncoder.AutoHardware;
        private int _recordingBitrateKbps = 12000;
        private int _audioBitrateKbps = 320;
        private bool _multiTrackAudio = false;
        private CreatorPreset _activePreset = CreatorPreset.YouTube1080p60;

        // Audio Device Settings
        private string _selectedMicDevice = "Default";
        private string _selectedAudioOutputDevice = "Default";
        private double _micVolume = 1.0;
        private double _desktopVolume = 0.8;

        // Streaming Settings (Primary: YouTube / Twitch)
        private string _streamPlatform = "YouTube Live";
        private string _rtmpServerUrl = "rtmp://a.rtmp.youtube.com/live2";
        private string _streamKey = "";
        private int _streamBitrateKbps = 6000;
        private int _streamAudioBitrateKbps = 160;

        // Dual-Streaming Settings (Secondary: TikTok / Instagram / Shorts 9:16)
        private bool _dualStreamingEnabled = false;
        private string _secondaryStreamPlatform = "TikTok Live";
        private string _secondaryRtmpServerUrl = "rtmp://live.tiktok.com/app";
        private string _secondaryStreamKey = "";
        private int _secondaryStreamBitrateKbps = 4500;
        private string _secondaryLayoutMode = "CenterCrop"; // "CenterCrop" or "LetterboxPad"

        public string Name { get => _name; set => SetField(ref _name, value); }
        public CanvasFormat CanvasFormat
        {
            get => _canvasFormat;
            set
            {
                if (SetField(ref _canvasFormat, value))
                {
                    ApplyCanvasDimensions();
                }
            }
        }

        public int CanvasWidth { get => _canvasWidth; set => SetField(ref _canvasWidth, value); }
        public int CanvasHeight { get => _canvasHeight; set => SetField(ref _canvasHeight, value); }
        public int Fps { get => _fps; set => SetField(ref _fps, value); }

        public string RecordingDirectory { get => _recordingDirectory; set => SetField(ref _recordingDirectory, value); }
        public RecordingFormat RecFormat { get => _recFormat; set => SetField(ref _recFormat, value); }
        public VideoEncoder Encoder { get => _encoder; set => SetField(ref _encoder, value); }
        public int RecordingBitrateKbps { get => _recordingBitrateKbps; set => SetField(ref _recordingBitrateKbps, value); }
        public int AudioBitrateKbps { get => _audioBitrateKbps; set => SetField(ref _audioBitrateKbps, value); }
        public bool MultiTrackAudioRecording { get => _multiTrackAudio; set => SetField(ref _multiTrackAudio, value); }
        
        public CreatorPreset ActivePreset
        {
            get => _activePreset;
            set
            {
                if (SetField(ref _activePreset, value))
                {
                    ApplyCreatorPreset(value);
                }
            }
        }

        public string SelectedMicDevice { get => _selectedMicDevice; set => SetField(ref _selectedMicDevice, value); }
        public string SelectedAudioOutputDevice { get => _selectedAudioOutputDevice; set => SetField(ref _selectedAudioOutputDevice, value); }
        public double MicVolume { get => _micVolume; set => SetField(ref _micVolume, value); }
        public double DesktopVolume { get => _desktopVolume; set => SetField(ref _desktopVolume, value); }

        public string StreamPlatform { get => _streamPlatform; set => SetField(ref _streamPlatform, value); }
        public string RtmpServerUrl { get => _rtmpServerUrl; set => SetField(ref _rtmpServerUrl, value); }
        public string StreamKey { get => _streamKey; set => SetField(ref _streamKey, value); }
        public int StreamBitrateKbps { get => _streamBitrateKbps; set => SetField(ref _streamBitrateKbps, value); }
        public int StreamAudioBitrateKbps { get => _streamAudioBitrateKbps; set => SetField(ref _streamAudioBitrateKbps, value); }

        public bool DualStreamingEnabled { get => _dualStreamingEnabled; set => SetField(ref _dualStreamingEnabled, value); }
        public string SecondaryStreamPlatform { get => _secondaryStreamPlatform; set => SetField(ref _secondaryStreamPlatform, value); }
        public string SecondaryRtmpServerUrl { get => _secondaryRtmpServerUrl; set => SetField(ref _secondaryRtmpServerUrl, value); }
        public string SecondaryStreamKey { get => _secondaryStreamKey; set => SetField(ref _secondaryStreamKey, value); }
        public int SecondaryStreamBitrateKbps { get => _secondaryStreamBitrateKbps; set => SetField(ref _secondaryStreamBitrateKbps, value); }
        public string SecondaryLayoutMode { get => _secondaryLayoutMode; set => SetField(ref _secondaryLayoutMode, value); }

        public void ApplyCanvasDimensions()
        {
            switch (CanvasFormat)
            {
                case CanvasFormat.Horizontal16x9:
                    CanvasWidth = 1920;
                    CanvasHeight = 1080;
                    break;
                case CanvasFormat.Vertical9x16:
                    CanvasWidth = 1080;
                    CanvasHeight = 1920;
                    break;
                case CanvasFormat.Square1x1:
                    CanvasWidth = 1080;
                    CanvasHeight = 1080;
                    break;
            }
        }

        public void ApplyCreatorPreset(CreatorPreset preset)
        {
            switch (preset)
            {
                case CreatorPreset.YouTube4K:
                    CanvasFormat = CanvasFormat.Horizontal16x9;
                    CanvasWidth = 3840;
                    CanvasHeight = 2160;
                    Fps = 60;
                    RecordingBitrateKbps = 35000;
                    RecFormat = RecordingFormat.MP4;
                    break;
                case CreatorPreset.YouTube1080p60:
                    CanvasFormat = CanvasFormat.Horizontal16x9;
                    CanvasWidth = 1920;
                    CanvasHeight = 1080;
                    Fps = 60;
                    RecordingBitrateKbps = 12000;
                    RecFormat = RecordingFormat.MP4;
                    break;
                case CreatorPreset.ShortsReelsTikTokVertical:
                    CanvasFormat = CanvasFormat.Vertical9x16;
                    CanvasWidth = 1080;
                    CanvasHeight = 1920;
                    Fps = 60;
                    RecordingBitrateKbps = 14000;
                    RecFormat = RecordingFormat.MP4;
                    break;
                case CreatorPreset.TwitchLive1080p:
                    CanvasFormat = CanvasFormat.Horizontal16x9;
                    CanvasWidth = 1920;
                    CanvasHeight = 1080;
                    Fps = 60;
                    StreamBitrateKbps = 6000;
                    RecordingBitrateKbps = 8000;
                    StreamPlatform = "Twitch";
                    RtmpServerUrl = "rtmp://live.twitch.tv/app";
                    break;
                case CreatorPreset.HighQualityArchival:
                    CanvasFormat = CanvasFormat.Horizontal16x9;
                    CanvasWidth = 1920;
                    CanvasHeight = 1080;
                    Fps = 60;
                    RecordingBitrateKbps = 25000;
                    RecFormat = RecordingFormat.MKV;
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
