using System;
using System.Collections.Generic;
using System.Windows;

namespace RamaverseStudio.UI.Gizmo
{
    public enum GizmoHandleType
    {
        None, Body, RotatePip,
        NW, N, NE, E, SE, S, SW, W,
        CropLeft, CropTop, CropRight, CropBottom
    }

    public enum SnapGuideType
    {
        CanvasCenter, CanvasEdge, GoldenRatio, RuleOfThirds, SafeArea, SiblingEdge, SiblingCenter
    }

    public struct ActiveGuideLine
    {
        public Point Start;
        public Point End;
        public SnapGuideType Type;
        public string Label;
    }

    /// <summary>
    /// Smart Snapping & Magnetic Hysteresis Engine for direct manipulation canvas transformations.
    /// Snaps to Center, Edges, Golden Ratio (Phi ~ 0.618), Rule of Thirds, and Sibling Items.
    /// </summary>
    public class SnapEngine
    {
        private const double InvGoldenRatio = 0.618033988749895;
        public double SnapThresholdScreenPx { get; set; } = 7.0;

        public void CalculateSnaps(
            double currentX, double currentY, double width, double height,
            int canvasW, int canvasH, double scaleX, double scaleY,
            IEnumerable<Rect>? siblingRects,
            out double outX, out double outY,
            List<ActiveGuideLine> activeGuides)
        {
            activeGuides.Clear();
            outX = currentX;
            outY = currentY;

            double snapThreshX = SnapThresholdScreenPx / scaleX;
            double snapThreshY = SnapThresholdScreenPx / scaleY;

            var xTargets = new List<(double Pos, SnapGuideType Type, string Label)>
            {
                (0, SnapGuideType.CanvasEdge, "Canvas Left (0px)"),
                (canvasW / 2.0, SnapGuideType.CanvasCenter, "Center X (50%)"),
                (canvasW, SnapGuideType.CanvasEdge, $"Canvas Right ({canvasW}px)"),
                (canvasW * (1.0 - InvGoldenRatio), SnapGuideType.GoldenRatio, "Golden Ratio (38.2%)"),
                (canvasW * InvGoldenRatio, SnapGuideType.GoldenRatio, "Golden Ratio (61.8%)"),
                (canvasW / 3.0, SnapGuideType.RuleOfThirds, "Third (33.3%)"),
                (canvasW * 2.0 / 3.0, SnapGuideType.RuleOfThirds, "Third (66.7%)")
            };

            var yTargets = new List<(double Pos, SnapGuideType Type, string Label)>
            {
                (0, SnapGuideType.CanvasEdge, "Canvas Top (0px)"),
                (canvasH / 2.0, SnapGuideType.CanvasCenter, "Center Y (50%)"),
                (canvasH, SnapGuideType.CanvasEdge, $"Canvas Bottom ({canvasH}px)"),
                (canvasH * (1.0 - InvGoldenRatio), SnapGuideType.GoldenRatio, "Golden Ratio (38.2%)"),
                (canvasH * InvGoldenRatio, SnapGuideType.GoldenRatio, "Golden Ratio (61.8%)"),
                (canvasH / 3.0, SnapGuideType.RuleOfThirds, "Third (33.3%)"),
                (canvasH * 2.0 / 3.0, SnapGuideType.RuleOfThirds, "Third (66.7%)")
            };

            if (siblingRects != null)
            {
                foreach (var r in siblingRects)
                {
                    xTargets.Add((r.Left, SnapGuideType.SiblingEdge, "Align Left"));
                    xTargets.Add((r.Left + r.Width / 2.0, SnapGuideType.SiblingCenter, "Align Center X"));
                    xTargets.Add((r.Right, SnapGuideType.SiblingEdge, "Align Right"));

                    yTargets.Add((r.Top, SnapGuideType.SiblingEdge, "Align Top"));
                    yTargets.Add((r.Top + r.Height / 2.0, SnapGuideType.SiblingCenter, "Align Center Y"));
                    yTargets.Add((r.Bottom, SnapGuideType.SiblingEdge, "Align Bottom"));
                }
            }

            // Test X Points: Left, Center, Right
            double bestDeltaX = double.MaxValue;
            double snapShiftX = 0;
            SnapGuideType bestTypeX = SnapGuideType.CanvasEdge;
            string bestLabelX = "";
            double guideLineX = 0;

            double[] testPointsX = { currentX, currentX + width / 2.0, currentX + width };
            for (int i = 0; i < testPointsX.Length; i++)
            {
                double pt = testPointsX[i];
                foreach (var target in xTargets)
                {
                    double delta = Math.Abs(pt - target.Pos);
                    if (delta < snapThreshX && delta < bestDeltaX)
                    {
                        bestDeltaX = delta;
                        snapShiftX = target.Pos - pt;
                        bestTypeX = target.Type;
                        bestLabelX = target.Label;
                        guideLineX = target.Pos;
                    }
                }
            }

            if (bestDeltaX <= snapThreshX)
            {
                outX = currentX + snapShiftX;
                activeGuides.Add(new ActiveGuideLine
                {
                    Start = new Point(guideLineX, 0),
                    End = new Point(guideLineX, canvasH),
                    Type = bestTypeX,
                    Label = bestLabelX
                });
            }

            // Test Y Points: Top, Center, Bottom
            double bestDeltaY = double.MaxValue;
            double snapShiftY = 0;
            SnapGuideType bestTypeY = SnapGuideType.CanvasEdge;
            string bestLabelY = "";
            double guideLineY = 0;

            double[] testPointsY = { currentY, currentY + height / 2.0, currentY + height };
            for (int i = 0; i < testPointsY.Length; i++)
            {
                double pt = testPointsY[i];
                foreach (var target in yTargets)
                {
                    double delta = Math.Abs(pt - target.Pos);
                    if (delta < snapThreshY && delta < bestDeltaY)
                    {
                        bestDeltaY = delta;
                        snapShiftY = target.Pos - pt;
                        bestTypeY = target.Type;
                        bestLabelY = target.Label;
                        guideLineY = target.Pos;
                    }
                }
            }

            if (bestDeltaY <= snapThreshY)
            {
                outY = currentY + snapShiftY;
                activeGuides.Add(new ActiveGuideLine
                {
                    Start = new Point(0, guideLineY),
                    End = new Point(canvasW, guideLineY),
                    Type = bestTypeY,
                    Label = bestLabelY
                });
            }
        }
    }
}
