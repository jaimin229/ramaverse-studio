using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using RamaverseStudio.Models;

namespace RamaverseStudio.UI.Gizmo
{
    /// <summary>
    /// Zero-lag, zero-GC direct-manipulation transform gizmo overlay using DrawingVisual.
    /// Provides 8 bounding box resize handles, rotation pip stalk, Alt+drag crop bars,
    /// and golden ratio / center alignment guides.
    /// </summary>
    public class FastTransformGizmoOverlay : FrameworkElement
    {
        private readonly VisualCollection _visuals;
        private readonly DrawingVisual _drawingVisual;
        private readonly SnapEngine _snapEngine = new();
        private readonly List<ActiveGuideLine> _activeGuides = new();

        private readonly List<SourceItem> _selectedSources = new();
        private int _canvasWidth = 1920;
        private int _canvasHeight = 1080;

        private bool _isInteracting = false;
        private GizmoHandleType _activeHandle = GizmoHandleType.None;
        private Point _mouseStartScreen;
        private Point _mouseStartCanvas;

        private struct SourceSnapshot
        {
            public double X, Y, W, H, Rot;
            public double CropL, CropT, CropR, CropB;
        }
        private readonly List<SourceSnapshot> _initialSnapshots = new();
        private Rect _initialGroupBounds;

        private static readonly SolidColorBrush BoxOutlineBrush = CreateFrozenBrush(Color.FromArgb(255, 124, 58, 237));
        private static readonly SolidColorBrush BoxFillBrush = CreateFrozenBrush(Color.FromArgb(20, 124, 58, 237));
        private static readonly SolidColorBrush HandleFillBrush = CreateFrozenBrush(Colors.White);
        private static readonly SolidColorBrush HandleStrokeBrush = CreateFrozenBrush(Color.FromArgb(255, 20, 14, 38));
        private static readonly SolidColorBrush RotatePipBrush = CreateFrozenBrush(Color.FromArgb(255, 245, 158, 11));
        private static readonly SolidColorBrush CropBarBrush = CreateFrozenBrush(Color.FromArgb(255, 16, 185, 129));
        private static readonly SolidColorBrush GuideCenterBrush = CreateFrozenBrush(Color.FromArgb(255, 239, 68, 68));
        private static readonly SolidColorBrush GuideGoldenBrush = CreateFrozenBrush(Color.FromArgb(255, 234, 179, 8));
        private static readonly SolidColorBrush GuideDefaultBrush = CreateFrozenBrush(Color.FromArgb(220, 6, 182, 212));

        private static readonly Pen BoxPen = CreateFrozenPen(BoxOutlineBrush, 1.5);
        private static readonly Pen HandlePen = CreateFrozenPen(HandleStrokeBrush, 1.5);
        private static readonly Pen RotateStalkPen = CreateFrozenPen(RotatePipBrush, 1.5);
        private static readonly Pen CropBarPen = CreateFrozenPen(CropBarBrush, 3.0);
        private static readonly Pen GuideCenterPen = CreateFrozenDashedPen(GuideCenterBrush, 1.0, new[] { 4.0, 4.0 });
        private static readonly Pen GuideGoldenPen = CreateFrozenDashedPen(GuideGoldenBrush, 1.0, new[] { 6.0, 3.0 });
        private static readonly Pen GuideDefaultPen = CreateFrozenDashedPen(GuideDefaultBrush, 1.0, new[] { 3.0, 3.0 });

        private static readonly Typeface BadgeTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        public event Action? TransformStarted;
        public event Action? TransformChanged;
        public event Action? TransformCompleted;

        public FastTransformGizmoOverlay()
        {
            _visuals = new VisualCollection(this);
            _drawingVisual = new DrawingVisual();
            _visuals.Add(_drawingVisual);

            ClipToBounds = true;
            Focusable = true;
            SizeChanged += (s, e) => InvalidateGizmo();
        }

        protected override int VisualChildrenCount => _visuals.Count;
        protected override Visual GetVisualChild(int index) => _visuals[index];

        public void SetCanvasResolution(int w, int h)
        {
            _canvasWidth = Math.Max(1, w);
            _canvasHeight = Math.Max(1, h);
            InvalidateGizmo();
        }

