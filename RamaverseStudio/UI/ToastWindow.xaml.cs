using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using RamaverseStudio.Services;

namespace RamaverseStudio.UI
{
    /// <summary>
    /// Focus-stealing-free toast window. Anchors itself to the bottom-center of
    /// the host window (or primary screen), fades in/out, and closes itself.
    /// Uses WS_EX_NOACTIVATE so clicks pass through to the app beneath.
    /// </summary>
    public partial class ToastWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        private readonly DispatcherTimer _closeTimer;

        public ToastWindow(string message, ToastNotifier.ToastKind kind, Window? host)
        {
            InitializeComponent();

            ToastText.Text = message;

            (GlyphText.Text, ToastBorder.BorderBrush) = kind switch
            {
                ToastNotifier.ToastKind.Success => ("✓", System.Windows.Media.Brushes.White),
                ToastNotifier.ToastKind.Warning => ("!", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 200, 60))),
                ToastNotifier.ToastKind.Error => ("✕", new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 90, 90))),
                _ => ("i", System.Windows.Media.Brushes.White)
            };

            if (kind == ToastNotifier.ToastKind.Warning)
            {
                GlyphBadge.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 200, 60));
            }
            else if (kind == ToastNotifier.ToastKind.Error)
            {
                GlyphBadge.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 90, 90));
            }

            Owner = host;
            WindowStartupLocation = WindowStartupLocation.Manual;

            _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
            _closeTimer.Tick += (s, e) => BeginClose();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Never steal focus from the app the user is working in.
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);

            PositionSelf();
        }

        private void PositionSelf()
        {
            try
            {
                double left, top;
                if (Owner != null && Owner.IsLoaded)
                {
                    left = Owner.Left + (Owner.ActualWidth - ActualWidth) / 2;
                    top = Owner.Top + Owner.ActualHeight - ActualHeight - 48;
                }
                else
                {
                    left = (SystemParameters.WorkArea.Width - ActualWidth) / 2 + SystemParameters.WorkArea.X;
                    top = SystemParameters.WorkArea.Bottom - ActualHeight - 40;
                }

                Left = Math.Max(8, left);
                Top = Math.Max(8, top);
            }
            catch { }
        }

        public void Show(double seconds)
        {
            Show();
            PositionSelf();

            Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
            BeginAnimation(OpacityProperty, fadeIn);

            _closeTimer.Interval = TimeSpan.FromSeconds(Math.Max(1.5, seconds));
            _closeTimer.Start();
        }

        private void BeginClose()
        {
            _closeTimer.Stop();
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(240))
            {
                DecelerationRatio = 0.4
            };
            fadeOut.Completed += (s, e) => { try { Close(); } catch { } };
            BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}
