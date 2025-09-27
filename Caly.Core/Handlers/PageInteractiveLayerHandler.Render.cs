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

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Caly.Core.Controls;
using Caly.Core.Utilities;
using Caly.Pdf.Models;
using System.Linq;
using UglyToad.PdfPig.Geometry;

namespace Caly.Core.Handlers
{
    public sealed partial class PageInteractiveLayerHandler
    {
#if DEBUG
        private static void DrawArrow(DrawingContext context, IPen pen, Point lineStart, Point lineEnd)
        {
            context.DrawLine(pen, lineStart, lineEnd);
            context.DrawEllipse(null, pen, lineEnd, 1, 1);
        }
#endif

        public void RenderPage(PdfPageTextLayerControl control, DrawingContext context, Rect visibleArea)
        {
            var pdfVisibleArea = visibleArea.ToPdfRectangle();
#if DEBUG
            if (control.PdfTextLayer?.Annotations is not null)
            {
                var purpleBrush = new SolidColorBrush(Colors.Purple, 0.4);
                var purplePen = new Pen(purpleBrush, 0.5);

                foreach (var annotation in control.PdfTextLayer.Annotations)
                {
                    context.DrawGeometry(purpleBrush, purplePen, PdfWordHelpers.GetGeometry(annotation.BoundingBox, true));
                }
            }
#endif

            if (control.PdfTextLayer?.TextBlocks is null)
            {
                return;
            }

#if DEBUG
            var redBrush = new SolidColorBrush(Colors.Red, 0.4);
            var redPen = new Pen(redBrush, 0.5);
            var blueBrush = new SolidColorBrush(Colors.Blue, 0.4);
            var bluePen = new Pen(blueBrush, 0.5);
            var greenBrush = new SolidColorBrush(Colors.Green, 0.4);
            var greenPen = new Pen(greenBrush, 0.5);

            var yellowBrush = new SolidColorBrush(Colors.Yellow, 0.4);
            var yellowPen = new Pen(yellowBrush, 0.5);

            PdfWord? previousWord = null;

            foreach (var block in control.PdfTextLayer.TextBlocks)
            {
                context.DrawGeometry(greenBrush, greenPen, PdfWordHelpers.GetGeometry(block.BoundingBox, true));
                context.DrawEllipse(Brushes.DarkGreen, null, new Point(block.BoundingBox.TopLeft.X, block.BoundingBox.TopLeft.Y), 2, 2);
                context.DrawEllipse(Brushes.DarkBlue, null, new Point(block.BoundingBox.BottomLeft.X, block.BoundingBox.BottomLeft.Y), 2, 2);
                context.DrawEllipse(Brushes.DarkRed, null, new Point(block.BoundingBox.BottomRight.X, block.BoundingBox.BottomRight.Y), 2, 2);

                foreach (var line in block.TextLines)
                {
                    context.DrawGeometry(yellowBrush, yellowPen, PdfWordHelpers.GetGeometry(line.BoundingBox, true));
                    context.DrawEllipse(Brushes.DarkGreen, null, new Point(line.BoundingBox.TopLeft.X, line.BoundingBox.TopLeft.Y), 1, 1);
                    context.DrawEllipse(Brushes.DarkBlue, null, new Point(line.BoundingBox.BottomLeft.X, line.BoundingBox.BottomLeft.Y), 1, 1);
                    context.DrawEllipse(Brushes.DarkRed, null, new Point(line.BoundingBox.BottomRight.X, line.BoundingBox.BottomRight.Y), 1, 1);

                    foreach (var word in line.Words)
                    {
                        context.DrawGeometry(redBrush, redPen, PdfWordHelpers.GetGeometry(word.BoundingBox));
                        context.DrawEllipse(Brushes.DarkGreen, null, new Point(word.BoundingBox.TopLeft.X, word.BoundingBox.TopLeft.Y), 0.5, 0.5);
                        context.DrawEllipse(Brushes.DarkBlue, null, new Point(word.BoundingBox.BottomLeft.X, word.BoundingBox.BottomLeft.Y), 0.5, 0.5);
                        context.DrawEllipse(Brushes.DarkRed, null, new Point(word.BoundingBox.BottomRight.X, word.BoundingBox.BottomRight.Y), 0.5, 0.5);

                        if (previousWord is not null)
                        {
                            var start = new Point(previousWord.BoundingBox.Centroid.X, previousWord.BoundingBox.Centroid.Y);
                            var end = new Point(word.BoundingBox.Centroid.X, word.BoundingBox.Centroid.Y);
                            DrawArrow(context, bluePen, start, end);
                        }

                        previousWord = word;
                    }
                }
            }

            if (p1.HasValue && p2.HasValue)
            {
                context.DrawLine(redPen, p1.Value, p2.Value);
                context.DrawEllipse(null, redPen, p2.Value, 1, 1);
            }

            if (focusLine is not null)
            {
                context.DrawGeometry(redBrush, redPen, PdfWordHelpers.GetGeometry(focusLine.BoundingBox, true));
            }
#endif

            // Draw search results first
            var results = _searchWordsResults[control.PageNumber!.Value - 1];
            if (results is not null && results.Count > 0)
            {
                var searchBrush = new ImmutableSolidColorBrush(_searchColor);

                foreach (PdfWord result in results.Where(w => w.BoundingBox.IntersectsWith(pdfVisibleArea)))
                {
                    context.DrawGeometry(searchBrush, null, PdfWordHelpers.GetGeometry(result));
                }
            }

            // Draw selected text then
            if (Selection.IsValid)
            {
                var selectionBrush = new ImmutableSolidColorBrush(_selectionColor);

                foreach (var g in Selection.GetPageSelectionAs(control.PageNumber!.Value, PdfWordHelpers.GetGeometry, PdfWordHelpers.GetGeometry))
                {
                    // TODO - filter GetPageSelectionAs by visible area before creating the geometry

                    if (g is null || !g.Bounds.Intersects(visibleArea))
                    {
                        continue;
                    }

                    context.DrawGeometry(selectionBrush, null, g);
                }
            }

            // Other possible renders
        }
    }
}
