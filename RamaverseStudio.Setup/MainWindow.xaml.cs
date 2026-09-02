using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace RamaverseStudio.Setup
{
    public partial class MainWindow : Window
    {
        private readonly string _targetDir;
        private readonly string _targetExe;

        public MainWindow()
        {
            InitializeComponent();

            _targetDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "RamaverseStudio");
            _targetExe = Path.Combine(_targetDir, "RamaverseStudio.exe");

            TxtInstallPath.Text = _targetDir;
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void OnInstallClicked(object sender, RoutedEventArgs e)
        {
            if (BtnInstall.Content.ToString() == "Launch Ramaverse")
            {
                if (File.Exists(_targetExe))
                {
                    Process.Start(new ProcessStartInfo(_targetExe) { UseShellExecute = true });
                }
                Close();
                return;
            }

            BtnInstall.IsEnabled = false;
            BtnCancel.IsEnabled = false;
            ProgressBarInstall.Visibility = Visibility.Visible;
            ProgressBarInstall.IsIndeterminate = true;
            TxtProgressDesc.Text = "Extracting Ramaverse Studio binaries...";

            try
            {
                await Task.Run(() =>
                {
                    Directory.CreateDirectory(_targetDir);

                    // Extract embedded payload
                    var assembly = Assembly.GetExecutingAssembly();
                    string resourceName = "RamaverseStudio.Setup.Payload.RamaverseStudio.exe";

                    // Fallback to searching resource names if name varies
                    var names = assembly.GetManifestResourceNames();
                    foreach (var n in names)
                    {
                        if (n.EndsWith("RamaverseStudio.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            resourceName = n;
                            break;
                        }
                    }

                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream == null)
                        {
                            throw new InvalidOperationException($"Embedded binary payload not found ({resourceName}).");
                        }

                        using (var dest = File.Create(_targetExe))
                        {
                            stream.CopyTo(dest);
                        }
                    }

                    // Create shortcuts
                    bool makeDesktop = false;
                    bool makeStart = false;
                    Dispatcher.Invoke(() =>
                    {
                        makeDesktop = ChkDesktopShortcut.IsChecked == true;
                        makeStart = ChkStartMenu.IsChecked == true;
                    });

                    if (makeDesktop)
                    {
                        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        CreateShortcut(Path.Combine(desktopPath, "Ramaverse Studio.lnk"), _targetExe, _targetDir);
                    }

                    if (makeStart)
                    {
                        string startMenuPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                            "Programs");
                        CreateShortcut(Path.Combine(startMenuPath, "Ramaverse Studio.lnk"), _targetExe, _targetDir);
                    }

                    // Register uninstaller in HKCU
                    RegisterUninstaller();
                });

                ProgressBarInstall.IsIndeterminate = false;
                ProgressBarInstall.Value = 100;
                TxtStatus.Text = "✨ Installation Complete! Ramaverse Studio is ready to use.";
                TxtProgressDesc.Text = "Successfully installed in Local AppData.";
                BtnInstall.Content = "Launch Ramaverse";
                BtnInstall.IsEnabled = true;
                BtnCancel.Content = "Close";
                BtnCancel.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ProgressBarInstall.Visibility = Visibility.Collapsed;
                TxtStatus.Text = $"Installation failed: {ex.Message}";
                TxtProgressDesc.Text = "Error during setup extraction.";
                BtnInstall.IsEnabled = true;
                BtnCancel.IsEnabled = true;
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell != null)
                    {
                        dynamic shortcut = shell.CreateShortcut(shortcutPath);
                        shortcut.TargetPath = targetPath;
                        shortcut.WorkingDirectory = workingDir;
                        shortcut.Description = "Ramaverse Studio - High Performance Creator Studio";
                        shortcut.Save();
                        return;
                    }
                }
            }
            catch { }
        }

        private void RegisterUninstaller()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\RamaverseStudio");
                if (key != null)
                {
                    key.SetValue("DisplayName", "Ramaverse Studio");
                    key.SetValue("DisplayVersion", "1.2.0");
                    key.SetValue("Publisher", "Ramaverse");
                    key.SetValue("InstallLocation", _targetDir);
                    key.SetValue("DisplayIcon", _targetExe);
                    key.SetValue("UninstallString", $"cmd.exe /c rmdir /s /q \"{_targetDir}\"");
                }
            }
            catch { }
        }
    }
}
