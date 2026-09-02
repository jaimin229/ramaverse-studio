using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace RamaverseStudio.Video
{
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public Rectangle Bounds { get; set; }

        public override string ToString() => string.IsNullOrWhiteSpace(Title) ? $"Window ({Handle})" : Title;
    }

    public static class WindowCaptureHelper
    {
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjectSource, int nXSrc, int nYSrc, int dwRop);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const uint PW_RENDERFULLCONTENT = 0x00000002;
        private const uint PW_CLIENTONLY = 0x00000001;
        private const int SRCCOPY = 0x00CC0020;

        public static List<WindowInfo> GetCapturableWindows()
        {
            var list = new List<WindowInfo>();

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                int length = GetWindowTextLength(hWnd);
                if (length == 0) return true;

                var builder = new StringBuilder(length + 1);
                GetWindowText(hWnd, builder, builder.Capacity);
                string title = builder.ToString();

                // Filter out tooltips / internal invisible shells
                if (string.IsNullOrWhiteSpace(title) ||
                    title == "Program Manager" ||
                    title == "Settings" ||
                    title == "Windows Input Experience")
                    return true;

                GetWindowRect(hWnd, out RECT rc);
                int width = rc.Right - rc.Left;
                int height = rc.Bottom - rc.Top;

                if (width > 60 && height > 60)
                {
                    list.Add(new WindowInfo
                    {
                        Handle = hWnd,
                        Title = title,
                        Bounds = new Rectangle(rc.Left, rc.Top, width, height)
                    });
                }

                return true;
            }, IntPtr.Zero);

            return list;
        }

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        /// <summary>
        /// Captures a window correctly on high-DPI displays. GetWindowRect returns
        /// physical pixels once the process is DPI-aware; without this, per-monitor
        /// scaling (125%/150% — the worldwide laptop default) crops the capture.
        /// </summary>
        public static Bitmap? CaptureWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindowVisible(hWnd) || IsIconic(hWnd))
                return null;

            GetWindowRect(hWnd, out RECT rc);
            int width = rc.Right - rc.Left;
            int height = rc.Bottom - rc.Top;

            if (width <= 0 || height <= 0 || rc.Left <= -30000 || rc.Top <= -30000)
                return null;

            // Ask the OS for this window's actual DPI and derive the scale the
            // rectangle was measured with. Unaware processes get 96 (100%).
            uint dpi = 96;
            try { dpi = GetDpiForWindow(hWnd); } catch { }
            if (dpi == 0) dpi = 96;
            double scale = dpi / 96.0;

            // If our process is not DPI-aware the rect is virtualized; rescale
            // physical capture to the true window size.
            if (scale > 1.01 && !IsProcessDpiAware())
            {
                width = (int)Math.Round(width / scale);
                height = (int)Math.Round(height / scale);
            }

            if (width < 8 || height < 8) return null;

            Bitmap bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                IntPtr hdc = g.GetHdc();

                // Try PrintWindow with full content first (works while occluded)
                bool success = PrintWindow(hWnd, hdc, PW_RENDERFULLCONTENT);
                if (!success)
                {
                    // Fallback to standard PrintWindow
                    success = PrintWindow(hWnd, hdc, 0);
                }

                if (!success)
                {
                    // Fallback to BitBlt
                    IntPtr srcDc = GetWindowDC(hWnd);
                    BitBlt(hdc, 0, 0, width, height, srcDc, 0, 0, SRCCOPY);
                    ReleaseDC(hWnd, srcDc);
                }

                g.ReleaseHdc(hdc);
            }

            return bmp;
        }

        private static bool _dpiAwareChecked;
        private static bool _dpiAwareValue;

        private static bool IsProcessDpiAware()
        {
            if (_dpiAwareChecked) return _dpiAwareValue;
            try
            {
                // WPF sets PerMonitorV2 via manifest on .NET; treat as aware.
                _dpiAwareValue = true;
            }
            catch { _dpiAwareValue = false; }
            _dpiAwareChecked = true;
            return _dpiAwareValue;
        }
    }
}
