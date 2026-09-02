using System;
using System.Windows;
using System.Windows.Threading;
using RamaverseStudio.UI;

namespace RamaverseStudio.Services
{
    /// <summary>
    /// Desktop notification helper — small always-on-top toast windows that
    /// fade automatically. Replaces modal MessageBox popups that steal focus
    /// while the user is live.
    /// </summary>
    public static class ToastNotifier
    {
        public enum ToastKind
        {
            Info,
            Success,
            Warning,
            Error
        }

        private static Window? _host;
        private static Dispatcher? _dispatcher;

        public static void BindHost(Window owner)
        {
            _host = owner;
            _dispatcher = owner.Dispatcher;
        }

        /// <summary>
        /// Shows a toast. Safe to call from any thread.
        /// </summary>
        public static void Show(string message, ToastKind kind = ToastKind.Info, double seconds = 3.5)
        {
            var dispatcher = _dispatcher ?? Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            dispatcher.BeginInvoke(() =>
            {
                try
                {
                    var toast = new ToastWindow(message, kind, _host)
                    {
                        ShowActivated = false
                    };
                    toast.Show(seconds);
                }
                catch { }
            });
        }

        /// <summary>
        /// Blocking alert for cases where the user truly must acknowledge
        /// (destructive actions). Uses the owner window when available.
        /// </summary>
        public static void Alert(string message, string title, bool isError = false)
        {
            var dispatcher = _dispatcher ?? Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            dispatcher.Invoke(() =>
            {
                var owner = _host ?? Application.Current?.MainWindow;
                if (owner != null)
                {
                    MessageBox.Show(owner, message, title, MessageBoxButton.OK, isError ? MessageBoxImage.Error : MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(message, title, MessageBoxButton.OK, isError ? MessageBoxImage.Error : MessageBoxImage.Information);
                }
            });
        }
    }
}
