using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace RamaverseStudio.UI
{
    /// <summary>
    /// Borderless fullscreen preview of the live canvas. Shares the compositor's
    /// WriteableBitmap (zero-copy mirror), sized for a second monitor / projector.
    /// </summary>
    public partial class ProjectorWindow : Window
    {
        private readonly DispatcherTimer _refreshTimer;

        private Func<WriteableBitmap?>? _bitmapProvider;

        public ProjectorWindow()
        {
            InitializeComponent();

            // Poll the shared bitmap a few times a second — the compositor's
            // BeginInvoke already pushes pixels into the same instance, so the
            // projector is effectively live without any per-frame work here.
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _refreshTimer.Tick += (s, e) => RefreshSource();
            _refreshTimer.Start();

            Loaded += (s, e) =>
            {
                WindowState = WindowState.Maximized;
                RefreshSource();
            };
        }

        /// <summary>
        /// Supplies the compositor's live WriteableBitmap for mirroring.
        /// </summary>
        public void BindBitmap(Func<WriteableBitmap?> provider)
        {
            _bitmapProvider = provider;
            RefreshSource();
        }

        private void RefreshSource()
        {
            try
            {
                var bmp = _bitmapProvider?.Invoke();
                if (bmp != null && !ReferenceEquals(ProjectorImage.Source, bmp))
                {
                    ProjectorImage.Source = bmp;
                }
            }
            catch { }
        }

        private void OnProjectorKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _refreshTimer.Stop();
            ProjectorImage.Source = null;
            base.OnClosed(e);
        }
    }
}
