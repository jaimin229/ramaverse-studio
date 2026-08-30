using System;
using System.IO;
using System.Windows;
using RamaverseStudio.Output;

namespace RamaverseStudio.UI
{
    public partial class RecordingCompletedDialog : Window
    {
        private readonly FFmpegRecordingEngine _recEngine;

        public RecordingCompletedDialog(FFmpegRecordingEngine recEngine, TimeSpan duration, double sizeMb)
        {
            InitializeComponent();
            _recEngine = recEngine;

            string filePath = recEngine.CurrentOutputFilePath;
            TxtFileName.Text = Path.GetFileName(filePath);
            TxtDuration.Text = duration.ToString(@"hh\:mm\:ss");
            TxtFileSize.Text = $"{sizeMb:F1} MB";
        }

        private void OnPlayVideoClicked(object sender, RoutedEventArgs e)
        {
            _recEngine.OpenOutputFile();
        }

        private void OnOpenFolderClicked(object sender, RoutedEventArgs e)
        {
            _recEngine.OpenOutputFolder();
        }

        private void OnDoneClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
