// Copyright (c) 2025 BobLd
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using Avalonia;
using Avalonia.Media;
using Caly.Pdf.Models;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis;

namespace Caly.Core.Utilities
{
    internal static class PdfWordHelpers
    {
        public static StreamGeometry GetGeometry(PdfRectangle rect, bool isFilled = false)
        {
            var sg = new StreamGeometry();
            using (var ctx = sg.Open())
            {
                ctx.BeginFigure(new Point(rect.BottomLeft.X, rect.BottomLeft.Y), isFilled);
                ctx.LineTo(new Point(rect.TopLeft.X, rect.TopLeft.Y));
                ctx.LineTo(new Point(rect.TopRight.X, rect.TopRight.Y));
                ctx.LineTo(new Point(rect.BottomRight.X, rect.BottomRight.Y));
                ctx.EndFigure(true);
            }

            return sg;
        }

        public static StreamGeometry GetGeometry(PdfWord word)
        {
            return GetGeometry(word.BoundingBox, true);
        }

        public static StreamGeometry? GetGeometry(PdfWord word, int startIndex, int endIndex)
        {
            System.Diagnostics.Debug.Assert(startIndex > -1);
            System.Diagnostics.Debug.Assert(endIndex > -1);
            System.Diagnostics.Debug.Assert(startIndex <= endIndex);
            
            int length = endIndex - startIndex + 1;

            if (length == 1)
            {
                return GetGeometry(word.GetLetterBoundingBox(startIndex), true);
            }

            Span<PdfRectangle> rects = length <= 128 ? stackalloc PdfRectangle[length] : new PdfRectangle[length];

            for (int l = startIndex; l <= endIndex; ++l)
            {
                rects[l - startIndex] = word.GetLetterBoundingBox(l);
            }
            return GetGeometry(rects, word.TextOrientation);
        }

        public static StreamGeometry GetGeometry(ReadOnlySpan<PdfRectangle> rects, TextOrientation orientation)
        {
            PdfRectangle bbox = orientation switch
            {
                TextOrientation.Horizontal => GetBoundingBoxH(rects),
                TextOrientation.Rotate180 => GetBoundingBox180(rects),
                TextOrientation.Rotate90 => GetBoundingBox90(rects),
                TextOrientation.Rotate270 => GetBoundingBox270(rects),
                _ => GetBoundingBoxOther(rects)
            };

            return GetGeometry(bbox, true);
        }

        #region Bounding box - Same as PdfWord
        private static PdfRectangle GetBoundingBoxH(ReadOnlySpan<PdfRectangle> rects)
        {
            var blX = double.MaxValue;
            var trX = double.MinValue;

            // Inverse Y axis - (0, 0) is top left
            var blY = double.MinValue;
            var trY = double.MaxValue;

            for (var i = 0; i < rects.Length; ++i)
            {
                var rect = rects[i];

                if (rect.BottomLeft.X < blX)
                {
                    blX = rect.BottomLeft.X;
                }

                if (rect.BottomLeft.Y > blY)
                {
                    blY = rect.BottomLeft.Y;
                }

                var right = rect.BottomLeft.X + rect.Width;
                if (right > trX)
                {
                    trX = right;
                }

                if (rect.TopLeft.Y < trY)
                {
                    trY = rect.TopLeft.Y;
                }
            }

            return new PdfRectangle(blX, blY, trX, trY);
        }

        private static PdfRectangle GetBoundingBox180(ReadOnlySpan<PdfRectangle> rects)
        {
            var blX = double.MinValue;
            var trX = double.MaxValue;

            // Inverse Y axis - (0, 0) is top left
            var blY = double.MaxValue;
            var trY = double.MinValue;

            for (var i = 0; i < rects.Length; ++i)
            {
                var rect = rects[i];

                if (rect.BottomLeft.X > blX)
                {
                    blX = rect.BottomLeft.X;
                }

                if (rect.BottomLeft.Y < blY)
                {
                    blY = rect.BottomLeft.Y;
                }

                var right = rect.BottomLeft.X - rect.Width;
                if (right < trX)
                {
                    trX = right;
                }

                if (rect.TopRight.Y > trY)
                {
                    trY = rect.TopRight.Y;
                }
            }

            return new PdfRectangle(blX, blY, trX, trY);
        }

        private static PdfRectangle GetBoundingBox90(ReadOnlySpan<PdfRectangle> rects)
        {
            var b = double.MaxValue;
            var r = double.MaxValue;
            var t = double.MinValue;
            var l = double.MinValue;

            for (var i = 0; i < rects.Length; i++)
            {
                var rect = rects[i];
                if (rect.BottomLeft.X < b)
                {
                    b = rect.BottomLeft.X;
                }

                if (rect.BottomRight.Y < r)
                {
                    r = rect.BottomRight.Y;
                }

                var right = rect.BottomLeft.X - rect.Height;
                if (right > t)
                {
                    t = right;
                }

                if (rect.BottomLeft.Y > l)
                {
                    l = rect.BottomLeft.Y;
                }
            }

            return new PdfRectangle(new PdfPoint(b, l), new PdfPoint(b, r),
                new PdfPoint(t, l), new PdfPoint(t, r));
        }