        public void SetSelection(IEnumerable<SourceItem> sources)
        {
            _selectedSources.Clear();
            _selectedSources.AddRange(sources);
            InvalidateGizmo();
        }

        private bool _showSafeAreas = false;
        public bool ShowSafeAreas
        {
            get => _showSafeAreas;
            set
            {
                if (_showSafeAreas != value)
                {
                    _showSafeAreas = value;
                    InvalidateGizmo();
                }
            }
        }

        public void InvalidateGizmo()
        {
            using DrawingContext dc = _drawingVisual.RenderOpen();

            if (ActualWidth <= 0 || ActualHeight <= 0)
                return;

            double sx = ActualWidth / _canvasWidth;
            double sy = ActualHeight / _canvasHeight;

            // 0. Render SMPTE EBU Broadcast Safe Areas if enabled
            if (_showSafeAreas)
            {
                // Action Safe (93% area - 3.5% margins)
                double asMarginX = _canvasWidth * 0.035 * sx;
                double asMarginY = _canvasHeight * 0.035 * sy;
                Rect actionSafe = new(asMarginX, asMarginY, ActualWidth - asMarginX * 2, ActualHeight - asMarginY * 2);
                dc.DrawRectangle(null, GuideDefaultPen, actionSafe);

                // Title Safe (80% area - 10% margins)
                double tsMarginX = _canvasWidth * 0.10 * sx;
                double tsMarginY = _canvasHeight * 0.10 * sy;
                Rect titleSafe = new(tsMarginX, tsMarginY, ActualWidth - tsMarginX * 2, ActualHeight - tsMarginY * 2);
                dc.DrawRectangle(null, GuideGoldenPen, titleSafe);

                // Center Crosshair
                double midX = ActualWidth / 2.0;
                double midY = ActualHeight / 2.0;
                dc.DrawLine(GuideCenterPen, new Point(midX - 12, midY), new Point(midX + 12, midY));
                dc.DrawLine(GuideCenterPen, new Point(midX, midY - 12), new Point(midX, midY + 12));
            }

            if (_selectedSources.Count == 0)
                return;

            // 1. Render Smart Snapping Guides
            foreach (var guide in _activeGuides)
            {
                Pen pen = guide.Type switch
                {
                    SnapGuideType.CanvasCenter => GuideCenterPen,
                    SnapGuideType.GoldenRatio => GuideGoldenPen,
                    _ => GuideDefaultPen
                };

                Point p1 = new(guide.Start.X * sx, guide.Start.Y * sy);
                Point p2 = new(guide.End.X * sx, guide.End.Y * sy);
                dc.DrawLine(pen, p1, p2);

                FormattedText text = new(guide.Label, CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, BadgeTypeface, 10, Brushes.White, 1.0);

                double badgeX = p1.X + 8;
                double badgeY = (guide.Start.Y == guide.End.Y) ? p1.Y - 18 : 20;
                Rect badgeRect = new(badgeX - 4, badgeY - 2, text.Width + 8, text.Height + 4);

                dc.DrawRoundedRectangle(CreateFrozenBrush(Color.FromArgb(220, 15, 23, 42)), null, badgeRect, 4, 4);
                dc.DrawText(text, new Point(badgeX, badgeY));
            }

            // 2. Render Gizmo Geometry
            if (_selectedSources.Count == 1)
            {
                RenderSingleGizmo(dc, _selectedSources[0], sx, sy);
            }
            else
            {
                RenderMultiSelectGizmo(dc, sx, sy);
            }
        }

        private void RenderSingleGizmo(DrawingContext dc, SourceItem src, double sx, double sy)
        {
            if (!src.IsVisible || src.IsLocked) return;
            bool isAltDown = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);

            double cx = (src.X + src.Width / 2.0) * sx;
            double cy = (src.Y + src.Height / 2.0) * sy;
            double sw = src.Width * sx;
            double sh = src.Height * sy;

            dc.PushTransform(new RotateTransform(src.Rotation, cx, cy));

            Rect rect = new(cx - sw / 2.0, cy - sh / 2.0, sw, sh);
            dc.DrawRectangle(BoxFillBrush, BoxPen, rect);

