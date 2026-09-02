using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RamaverseStudio.Services
{
    /// <summary>
    /// Global Windows In-Game Hotkey Manager via Win32 RegisterHotKey.
    /// Captures Ctrl+M and other shortcut commands while playing fullscreen games without requiring focus.
    /// </summary>
    public class GlobalHotkeyManager : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_NOREPEAT = 0x4000;

        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID_MARKER = 9001;
        private const int HOTKEY_ID_REPLAY = 9002;

        private IntPtr _hWnd;
        private HwndSource? _source;
        private Action? _onMarkerHotkeyPressed;
        private Action? _onReplayHotkeyPressed;

        public void Initialize(Window window, Action onMarkerHotkeyPressed, Action? onReplayHotkeyPressed = null)
        {
            _onMarkerHotkeyPressed = onMarkerHotkeyPressed;
            _onReplayHotkeyPressed = onReplayHotkeyPressed;

            var helper = new WindowInteropHelper(window);
            _hWnd = helper.Handle;

            if (_hWnd != IntPtr.Zero)
            {
                HookWindow();
            }
            else
            {
                window.SourceInitialized += (s, e) =>
                {
                    _hWnd = new WindowInteropHelper(window).Handle;
                    HookWindow();
                };
            }
        }

        private void HookWindow()
        {
            if (_hWnd == IntPtr.Zero) return;

            _source = HwndSource.FromHwnd(_hWnd);
            _source?.AddHook(HwndHook);

            // Register Ctrl + M (0x4D = 'M') with MOD_NOREPEAT
            RegisterHotKey(_hWnd, HOTKEY_ID_MARKER, MOD_CONTROL | MOD_NOREPEAT, 0x4D);

            // Register Ctrl + Shift + F10 (0x79 = F10) for Instant Replay Clip
            RegisterHotKey(_hWnd, HOTKEY_ID_REPLAY, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, 0x79);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID_MARKER)
                {
                    _onMarkerHotkeyPressed?.Invoke();
                    handled = true;
                }
                else if (id == HOTKEY_ID_REPLAY)
                {
                    _onReplayHotkeyPressed?.Invoke();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_hWnd != IntPtr.Zero)
            {
                try
                {
                    UnregisterHotKey(_hWnd, HOTKEY_ID_MARKER);
                    UnregisterHotKey(_hWnd, HOTKEY_ID_REPLAY);
                }
                catch { }
                _source?.RemoveHook(HwndHook);
                _hWnd = IntPtr.Zero;
            }
        }
    }
}
