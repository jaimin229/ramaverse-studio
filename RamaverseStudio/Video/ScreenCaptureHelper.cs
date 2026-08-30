using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace RamaverseStudio.Video
{
    public class ScreenInfo
    {
        public int Index { get; set; }
        public string Name { get; set; } = "Display";
        public Rectangle Bounds { get; set; }
        public bool IsPrimary { get; set; }
    }

    public static class ScreenCaptureHelper
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjectSource, int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll")]
        private static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyHeight, int istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SRCCOPY = 0x00CC0020;
        private const int CURSOR_SHOWING = 0x00000001;
        private const int DI_NORMAL = 0x0003;

        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        public static List<ScreenInfo> GetDisplays()
        {
            var list = new List<ScreenInfo>();
            int index = 0;

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdcMon, ref RECT rc, IntPtr data) =>
            {
                list.Add(new ScreenInfo
                {
                    Index = index,
                    Name = $"Display {index + 1} ({rc.Right - rc.Left}x{rc.Bottom - rc.Top})",
                    Bounds = new Rectangle(rc.Left, rc.Top, rc.Right - rc.Left, rc.Bottom - rc.Top),
                    IsPrimary = rc.Left == 0 && rc.Top == 0
                });
                index++;
                return true;
            }, IntPtr.Zero);

            if (list.Count == 0)
            {
                int w = GetSystemMetrics(SM_CXSCREEN);
                int h = GetSystemMetrics(SM_CYSCREEN);
                list.Add(new ScreenInfo
                {
                    Index = 0,
                    Name = $"Primary Display ({w}x{h})",
                    Bounds = new Rectangle(0, 0, w, h),
                    IsPrimary = true
                });
            }

            return list;
        }

        public static Bitmap? CaptureScreen(int displayIndex, bool captureCursor = true)
        {
            var displays = GetDisplays();
            Rectangle bounds;

            if (displayIndex >= 0 && displayIndex < displays.Count)
            {
                bounds = displays[displayIndex].Bounds;
            }
            else
            {
                bounds = new Rectangle(
                    GetSystemMetrics(SM_XVIRTUALSCREEN),
                    GetSystemMetrics(SM_YVIRTUALSCREEN),
                    GetSystemMetrics(SM_CXVIRTUALSCREEN),
                    GetSystemMetrics(SM_CYVIRTUALSCREEN));
                if (bounds.Width == 0 || bounds.Height == 0)
                {
                    bounds = new Rectangle(0, 0, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));
                }
            }

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return null;

            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                IntPtr hdcDest = g.GetHdc();
                IntPtr hdcSrc = GetWindowDC(IntPtr.Zero);

                BitBlt(hdcDest, 0, 0, bounds.Width, bounds.Height, hdcSrc, bounds.X, bounds.Y, SRCCOPY);

                if (captureCursor)
                {
                    try
                    {
                        CURSORINFO pci;
                        pci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));
                        if (GetCursorInfo(out pci) && pci.flags == CURSOR_SHOWING)
                        {
                            int curX = pci.ptScreenPos.x - bounds.X;
                            int curY = pci.ptScreenPos.y - bounds.Y;
                            DrawIconEx(hdcDest, curX, curY, pci.hCursor, 0, 0, 0, IntPtr.Zero, DI_NORMAL);
                        }
                    }
                    catch { }
                }

                g.ReleaseHdc(hdcDest);
                ReleaseDC(IntPtr.Zero, hdcSrc);
            }

            return bitmap;
        }
    }
}