            // Stalk & Rotation Pip
            double stalkLength = 26.0;
            Point topCenter = new(cx, rect.Top);
            Point pipCenter = new(cx, rect.Top - stalkLength);
            dc.DrawLine(RotateStalkPen, topCenter, pipCenter);
            dc.DrawEllipse(RotatePipBrush, HandlePen, pipCenter, 5.5, 5.5);

            if (isAltDown)
            {
                DrawCropBar(dc, new Point(rect.Left, rect.Top), new Point(rect.Left, rect.Bottom));
                DrawCropBar(dc, new Point(rect.Right, rect.Top), new Point(rect.Right, rect.Bottom));
                DrawCropBar(dc, new Point(rect.Left, rect.Top), new Point(rect.Right, rect.Top));
                DrawCropBar(dc, new Point(rect.Left, rect.Bottom), new Point(rect.Right, rect.Bottom));
            }
            else
            {
                DrawHandle(dc, rect.TopLeft);
                DrawHandle(dc, new Point(cx, rect.Top));
                DrawHandle(dc, rect.TopRight);
                DrawHandle(dc, new Point(rect.Right, cy));
                DrawHandle(dc, rect.BottomRight);
                DrawHandle(dc, new Point(cx, rect.Bottom));
                DrawHandle(dc, rect.BottomLeft);
                DrawHandle(dc, new Point(rect.Left, cy));
            }

            dc.Pop();
        }

        private void RenderMultiSelectGizmo(DrawingContext dc, double sx, double sy)
        {
            Rect group = CalculateGroupBounds();
            Rect sGroup = new(group.X * sx, group.Y * sy, group.Width * sx, group.Height * sy);

            dc.DrawRectangle(BoxFillBrush, BoxPen, sGroup);
            DrawHandle(dc, sGroup.TopLeft);
            DrawHandle(dc, new Point(sGroup.Left + sGroup.Width / 2.0, sGroup.Top));
            DrawHandle(dc, sGroup.TopRight);
            DrawHandle(dc, new Point(sGroup.Right, sGroup.Top + sGroup.Height / 2.0));
            DrawHandle(dc, sGroup.BottomRight);
            DrawHandle(dc, new Point(sGroup.Left + sGroup.Width / 2.0, sGroup.Bottom));
            DrawHandle(dc, sGroup.BottomLeft);
            DrawHandle(dc, new Point(sGroup.Left, sGroup.Top + sGroup.Height / 2.0));
        }

