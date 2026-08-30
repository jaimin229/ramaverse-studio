using System;
using System.Windows;
using System.Windows.Controls;

namespace RamaverseStudio.UI
{
    public partial class AudioMeterControl : UserControl
    {
        public AudioMeterControl()
        {
            InitializeComponent();
            SizeChanged += (s, e) => UpdateMeterVisuals();
        }

        public static readonly DependencyProperty LevelDbProperty =
            DependencyProperty.Register("LevelDb", typeof(double), typeof(AudioMeterControl),
                new PropertyMetadata(-60.0, OnLevelDbChanged));

        public static readonly DependencyProperty PeakHoldDbProperty =
            DependencyProperty.Register("PeakHoldDb", typeof(double), typeof(AudioMeterControl),
                new PropertyMetadata(-60.0, OnPeakHoldDbChanged));

        public double LevelDb
        {
            get => (double)GetValue(LevelDbProperty);
            set => SetValue(LevelDbProperty, value);
        }

        public double PeakHoldDb
        {
            get => (double)GetValue(PeakHoldDbProperty);
            set => SetValue(PeakHoldDbProperty, value);
        }

        public void SetLevel(double levelDb, double peakHoldDb)
        {
            LevelDb = levelDb;
            PeakHoldDb = peakHoldDb;
        }

        private static void OnLevelDbChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AudioMeterControl meter)
            {
                meter.UpdateMeterVisuals();
            }
        }

        private static void OnPeakHoldDbChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AudioMeterControl meter)
            {
                meter.UpdateMeterVisuals();
            }
        }

        private void UpdateMeterVisuals()
        {
            double totalWidth = ActualWidth;
            if (totalWidth <= 0) return;

            // Mapping -60 dB .. 0 dB to 0.0 .. 1.0 (non-linear for standard audio perception)
            double norm = DbToNormalized(LevelDb);
            MeterBar.Width = Math.Clamp(norm * totalWidth, 0, totalWidth);

            double peakNorm = DbToNormalized(PeakHoldDb);
            if (peakNorm > 0.02)
            {
                PeakLine.Visibility = Visibility.Visible;
                double left = Math.Clamp(peakNorm * totalWidth - 2, 0, totalWidth - 2);
                PeakLine.Margin = new Thickness(left, 0, 0, 0);
            }
            else
            {
                PeakLine.Visibility = Visibility.Collapsed;
            }
        }

        private double DbToNormalized(double db)
        {
            if (db <= -60.0) return 0.0;
            if (db >= 0.0) return 1.0;

            // Non-linear perceptual scale:
            // -60 dB -> 0.0
            // -18 dB -> 0.65
            // -6 dB  -> 0.85
            // 0 dB   -> 1.0
            if (db < -18.0)
            {
                return 0.65 * ((db + 60.0) / 42.0);
            }
            else if (db < -6.0)
            {
                return 0.65 + 0.20 * ((db + 18.0) / 12.0);
            }
            else
            {
                return 0.85 + 0.15 * ((db + 6.0) / 6.0);
            }
        }
    }
}
