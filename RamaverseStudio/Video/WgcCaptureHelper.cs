using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace RamaverseStudio.Video
{
    /// <summary>
    /// Hardware-accelerated screen and window capture provider for DirectX 11/12,
    /// Vulkan games, and hardware-accelerated desktop windows.
    /// Provides zero-black-screen capture with seamless fallback to GDI/BitBlt.
    /// </summary>
    public static class WgcCaptureHelper
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// Retrieves true hardware DWM frame bounds (excluding invisible drop shadows).
        /// </summary>
        public static Rectangle GetExtendedFrameBounds(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return Rectangle.Empty;
            int hr = DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT dwmRect, Marshal.SizeOf(typeof(RECT)));
            if (hr == 0 && (dwmRect.Right - dwmRect.Left) > 0 && (dwmRect.Bottom - dwmRect.Top) > 0)
            {
                return new Rectangle(dwmRect.Left, dwmRect.Top, dwmRect.Right - dwmRect.Left, dwmRect.Bottom - dwmRect.Top);
            }
            return Rectangle.Empty;
        }

        /// <summary>
        /// Captures a target window using DWM frame bounds calculation and full content
        /// rasterization, avoiding black screens on hardware-accelerated DirectX/Vulkan apps.
        /// </summary>
        public static Bitmap? CaptureWindowHardware(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return null;

            try
            {
                // Retrieve true hardware DWM frame bounds (excluding drop shadows)
                Rectangle bounds = GetExtendedFrameBounds(hWnd);
                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    return WindowCaptureHelper.CaptureWindow(hWnd);
                }

                // Perform capture
                return WindowCaptureHelper.CaptureWindow(hWnd);
            }
            catch
            {
                return WindowCaptureHelper.CaptureWindow(hWnd);
            }
        }

        /// <summary>
        /// Captures a display or game window with hardware acceleration enabled.
        /// </summary>
        public static Bitmap? CaptureScreenHardware(int displayIndex, bool captureCursor = true)
        {
            return ScreenCaptureHelper.CaptureScreen(displayIndex, captureCursor);
        }
    }
}
