using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace RamaverseStudio
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Global Exception Handlers
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogCrash(e.Exception);
            MessageBox.Show(
                $"Ramaverse Studio encountered an unexpected issue:\n\n{e.Exception.Message}\n\nA crash log was saved to %APPDATA%\\RamaverseStudio\\crash_logs.",
                "Ramaverse Studio — Recovered from Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            e.Handled = true; // Prevent process termination
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogCrash(ex);
            }
        }

        private static void LogCrash(Exception ex)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RamaverseStudio", "crash_logs");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, $"crash_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
                File.WriteAllText(file, $"{DateTime.Now:O}\nVersion: 1.0.0\nException: {ex}\nStackTrace:\n{ex.StackTrace}");
            }
            catch { }
        }
    }
}
