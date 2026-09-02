using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using RamaverseStudio.Models;

namespace RamaverseStudio.UI
{
    public partial class CanvasGizmoOverlay : UserControl
    {
        private SourceItem? _selectedSource;
        private int _canvasWidth = 1920;
        private int _canvasHeight = 1080;

        private bool _isDragging = false;
        private bool _isResizing = false;
        private bool _isRotating = false;
        private string _activeHandle = "";
        private Point _dragStartMouse;
        private double _initialX, _initialY, _initialW, _initialH, _initialRot;

        public event Action? TransformModified;
        public event Action? TransformBegun;

        public CanvasGizmoOverlay()
        {
            InitializeComponent();
            SizeChanged += (s, e) => UpdateGizmo();
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseUp;
            MouseLeave += (s, e) => OnMouseUp(s, null!);
        }

        private void OnBoundingBoxMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_selectedSource == null || _selectedSource.IsLocked) return;

            TransformBegun?.Invoke();
            _isDragging = true;
            _dragStartMouse = e.GetPosition(OverlayCanvas);
            _initialX = _selectedSource.X;
            _initialY = _selectedSource.Y;
            SelectionBox.CaptureMouse();
            e.Handled = true;
        }

        public void SetCanvasResolution(int w, int h)
        {
            _canvasWidth = Math.Max(1, w);
            _canvasHeight = Math.Max(1, h);
            UpdateGizmo();
        }

        public bool ShowSafeAreas
        {
            get => GuidesGrid.Visibility == Visibility.Visible;
            set
            {
                GuidesGrid.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                UpdateGizmo();
            }
        }

        public void SetSelectedSource(SourceItem? source)
        {
            _selectedSource = source;
            UpdateGizmo();
        }

        public void UpdateGizmo()
        {
            if (_selectedSource == null || !_selectedSource.IsVisible || _selectedSource.IsLocked)
            {
                GizmoContainer.Visibility = Visibility.Collapsed;
                return;
            }

            double viewW = ActualWidth;
            double viewH = ActualHeight;

            if (viewW <= 0 || viewH <= 0 || _canvasWidth <= 0 || _canvasHeight <= 0)
            {
                GizmoContainer.Visibility = Visibility.Collapsed;
                return;
            }

            // Canvas to Viewport scale
            double scaleX = viewW / _canvasWidth;
            double scaleY = viewH / _canvasHeight;

            double vx = _selectedSource.X * scaleX;
            double vy = _selectedSource.Y * scaleY;
            double vw = _selectedSource.Width * scaleX;
            double vh = _selectedSource.Height * scaleY;

            GizmoContainer.Visibility = Visibility.Visible;

            SelectionBox.Width = Math.Max(4, vw);
            SelectionBox.Height = Math.Max(4, vh);
            Canvas.SetLeft(SelectionBox, vx);
            Canvas.SetTop(SelectionBox, vy);

            // Rotate Handle (placed 25px above center top)
            double topCenterX = vx + vw / 2.0;
            double topCenterY = vy;
            double rotHandleY = topCenterY - 24;

            RotateLine.X1 = topCenterX;
            RotateLine.Y1 = topCenterY;
            RotateLine.X2 = topCenterX;
            RotateLine.Y2 = rotHandleY;

            Canvas.SetLeft(HandleRotate, topCenterX - HandleRotate.Width / 2.0);
            Canvas.SetTop(HandleRotate, rotHandleY - HandleRotate.Height / 2.0);

            // 8 Resize Handles
            PositionHandle(HandleNW, vx, vy);
            PositionHandle(HandleN, vx + vw / 2.0, vy);
            PositionHandle(HandleNE, vx + vw, vy);
            PositionHandle(HandleE, vx + vw, vy + vh / 2.0);
            PositionHandle(HandleSE, vx + vw, vy + vh);
            PositionHandle(HandleS, vx + vw / 2.0, vy + vh);
            PositionHandle(HandleSW, vx, vy + vh);
            PositionHandle(HandleW, vx, vy + vh / 2.0);
        }

        private void PositionHandle(FrameworkElement handle, double centerX, double centerY)
        {
            Canvas.SetLeft(handle, centerX - handle.Width / 2.0);
            Canvas.SetTop(handle, centerY - handle.Height / 2.0);
        }

        private void OnHandleMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_selectedSource == null || _selectedSource.IsLocked) return;
            if (sender is FrameworkElement el && el.Tag is string handleTag)
            {
                TransformBegun?.Invoke();
                _isResizing = true;
                _activeHandle = handleTag;
                _dragStartMouse = e.GetPosition(OverlayCanvas);
                _initialX = _selectedSource.X;
                _initialY = _selectedSource.Y;
                _initialW = _selectedSource.Width;
                _initialH = _selectedSource.Height;
                el.CaptureMouse();
                e.Handled = true;
            }
        }

        private void OnRotateHandleMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_selectedSource == null || _selectedSource.IsLocked) return;

            TransformBegun?.Invoke();
            _isRotating = true;
            _dragStartMouse = e.GetPosition(OverlayCanvas);
            _initialRot = _selectedSource.Rotation;
            HandleRotate.CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_selectedSource == null) return;

            Point curMouse = e.GetPosition(OverlayCanvas);
            double scaleX = ActualWidth / _canvasWidth;
            double scaleY = ActualHeight / _canvasHeight;

            if (scaleX <= 0 || scaleY <= 0) return;

            double dxCanvas = (curMouse.X - _dragStartMouse.X) / scaleX;
            double dyCanvas = (curMouse.Y - _dragStartMouse.Y) / scaleY;

            if (_isDragging)
            {
                _selectedSource.X = Math.Round(_initialX + dxCanvas);
                _selectedSource.Y = Math.Round(_initialY + dyCanvas);
                UpdateGizmo();
                TransformModified?.Invoke();
            }
            else if (_isResizing)
            {
                double newX = _initialX;
                double newY = _initialY;
                double newW = _initialW;
                double newH = _initialH;

                switch (_activeHandle)
                {
                    case "SE":
                        newW = Math.Max(20, _initialW + dxCanvas);
                        newH = Math.Max(20, _initialH + dyCanvas);
                        break;
                    case "E":
                        newW = Math.Max(20, _initialW + dxCanvas);
                        break;
                    case "S":
                        newH = Math.Max(20, _initialH + dyCanvas);
                        break;
                    case "NW":
                        newW = Math.Max(20, _initialW - dxCanvas);
                        newH = Math.Max(20, _initialH - dyCanvas);
                        newX = _initialX + (_initialW - newW);
                        newY = _initialY + (_initialH - newH);
                        break;
                    case "N":
                        newH = Math.Max(20, _initialH - dyCanvas);
                        newY = _initialY + (_initialH - newH);
                        break;
                    case "W":
                        newW = Math.Max(20, _initialW - dxCanvas);
                        newX = _initialX + (_initialW - newW);
                        break;
                    case "NE":
                        newW = Math.Max(20, _initialW + dxCanvas);
                        newH = Math.Max(20, _initialH - dyCanvas);
                        newY = _initialY + (_initialH - newH);
                        break;
                    case "SW":
                        newW = Math.Max(20, _initialW - dxCanvas);
                        newH = Math.Max(20, _initialH + dyCanvas);
                        newX = _initialX + (_initialW - newW);
                        break;
                }

                _selectedSource.X = Math.Round(newX);
                _selectedSource.Y = Math.Round(newY);
                _selectedSource.Width = Math.Round(newW);
                _selectedSource.Height = Math.Round(newH);

                UpdateGizmo();
                TransformModified?.Invoke();
            }
            else if (_isRotating)
            {
                double scaleX_ = ActualWidth / _canvasWidth;
                double scaleY_ = ActualHeight / _canvasHeight;
                double cx = (_selectedSource.X + _selectedSource.Width / 2.0) * scaleX_;
                double cy = (_selectedSource.Y + _selectedSource.Height / 2.0) * scaleY_;

                double angleRad = Math.Atan2(curMouse.Y - cy, curMouse.X - cx);
                double angleDeg = angleRad * 180.0 / Math.PI + 90.0;
                _selectedSource.Rotation = Math.Round(angleDeg);

                UpdateGizmo();
                TransformModified?.Invoke();
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs? e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                SelectionBox.ReleaseMouseCapture();
            }
            if (_isResizing)
            {
                _isResizing = false;
                Mouse.Capture(null);
            }
            if (_isRotating)
            {
                _isRotating = false;
                HandleRotate.ReleaseMouseCapture();
            }
        }
    }
}
