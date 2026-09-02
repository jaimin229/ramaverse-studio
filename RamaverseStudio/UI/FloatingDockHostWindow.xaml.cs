using System;
using System.ComponentModel;
using System.Windows;

namespace RamaverseStudio.UI
{
    public partial class FloatingDockHostWindow : Window
    {
        private readonly UIElement _hostedElement;
        private readonly Action<UIElement> _reDockCallback;
        private bool _isReDocking = false;

        public FloatingDockHostWindow(string title, UIElement content, Action<UIElement> reDockCallback)
        {
            InitializeComponent();
            TxtDockTitle.Text = title;
            Title = $"{title} - Ramaverse Studio";
            _hostedElement = content;
            _reDockCallback = reDockCallback;

            DockContentHost.Content = _hostedElement;
        }

        private void OnReDockClicked(object sender, RoutedEventArgs e)
        {
            ReDock();
        }

        private void ReDock()
        {
            if (_isReDocking) return;
            _isReDocking = true;
            DockContentHost.Content = null;
            _reDockCallback?.Invoke(_hostedElement);
            Close();
        }

        private void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            if (!_isReDocking)
            {
                _isReDocking = true;
                DockContentHost.Content = null;
                _reDockCallback?.Invoke(_hostedElement);
            }
        }
    }
}
