using System;
using System.Windows;
using System.Windows.Controls;

namespace RamaverseStudio.UI
{
    public partial class AtemTBarControl : UserControl
    {
        public event Action? CutRequested;
        public event Action? AutoRequested;
        public event Action<double>? FaderChanged;

        public AtemTBarControl()
        {
            InitializeComponent();
        }

        public void UpdateTimecode(TimeSpan time, int fps = 60)
        {
            int frames = (int)((time.TotalSeconds - Math.Truncate(time.TotalSeconds)) * fps);
            TxtTimecode.Text = $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}:{frames:D2}";
        }

        public void SetTallyState(bool previewActive, bool programActive)
        {
            TxtTallyPreview.Opacity = previewActive ? 1.0 : 0.4;
            TxtTallyProgram.Opacity = programActive ? 1.0 : 0.4;
        }

        private void OnTBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            FaderChanged?.Invoke(e.NewValue);
        }

        private void OnCutClicked(object sender, RoutedEventArgs e)
        {
            CutRequested?.Invoke();
            TBarSlider.Value = 0.0;
        }

        private void OnAutoClicked(object sender, RoutedEventArgs e)
        {
            AutoRequested?.Invoke();
        }
    }
}