        private static void DrawHandle(DrawingContext dc, Point p) => dc.DrawRectangle(HandleFillBrush, HandlePen, new Rect(p.X - 4.5, p.Y - 4.5, 9, 9));
        private static void DrawCropBar(DrawingContext dc, Point p1, Point p2) => dc.DrawLine(CropBarPen, p1, p2);

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || _selectedSources.Count == 0) return;

            Point mouseScreen = e.GetPosition(this);
            double sx = ActualWidth / _canvasWidth;
            double sy = ActualHeight / _canvasHeight;

            _activeHandle = HitTestHandle(mouseScreen, sx, sy);
            if (_activeHandle == GizmoHandleType.None) return;

            _isInteracting = true;
            _mouseStartScreen = mouseScreen;
            _mouseStartCanvas = new Point(mouseScreen.X / sx, mouseScreen.Y / sy);

            _initialSnapshots.Clear();
            foreach (var src in _selectedSources)
            {
                _initialSnapshots.Add(new SourceSnapshot
                {
                    X = src.X, Y = src.Y, W = src.Width, H = src.Height, Rot = src.Rotation,
                    CropL = src.CropLeft, CropT = src.CropTop, CropR = src.CropRight, CropB = src.CropBottom
                });
            }
            _initialGroupBounds = CalculateGroupBounds();

            CaptureMouse();
            TransformStarted?.Invoke();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Point mouseScreen = e.GetPosition(this);
            double sx = ActualWidth / _canvasWidth;
            double sy = ActualHeight / _canvasHeight;

            if (!_isInteracting)
            {
                GizmoHandleType hoverHandle = HitTestHandle(mouseScreen, sx, sy);
                UpdateCursor(hoverHandle);
                return;
            }

            Point curCanvas = new(mouseScreen.X / sx, mouseScreen.Y / sy);
            double dxCanvas = curCanvas.X - _mouseStartCanvas.X;
            double dyCanvas = curCanvas.Y - _mouseStartCanvas.Y;

            bool isShiftDown = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            bool isAltDown = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);

            if (_activeHandle == GizmoHandleType.Body)
            {
                double targetX = _initialGroupBounds.X + dxCanvas;
                double targetY = _initialGroupBounds.Y + dyCanvas;

                _snapEngine.CalculateSnaps(targetX, targetY, _initialGroupBounds.Width, _initialGroupBounds.Height,
                    _canvasWidth, _canvasHeight, sx, sy, null,
                    out double snappedX, out double snappedY, _activeGuides);

                double appliedDx = snappedX - _initialGroupBounds.X;
                double appliedDy = snappedY - _initialGroupBounds.Y;

                for (int i = 0; i < _selectedSources.Count; i++)
                {
                    _selectedSources[i].X = Math.Round(_initialSnapshots[i].X + appliedDx);
                    _selectedSources[i].Y = Math.Round(_initialSnapshots[i].Y + appliedDy);
                }
            }
            else if (_activeHandle == GizmoHandleType.RotatePip && _selectedSources.Count == 1)
            {
                var src = _selectedSources[0];
                var snap = _initialSnapshots[0];
                Point centerScreen = new((snap.X + snap.W / 2.0) * sx, (snap.Y + snap.H / 2.0) * sy);

                double angleRad = Math.Atan2(mouseScreen.Y - centerScreen.Y, mouseScreen.X - centerScreen.X) + Math.PI / 2.0;
                double angleDeg = angleRad * 180.0 / Math.PI;

                if (isShiftDown) angleDeg = Math.Round(angleDeg / 15.0) * 15.0;
                src.Rotation = Math.Round((angleDeg % 360.0 + 360.0) % 360.0);
            }
            else if (IsCropHandle(_activeHandle) && _selectedSources.Count == 1)
            {
                var src = _selectedSources[0];
                var snap = _initialSnapshots[0];

                switch (_activeHandle)
                {
                    case GizmoHandleType.CropLeft: src.CropLeft = Math.Max(0, snap.CropL + dxCanvas); break;
                    case GizmoHandleType.CropRight: src.CropRight = Math.Max(0, snap.CropR - dxCanvas); break;
                    case GizmoHandleType.CropTop: src.CropTop = Math.Max(0, snap.CropT + dyCanvas); break;
                    case GizmoHandleType.CropBottom: src.CropBottom = Math.Max(0, snap.CropB - dyCanvas); break;
                }
            }
            else if (_selectedSources.Count == 1)
            {
                ApplyRotatedResize(_selectedSources[0], _initialSnapshots[0], _activeHandle, curCanvas, isShiftDown, isAltDown);
            }

            InvalidateGizmo();
            TransformChanged?.Invoke();
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (_isInteracting)
            {
                _isInteracting = false;
                _activeHandle = GizmoHandleType.None;
                _activeGuides.Clear();
                ReleaseMouseCapture();
                InvalidateGizmo();
                TransformCompleted?.Invoke();
            }
        }

        private static void ApplyRotatedResize(SourceItem src, SourceSnapshot snap, GizmoHandleType handle, Point curMouseCanvas, bool preserveAspect, bool scaleFromCenter)
        {
            double rad = snap.Rot * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            Point center0 = new(snap.X + snap.W / 2.0, snap.Y + snap.H / 2.0);

            Point oppLocal = handle switch
            {
                GizmoHandleType.SE => new Point(-snap.W / 2.0, -snap.H / 2.0),
                GizmoHandleType.NW => new Point(snap.W / 2.0, snap.H / 2.0),
                GizmoHandleType.NE => new Point(-snap.W / 2.0, snap.H / 2.0),
                GizmoHandleType.SW => new Point(snap.W / 2.0, -snap.H / 2.0),
                GizmoHandleType.E => new Point(-snap.W / 2.0, 0),
                GizmoHandleType.W => new Point(snap.W / 2.0, 0),
                GizmoHandleType.S => new Point(0, -snap.H / 2.0),
                GizmoHandleType.N => new Point(0, snap.H / 2.0),
                _ => new Point(0, 0)
            };

            Point oppCanvas = new(
                center0.X + (oppLocal.X * cos - oppLocal.Y * sin),
                center0.Y + (oppLocal.X * sin + oppLocal.Y * cos)
            );

            double dx = curMouseCanvas.X - oppCanvas.X;
            double dy = curMouseCanvas.Y - oppCanvas.Y;

            double localU = dx * cos + dy * sin;
            double localV = -dx * sin + dy * cos;

            double newW = snap.W;
            double newH = snap.H;

            switch (handle)
            {
                case GizmoHandleType.SE: newW = Math.Max(20, localU); newH = Math.Max(20, localV); break;
                case GizmoHandleType.NW: newW = Math.Max(20, -localU); newH = Math.Max(20, -localV); break;
                case GizmoHandleType.NE: newW = Math.Max(20, localU); newH = Math.Max(20, -localV); break;
                case GizmoHandleType.SW: newW = Math.Max(20, -localU); newH = Math.Max(20, localV); break;
                case GizmoHandleType.E: newW = Math.Max(20, localU); break;
                case GizmoHandleType.W: newW = Math.Max(20, -localU); break;
                case GizmoHandleType.S: newH = Math.Max(20, localV); break;
                case GizmoHandleType.N: newH = Math.Max(20, -localV); break;
            }

            if (preserveAspect)
            {
                double aspect = snap.W / Math.Max(1.0, snap.H);
                if (newW / Math.Max(1.0, newH) > aspect) newH = newW / aspect;
                else newW = newH * aspect;
            }

            Point newCenterLocal = handle switch
            {
                GizmoHandleType.SE => new Point(newW / 2.0, newH / 2.0),
                GizmoHandleType.NW => new Point(-newW / 2.0, -newH / 2.0),
                GizmoHandleType.NE => new Point(newW / 2.0, -newH / 2.0),
                GizmoHandleType.SW => new Point(-newW / 2.0, newH / 2.0),
                GizmoHandleType.E => new Point(newW / 2.0, 0),
                GizmoHandleType.W => new Point(-newW / 2.0, 0),
                GizmoHandleType.S => new Point(0, newH / 2.0),
                GizmoHandleType.N => new Point(0, -newH / 2.0),
                _ => new Point(0, 0)
            };

            Point newCenterCanvas = new(
                oppCanvas.X + (newCenterLocal.X * cos - newCenterLocal.Y * sin),
                oppCanvas.Y + (newCenterLocal.X * sin + newCenterLocal.Y * cos)
            );

            src.Width = Math.Round(newW);
            src.Height = Math.Round(newH);
            src.X = Math.Round(newCenterCanvas.X - newW / 2.0);
            src.Y = Math.Round(newCenterCanvas.Y - newH / 2.0);
        }

        private GizmoHandleType HitTestHandle(Point screenPt, double sx, double sy)
        {
            if (_selectedSources.Count == 0) return GizmoHandleType.None;
            bool isAltDown = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);

            if (_selectedSources.Count == 1)
            {
                var src = _selectedSources[0];
                double cx = (src.X + src.Width / 2.0) * sx;
                double cy = (src.Y + src.Height / 2.0) * sy;
                double rad = -src.Rotation * Math.PI / 180.0;

                double dx = screenPt.X - cx;
                double dy = screenPt.Y - cy;
                Point unrotated = new(
                    cx + (dx * Math.Cos(rad) - dy * Math.Sin(rad)),
                    cy + (dx * Math.Sin(rad) + dy * Math.Cos(rad))
                );

                Rect box = new(cx - src.Width * sx / 2.0, cy - src.Height * sy / 2.0, src.Width * sx, src.Height * sy);

                Point pip = new(cx, box.Top - 26.0);
                if ((unrotated - pip).Length <= 10.0) return GizmoHandleType.RotatePip;

                if (isAltDown)
                {
                    if (Math.Abs(unrotated.X - box.Left) <= 6 && unrotated.Y >= box.Top && unrotated.Y <= box.Bottom) return GizmoHandleType.CropLeft;
                    if (Math.Abs(unrotated.X - box.Right) <= 6 && unrotated.Y >= box.Top && unrotated.Y <= box.Bottom) return GizmoHandleType.CropRight;
                    if (Math.Abs(unrotated.Y - box.Top) <= 6 && unrotated.X >= box.Left && unrotated.X <= box.Right) return GizmoHandleType.CropTop;
                    if (Math.Abs(unrotated.Y - box.Bottom) <= 6 && unrotated.X >= box.Left && unrotated.X <= box.Right) return GizmoHandleType.CropBottom;
                }
                else
                {
                    if (HitHandle(unrotated, box.TopLeft)) return GizmoHandleType.NW;
                    if (HitHandle(unrotated, box.TopRight)) return GizmoHandleType.NE;
                    if (HitHandle(unrotated, box.BottomLeft)) return GizmoHandleType.SW;
                    if (HitHandle(unrotated, box.BottomRight)) return GizmoHandleType.SE;
                    if (HitHandle(unrotated, new Point(cx, box.Top))) return GizmoHandleType.N;
                    if (HitHandle(unrotated, new Point(cx, box.Bottom))) return GizmoHandleType.S;
                    if (HitHandle(unrotated, new Point(box.Left, cy))) return GizmoHandleType.W;
                    if (HitHandle(unrotated, new Point(box.Right, cy))) return GizmoHandleType.E;
                }

                if (box.Contains(unrotated)) return GizmoHandleType.Body;
            }
            else
            {
                Rect group = CalculateGroupBounds();
                Rect sGroup = new(group.X * sx, group.Y * sy, group.Width * sx, group.Height * sy);

                if (HitHandle(screenPt, sGroup.TopLeft)) return GizmoHandleType.NW;
                if (HitHandle(screenPt, sGroup.TopRight)) return GizmoHandleType.NE;
                if (HitHandle(screenPt, sGroup.BottomLeft)) return GizmoHandleType.SW;
                if (HitHandle(screenPt, sGroup.BottomRight)) return GizmoHandleType.SE;
                if (sGroup.Contains(screenPt)) return GizmoHandleType.Body;
            }

            return GizmoHandleType.None;
        }

        private static bool HitHandle(Point p, Point target) => (p - target).Length <= 8.0;
        private static bool IsCropHandle(GizmoHandleType t) => t is GizmoHandleType.CropLeft or GizmoHandleType.CropRight or GizmoHandleType.CropTop or GizmoHandleType.CropBottom;

        private void UpdateCursor(GizmoHandleType handle)
        {
            Cursor = handle switch
            {
                GizmoHandleType.Body => Cursors.SizeAll,
                GizmoHandleType.RotatePip => Cursors.Hand,
                GizmoHandleType.NW or GizmoHandleType.SE => Cursors.SizeNWSE,
                GizmoHandleType.NE or GizmoHandleType.SW => Cursors.SizeNESW,
                GizmoHandleType.N or GizmoHandleType.S or GizmoHandleType.CropTop or GizmoHandleType.CropBottom => Cursors.SizeNS,
                GizmoHandleType.W or GizmoHandleType.E or GizmoHandleType.CropLeft or GizmoHandleType.CropRight => Cursors.SizeWE,
                _ => Cursors.Arrow
            };
        }

        private Rect CalculateGroupBounds()
        {
            if (_selectedSources.Count == 0) return Rect.Empty;
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var s in _selectedSources)
            {
                minX = Math.Min(minX, s.X);
                minY = Math.Min(minY, s.Y);
                maxX = Math.Max(maxX, s.X + s.Width);
                maxY = Math.Max(maxY, s.Y + s.Height);
            }

            return new Rect(minX, minY, Math.Max(10, maxX - minX), Math.Max(10, maxY - minY));
        }

        private static SolidColorBrush CreateFrozenBrush(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        private static Pen CreateFrozenPen(Brush b, double thickness) { var p = new Pen(b, thickness); p.Freeze(); return p; }
        private static Pen CreateFrozenDashedPen(Brush b, double thickness, double[] dashes) { var p = new Pen(b, thickness) { DashStyle = new DashStyle(dashes, 0) }; p.Freeze(); return p; }
    }
}
