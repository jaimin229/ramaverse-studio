using System;
using System.Windows;
using RamaverseStudio.Services;

namespace RamaverseStudio.UI
{
    /// <summary>
    /// Guided FFmpeg installation shown at first launch when the resolver
    /// cannot find FFmpeg. The whole flow is two clicks: Download → Done.
    /// </summary>
    public partial class FFmpegSetupDialog : Window
    {
        /// <summary>
        /// True when FFmpeg was installed (or was already present) and the
        /// caller can continue with recording/streaming features enabled.
        /// </summary>
        public bool SetupCompleted { get; private set; }

        public FFmpegSetupDialog()
        {
            InitializeComponent();

            if (Output.FFmpegPathResolver.IsAvailable)
            {
                // Caller should not have opened us, but be graceful.
                TxtStatusBig.Text = "FFmpeg is already installed.";
                TxtStatusDetail.Text = "You're all set — nothing to do here.";
                BtnDownload.Visibility = Visibility.Collapsed;
                SetupCompleted = true;
            }
        }

        private async void OnDownloadClicked(object sender, RoutedEventArgs e)
        {
            BtnDownload.IsEnabled = false;
            BtnSkip.IsEnabled = false;
            BtnManualHelp.IsEnabled = false;
            Progress.Visibility = Visibility.Visible;
            TxtProgressLabel.Visibility = Visibility.Visible;

            var progress = new Progress<double>(p =>
            {
                Progress.Value = p;
                TxtProgressLabel.Text = $"{p:F0}%";
            });

            FFmpegSetupService.ProgressMessage += OnProgressMessage;
            try
            {
                var result = await FFmpegSetupService.InstallFFmpegAsync(progress);
                Output.FFmpegPathResolver.InvalidateCache();

                switch (result)
                {
                    case FFmpegSetupService.SetupResult.AlreadyInstalled:
                    case FFmpegSetupService.SetupResult.DownloadedAndInstalled:
                        SetupCompleted = true;
                        TxtStatusBig.Text = "FFmpeg is ready!";
                        TxtStatusDetail.Text = "Recording and streaming are now unlocked. Enjoy!";
                        ToastNotifier.Show("FFmpeg installed — recording unlocked.", ToastNotifier.ToastKind.Success, 4);
                        BtnDownload.Content = "Done";
                        BtnDownload.IsEnabled = true;
                        BtnDownload.Click -= OnDownloadClicked;
                        BtnDownload.Click += (s2, e2) => { DialogResult = true; Close(); };
                        break;

                    case FFmpegSetupService.SetupResult.FailedNetwork:
                        TxtStatusBig.Text = "Download failed";
                        TxtStatusDetail.Text = "Check your internet connection and try again, or use the manual guide.";
                        ResetButtons();
                        break;

                    default:
                        TxtStatusBig.Text = "Installation failed";
                        TxtStatusDetail.Text = "Something went wrong extracting FFmpeg. Try the manual install guide, or contact support.";
                        ResetButtons();
                        break;
                }
            }
            finally
            {
                FFmpegSetupService.ProgressMessage -= OnProgressMessage;
            }
        }

        private void OnProgressMessage(string message)
        {
            Dispatcher.Invoke(() => TxtProgressLabel.Text = message);
        }

        private void ResetButtons()
        {
            Progress.Visibility = Visibility.Collapsed;
            BtnDownload.IsEnabled = true;
            BtnSkip.IsEnabled = true;
            BtnManualHelp.IsEnabled = true;
        }

        private void OnManualHelpClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://www.gyan.dev/ffmpeg/builds/",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void OnSkipClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
