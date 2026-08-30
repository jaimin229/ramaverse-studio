using System;
using System.IO;
using System.Windows;
using RamaverseStudio.Output;

namespace RamaverseStudio.UI
{
    public partial class RecordingCompletedDialog : Window
    {
        private readonly string _filePath = "";

        public RecordingCompletedDialog(FFmpegRecordingEngine recEngine, TimeSpan duration, double sizeMb)
            : this(recEngine.CurrentOutputFilePath, duration, sizeMb)
        {
        }

        public RecordingCompletedDialog(string filePath, TimeSpan duration, double sizeMb)
        {
            InitializeComponent();
            _filePath = filePath;

            TxtFileName.Text = Path.GetFileName(filePath);
            TxtDuration.Text = duration.ToString(@"hh\:mm\:ss");
            TxtFileSize.Text = $"{sizeMb:F1} MB";
        }

        private void OnPlayVideoClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _filePath,
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }

        private void OnOpenFolderClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_filePath}\"");
                }
            }
            catch { }
        }

        private void OnDoneClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
