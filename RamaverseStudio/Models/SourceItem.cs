using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace RamaverseStudio.Models
{
    public enum SourceType
    {
        DisplayCapture,
        WindowCapture,
        VideoCaptureDevice, // Webcam / Capture card
        PhoneCamera,        // Wireless / IP Webcam
        MediaFile,          // Video loop / clip
        ImageOverlay,       // PNG/JPG
        TextOverlay,        // GDI+ Text
        ColorSource,        // Solid color
        AudioVisualizer,    // Live Audio Spectrum / Waveform Bars
        AudioInputCapture,  // Mic
        AudioOutputCapture  // Desktop audio
    }

    public enum CanvasFormat
    {
        Horizontal16x9, // 1920x1080
        Vertical9x16,   // 1080x1920
        Square1x1,      // 1080x1080
        Custom
    }

    public class SourceItem : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString("N");
        private string _name = "Source";
        private SourceType _type = SourceType.DisplayCapture;
        private bool _isVisible = true;
        private bool _isLocked = false;
        private bool _isSelected = false;

        // Transform properties (Relative to canvas)
        private double _x = 0;
        private double _y = 0;
        private double _width = 1920;
        private double _height = 1080;
        private double _rotation = 0;
        private double _opacity = 1.0;
        private int _zIndex = 0;

        // Crop properties
        private double _cropLeft = 0;
        private double _cropTop = 0;
        private double _cropRight = 0;
        private double _cropBottom = 0;

        // Video Proc Amp / Color Adjustments
        private bool _colorAdjustEnabled = false;
        private double _brightness = 0.0;  // -100 to +100
        private double _contrast = 1.0;    // 0.0 to 3.0 (1.0 default)
        private double _hue = 0.0;         // -180 to +180
        private double _saturation = 1.0;  // 0.0 to 3.0 (1.0 default)
        private double _gamma = 1.0;       // 0.1 to 3.0 (1.0 default)

        // Chroma Key (Green Screen)
        private bool _chromaKeyEnabled = false;
        private Color _keyColor = Color.FromRgb(0, 255, 0); // Green
        private double _keySimilarity = 0.35; // 0.0 to 1.0
        private double _keySmoothness = 0.10; // 0.0 to 1.0
        private double _keySpillReduction = 0.50; // 0.0 to 1.0

        // DisplayCapture
        private int _displayIndex = 0;
        private bool _captureCursor = true;

        // WindowCapture
        private IntPtr _windowHandle = IntPtr.Zero;
        private string _windowTitle = "";

        // VideoCaptureDevice
        private string _cameraDeviceId = "";
        private string _cameraDeviceName = "";
        private int _cameraResolutionWidth = 1920;
        private int _cameraResolutionHeight = 1080;
        private int _cameraFps = 30;
        private bool _horizontalFlip = false;
        private bool _verticalFlip = false;

        // PhoneCamera
        private string _phoneStreamUrl = "http://192.168.1.100:8080/video";

        // MediaFile / ImageOverlay
        private string _filePath = "";
        private bool _loopMedia = true;

        // TextOverlay
        private string _textContent = "RAMAVERSE STUDIO";
        private string _fontFamily = "Segoe UI";
        private double _fontSize = 44;
        private bool _isBold = true;
        private bool _isItalic = false;
        private Color _textColor = Colors.White;
        private Color _textBackgroundColor = Color.FromArgb(160, 0, 0, 0);
        private Color _textOutlineColor = Colors.Black;
        private double _textOutlineThickness = 2.0;

        // ColorSource
        private Color _solidColor = Color.FromRgb(15, 23, 42);

        public string Id { get => _id; set => SetField(ref _id, value); }
        public string Name { get => _name; set => SetField(ref _name, value); }
        public SourceType Type { get => _type; set => SetField(ref _type, value); }
        public bool IsVisible { get => _isVisible; set => SetField(ref _isVisible, value); }
        public bool IsLocked { get => _isLocked; set => SetField(ref _isLocked, value); }
        public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }

        public double X { get => _x; set => SetField(ref _x, value); }
        public double Y { get => _y; set => SetField(ref _y, value); }
        public double Width { get => _width; set => SetField(ref _width, Math.Max(10, value)); }
        public double Height { get => _height; set => SetField(ref _height, Math.Max(10, value)); }
        public double Rotation { get => _rotation; set => SetField(ref _rotation, value); }
        public double Opacity { get => _opacity; set => SetField(ref _opacity, Math.Clamp(value, 0.0, 1.0)); }
        public int ZIndex { get => _zIndex; set => SetField(ref _zIndex, value); }

        public double CropLeft { get => _cropLeft; set => SetField(ref _cropLeft, value); }
        public double CropTop { get => _cropTop; set => SetField(ref _cropTop, value); }
        public double CropRight { get => _cropRight; set => SetField(ref _cropRight, value); }
        public double CropBottom { get => _cropBottom; set => SetField(ref _cropBottom, value); }

        public bool ColorAdjustEnabled { get => _colorAdjustEnabled; set => SetField(ref _colorAdjustEnabled, value); }
        public double Brightness { get => _brightness; set => SetField(ref _brightness, value); }
        public double Contrast { get => _contrast; set => SetField(ref _contrast, value); }
        public double Hue { get => _hue; set => SetField(ref _hue, value); }
        public double Saturation { get => _saturation; set => SetField(ref _saturation, value); }
        public double Gamma { get => _gamma; set => SetField(ref _gamma, value); }

        public bool ChromaKeyEnabled { get => _chromaKeyEnabled; set => SetField(ref _chromaKeyEnabled, value); }
        public Color KeyColor { get => _keyColor; set => SetField(ref _keyColor, value); }
        public double KeySimilarity { get => _keySimilarity; set => SetField(ref _keySimilarity, value); }
        public double KeySmoothness { get => _keySmoothness; set => SetField(ref _keySmoothness, value); }
        public double KeySpillReduction { get => _keySpillReduction; set => SetField(ref _keySpillReduction, value); }

        public int DisplayIndex { get => _displayIndex; set => SetField(ref _displayIndex, value); }
        public bool CaptureCursor { get => _captureCursor; set => SetField(ref _captureCursor, value); }

        [System.Text.Json.Serialization.JsonIgnore]
        public IntPtr WindowHandle { get => _windowHandle; set => SetField(ref _windowHandle, value); }
        public string WindowTitle { get => _windowTitle; set => SetField(ref _windowTitle, value); }

        public string CameraDeviceId { get => _cameraDeviceId; set => SetField(ref _cameraDeviceId, value); }
        public string CameraDeviceName { get => _cameraDeviceName; set => SetField(ref _cameraDeviceName, value); }
        public int CameraResolutionWidth { get => _cameraResolutionWidth; set => SetField(ref _cameraResolutionWidth, value); }
        public int CameraResolutionHeight { get => _cameraResolutionHeight; set => SetField(ref _cameraResolutionHeight, value); }
        public int CameraFps { get => _cameraFps; set => SetField(ref _cameraFps, value); }
        public bool HorizontalFlip { get => _horizontalFlip; set => SetField(ref _horizontalFlip, value); }
        public bool VerticalFlip { get => _verticalFlip; set => SetField(ref _verticalFlip, value); }

        public string PhoneStreamUrl { get => _phoneStreamUrl; set => SetField(ref _phoneStreamUrl, value); }

        public string FilePath { get => _filePath; set => SetField(ref _filePath, value); }
        public bool LoopMedia { get => _loopMedia; set => SetField(ref _loopMedia, value); }

        public string TextContent { get => _textContent; set => SetField(ref _textContent, value); }
        public string FontFamily { get => _fontFamily; set => SetField(ref _fontFamily, value); }
        public double FontSize { get => _fontSize; set => SetField(ref _fontSize, value); }
        public bool IsBold { get => _isBold; set => SetField(ref _isBold, value); }
        public bool IsItalic { get => _isItalic; set => SetField(ref _isItalic, value); }
        public Color TextColor { get => _textColor; set => SetField(ref _textColor, value); }
        public Color TextBackgroundColor { get => _textBackgroundColor; set => SetField(ref _textBackgroundColor, value); }
        public Color TextOutlineColor { get => _textOutlineColor; set => SetField(ref _textOutlineColor, value); }
        public double TextOutlineThickness { get => _textOutlineThickness; set => SetField(ref _textOutlineThickness, value); }

        public Color SolidColor { get => _solidColor; set => SetField(ref _solidColor, value); }

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

        public SourceItem Clone()
        {
            var clone = (SourceItem)this.MemberwiseClone();
            clone.Id = Guid.NewGuid().ToString("N");
            clone.Name = $"{this.Name} (Copy)";
            return clone;
        }
    }
}
