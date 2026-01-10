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
        private static readonly Color _selectionColor = Color.FromArgb(0xa9, 0x33, 0x99, 0xFF);

        public void RenderPage(PageInteractiveLayerControl control, DrawingContext context, Rect visibleArea)
        {
            var pdfVisibleArea = visibleArea.ToPdfRectangle();

            if (control.PdfTextLayer?.TextBlocks is null)
            {
                return;
            }

#if DEBUG
            var redBrush = new SolidColorBrush(Colors.Red, 0.4);
            var redPen = new Pen(redBrush, 0.5);
            
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
