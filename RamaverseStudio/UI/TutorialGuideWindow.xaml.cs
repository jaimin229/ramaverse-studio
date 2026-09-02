using System.Windows;
using System.Windows.Controls;

namespace RamaverseStudio.UI
{
    public partial class TutorialGuideWindow : Window
    {
        public TutorialGuideWindow()
        {
            InitializeComponent();
        }

        private void OnTabSelected(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tag && ContentHostPanel != null)
            {
                PageQuickStart.Visibility = tag == "QuickStart" ? Visibility.Visible : Visibility.Collapsed;
                PageCapture.Visibility = tag == "Capture" ? Visibility.Visible : Visibility.Collapsed;
                PageAudio.Visibility = tag == "Audio" ? Visibility.Visible : Visibility.Collapsed;
                PageStudioMode.Visibility = tag == "StudioMode" ? Visibility.Visible : Visibility.Collapsed;
                PageRemote.Visibility = tag == "Remote" ? Visibility.Visible : Visibility.Collapsed;
                PageReplay.Visibility = tag == "Replay" ? Visibility.Visible : Visibility.Collapsed;
                PageExporter.Visibility = tag == "Exporter" ? Visibility.Visible : Visibility.Collapsed;
                PageWebChat.Visibility = tag == "WebChat" ? Visibility.Visible : Visibility.Collapsed;
                PageHotkeys.Visibility = tag == "Hotkeys" ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
