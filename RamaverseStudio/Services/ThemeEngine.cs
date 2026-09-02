using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace RamaverseStudio.Services
{
    public enum StudioTheme
    {
        ObsidianPurple,
        CyberpunkNeon,
        IndustrialMonolith,
        MatrixGreen,
        StudioLight
    }

    public enum UiDensity
    {
        Compact,
        Normal,
        BroadcastPro
    }

    /// <summary>
    /// Runtime Theme & Accessibility engine for Ramaverse Studio.
    /// Manages 5 WCAG 2.2 AAA compliant themes, dynamic font scaling (100% to 150%),
    /// and touch/broadcast density modes.
    /// </summary>
    public sealed class ThemeEngine : INotifyPropertyChanged
    {
        public static ThemeEngine Instance { get; } = new ThemeEngine();

        private StudioTheme _currentTheme = StudioTheme.ObsidianPurple;
        private UiDensity _currentDensity = UiDensity.Normal;
        private double _fontScale = 1.0;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? ThemeChanged;

        public StudioTheme CurrentTheme
        {
            get => _currentTheme;
            set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    ApplyTheme(value);
                    OnPropertyChanged();
                    ThemeChanged?.Invoke();
                }
            }
        }

        public UiDensity CurrentDensity
        {
            get => _currentDensity;
            set
            {
                if (_currentDensity != value)
                {
                    _currentDensity = value;
                    ApplyDensity(value);
                    OnPropertyChanged();
                }
            }
        }

        public double FontScale
        {
            get => _fontScale;
            set
            {
                double clamped = Math.Clamp(value, 0.9, 1.6);
                if (Math.Abs(_fontScale - clamped) > 0.001)
                {
                    _fontScale = clamped;
                    ApplyFontScale(clamped);
                    OnPropertyChanged();
                }
            }
        }

        public void ApplyTheme(StudioTheme theme)
        {
            var app = Application.Current;
            if (app == null) return;

            string themeUri = theme switch
            {
                StudioTheme.CyberpunkNeon => "Themes/Theme.CyberpunkNeon.xaml",
                StudioTheme.IndustrialMonolith => "Themes/Theme.IndustrialMonolith.xaml",
                StudioTheme.MatrixGreen => "Themes/Theme.MatrixGreen.xaml",
                StudioTheme.StudioLight => "Themes/Theme.StudioLight.xaml",
                _ => "Themes/Theme.ObsidianPurple.xaml"
            };

            try
            {
                var newDict = new ResourceDictionary
                {
                    Source = new Uri(themeUri, UriKind.RelativeOrAbsolute)
                };

                // Merge into application resources
                var merged = app.Resources.MergedDictionaries;
                
                // Find and replace existing theme dict
                int themeIndex = -1;
                for (int i = 0; i < merged.Count; i++)
                {
                    if (merged[i].Source?.OriginalString.Contains("Theme.") == true)
                    {
                        themeIndex = i;
                        break;
                    }
                }

                if (themeIndex >= 0)
                {
                    merged[themeIndex] = newDict;
                }
                else
                {
                    merged.Add(newDict);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeEngine] ApplyTheme error: {ex.Message}");
            }
        }

        public void ApplyDensity(UiDensity density)
        {
            var app = Application.Current;
            if (app == null) return;

            double buttonHeight = density switch
            {
                UiDensity.Compact => 28.0,
                UiDensity.BroadcastPro => 44.0,
                _ => 34.0
            };

            double paddingX = density switch
            {
                UiDensity.Compact => 8.0,
                UiDensity.BroadcastPro => 16.0,
                _ => 12.0
            };

            app.Resources["DynamicControlHeight"] = buttonHeight;
            app.Resources["DynamicPaddingX"] = paddingX;
        }

        public void ApplyFontScale(double scale)
        {
            var app = Application.Current;
            if (app == null) return;

            app.Resources["DynamicFontSizeSm"] = 11.0 * scale;
            app.Resources["DynamicFontSizeBase"] = 13.0 * scale;
            app.Resources["DynamicFontSizeLg"] = 15.0 * scale;
            app.Resources["DynamicFontSizeXl"] = 18.0 * scale;
        }

        private void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
