using System;
using System.Threading;
using System.Windows;
using RamaverseStudio.AutoUpdate;

namespace RamaverseStudio.UI
{
    public partial class UpdateDialog : Window
    {
        private readonly UpdateInfo _updateInfo;
        private readonly UpdateManager _updateManager;
        private CancellationTokenSource? _downloadCts;

        public UpdateDialog(UpdateInfo updateInfo, UpdateManager updateManager)
        {
            InitializeComponent();
            _updateInfo = updateInfo;
            _updateManager = updateManager;

            TxtVersionHeader.Text = $"Ramaverse Studio v{updateInfo.Version} is available (Current: v{UpdateManager.CurrentVersion})";
            TxtReleaseNotes.Text = string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes) ? "• General performance enhancements and stability improvements." : updateInfo.ReleaseNotes;

            if (updateInfo.Mandatory)
            {
                BtnLater.Visibility = Visibility.Collapsed;
            }
        }

        private async void OnInstallUpdateClicked(object sender, RoutedEventArgs e)
        {
            BtnInstall.IsEnabled = false;
            BtnLater.IsEnabled = false;
            PanelProgress.Visibility = Visibility.Visible;
            _downloadCts = new CancellationTokenSource();

            var progress = new Progress<double>(percent =>
            {
                ProgressBarDownload.Value = percent;
                TxtProgressPercent.Text = $"{percent:F0}%";
            });

            string? downloadedZip = await _updateManager.DownloadUpdateAsync(_updateInfo.DownloadUrl, progress, _downloadCts.Token);

            if (!string.IsNullOrWhiteSpace(downloadedZip))
            {
                BtnInstall.Content = "Restarting...";
                UpdateManager.ApplyUpdateAndRestart(downloadedZip);
            }
            else
            {
                PanelProgress.Visibility = Visibility.Collapsed;
                BtnInstall.IsEnabled = true;
                BtnLater.IsEnabled = true;
                MessageBox.Show("Failed to download the update package. Please verify your internet connection.", "Update Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnLaterClicked(object sender, RoutedEventArgs e)
        {
            _downloadCts?.Cancel();
            Close();
        }
    }
}