        private static PdfRectangle GetBoundingBox270(ReadOnlySpan<PdfRectangle> rects)
        {
            var t = double.MaxValue;
            var b = double.MinValue;
            var l = double.MaxValue;
            var r = double.MinValue;

            for (var i = 0; i < rects.Length; i++)
            {
                var rect = rects[i];
                if (rect.TopLeft.X > b)
                {
                    b = rect.TopLeft.X;
                }

                if (rect.BottomLeft.Y < l)
                {
                    l = rect.BottomLeft.Y;
                }

                var right = rect.BottomRight.X;
                if (right < t)
                {
                    t = right;
                }

                if (rect.BottomRight.Y > r)
                {
                    r = rect.BottomRight.Y;
                }
            }

            return new PdfRectangle(new PdfPoint(b, l), new PdfPoint(b, r),
                new PdfPoint(t, l), new PdfPoint(t, r));
        }

        private static PdfRectangle GetBoundingBoxOther(ReadOnlySpan<PdfRectangle> rects)
        {
            Span<PdfPoint> baseLinePoints = rects.Length * 2 <= 256
                ? stackalloc PdfPoint[rects.Length * 2]
                : new PdfPoint[rects.Length * 2];

            // Fitting a line through the baselines points
            // to find the orientation (slope)
            double x0 = 0;
            double y0 = 0;

            for (int i = 0; i < rects.Length; ++i)
            {
                var r = rects[i];
                baseLinePoints[2 * i] = r.BottomLeft;
                baseLinePoints[2 * i + 1] = r.BottomRight;

                x0 += r.BottomLeft.X + r.BottomRight.X;
                y0 += r.BottomLeft.Y + r.BottomRight.Y;
            }

            x0 /= baseLinePoints.Length;
            y0 /= baseLinePoints.Length;

            double sumProduct = 0;
            double sumDiffSquaredX = 0;

            for (int i = 0; i < baseLinePoints.Length; ++i)
            {
                var point = baseLinePoints[i];
                var x_diff = point.X - x0;
                var y_diff = point.Y - y0;
                sumProduct += x_diff * y_diff;
                sumDiffSquaredX += x_diff * x_diff;
            }

            double cos = 0;
            double sin = 1;
            if (sumDiffSquaredX > 1e-3)
            {
                // not vertical line
                double angleRad = Math.Atan(sumProduct / sumDiffSquaredX); // -π/2 ≤ θ ≤ π/2
                cos = Math.Cos(angleRad);
                sin = Math.Sin(angleRad);
            }

            // Rotate the points to build the axis-aligned bounding box (AABB)
            var inverseRotation = new TransformationMatrix(
                cos, -sin, 0,
                sin, cos, 0,
                0, 0, 1);
            
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            void UpdateMinMax(PdfPoint point)
            {
                if (point.X < minX) minX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.X > maxX) maxX = point.X;
                if (point.Y > maxY) maxY = point.Y;
            }

            foreach (var r in rects)
            {
                UpdateMinMax(inverseRotation.Transform(r.BottomLeft));
                UpdateMinMax(inverseRotation.Transform(r.BottomRight));
                UpdateMinMax(inverseRotation.Transform(r.TopLeft));
                UpdateMinMax(inverseRotation.Transform(r.TopRight));
            }

            // Inverse Y axis - (0, 0) is top left
            var aabb = new PdfRectangle(minX, maxY, maxX, minY);

            // Rotate back the AABB to obtain to oriented bounding box (OBB)
            var rotateBack = new TransformationMatrix(
                cos, sin, 0,
                -sin, cos, 0,
                0, 0, 1);

            // Candidates bounding boxes
            var obb = rotateBack.Transform(aabb);
            var obb1 = new PdfRectangle(obb.BottomLeft, obb.TopLeft, obb.BottomRight, obb.TopRight);
            var obb2 = new PdfRectangle(obb.BottomRight, obb.BottomLeft, obb.TopRight, obb.TopLeft);
            var obb3 = new PdfRectangle(obb.TopRight, obb.BottomRight, obb.TopLeft, obb.BottomLeft);

            // Find the orientation of the OBB, using the baseline angle
            // Assumes word order is correct
            var firstRect = rects[0];
            var lastRect = rects[rects.Length - 1];

            var baseLineAngle = Math.Atan2(
                lastRect.BottomRight.Y - firstRect.BottomLeft.Y,
                lastRect.BottomRight.X - firstRect.BottomLeft.X) * 180 / Math.PI;

            double deltaAngle = Math.Abs(Distances.BoundAngle180(obb.Rotation - baseLineAngle));
            double deltaAngle1 = Math.Abs(Distances.BoundAngle180(obb1.Rotation - baseLineAngle));
            if (deltaAngle1 < deltaAngle)
            {
                deltaAngle = deltaAngle1;
                obb = obb1;
            }

            double deltaAngle2 = Math.Abs(Distances.BoundAngle180(obb2.Rotation - baseLineAngle));
            if (deltaAngle2 < deltaAngle)
            {
                deltaAngle = deltaAngle2;
                obb = obb2;
            }

            double deltaAngle3 = Math.Abs(Distances.BoundAngle180(obb3.Rotation - baseLineAngle));
            if (deltaAngle3 < deltaAngle)
            {
                obb = obb3;
            }

            return obb;
        }
        #endregion
    }
}
