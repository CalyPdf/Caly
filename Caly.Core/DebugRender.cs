using Avalonia;
using Avalonia.Media;
using Caly.Core.Controls;
using Caly.Core.Utilities;
using Caly.Pdf.Models;
using System.Diagnostics;

namespace Caly.Core;

internal class DebugRender
{
    [Conditional("DEBUG")]
    private static void DrawArrow(DrawingContext context, IPen pen, Point lineStart, Point lineEnd)
    {
        context.DrawLine(pen, lineStart, lineEnd);
        context.DrawEllipse(null, pen, lineEnd, 1, 1);
    }

    [Conditional("DEBUG")]
    public static void RenderAnnotations(PageInteractiveLayerControl control, DrawingContext context, Rect visibleArea)
    {
        if (control.PdfTextLayer?.Annotations is null)
        {
            return;
        }

        var purpleBrush = new SolidColorBrush(Colors.Purple, 0.4);
        var purplePen = new Pen(purpleBrush, 0.5);

        foreach (var annotation in control.PdfTextLayer.Annotations)
        {
            context.DrawGeometry(purpleBrush, purplePen, PdfWordHelpers.GetGeometry(annotation.BoundingBox, true));
        }
    }

    [Conditional("DEBUG")]
    public static void RenderText(PageInteractiveLayerControl control, DrawingContext context, Rect visibleArea)
    {
        if (control.PdfTextLayer?.TextBlocks is null)
        {
            return;
        }

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
            context.DrawEllipse(Brushes.DarkGreen, null,
                new Point(block.BoundingBox.TopLeft.X, block.BoundingBox.TopLeft.Y), 2, 2);
            context.DrawEllipse(Brushes.DarkBlue, null,
                new Point(block.BoundingBox.BottomLeft.X, block.BoundingBox.BottomLeft.Y), 2, 2);
            context.DrawEllipse(Brushes.DarkRed, null,
                new Point(block.BoundingBox.BottomRight.X, block.BoundingBox.BottomRight.Y), 2, 2);

            foreach (var line in block.TextLines)
            {
                context.DrawGeometry(yellowBrush, yellowPen, PdfWordHelpers.GetGeometry(line.BoundingBox, true));
                context.DrawEllipse(Brushes.DarkGreen, null,
                    new Point(line.BoundingBox.TopLeft.X, line.BoundingBox.TopLeft.Y), 1, 1);
                context.DrawEllipse(Brushes.DarkBlue, null,
                    new Point(line.BoundingBox.BottomLeft.X, line.BoundingBox.BottomLeft.Y), 1, 1);
                context.DrawEllipse(Brushes.DarkRed, null,
                    new Point(line.BoundingBox.BottomRight.X, line.BoundingBox.BottomRight.Y), 1, 1);

                foreach (var word in line.Words)
                {
                    context.DrawGeometry(redBrush, redPen, PdfWordHelpers.GetGeometry(word.BoundingBox));
                    context.DrawEllipse(Brushes.DarkGreen, null,
                        new Point(word.BoundingBox.TopLeft.X, word.BoundingBox.TopLeft.Y), 0.5, 0.5);
                    context.DrawEllipse(Brushes.DarkBlue, null,
                        new Point(word.BoundingBox.BottomLeft.X, word.BoundingBox.BottomLeft.Y), 0.5, 0.5);
                    context.DrawEllipse(Brushes.DarkRed, null,
                        new Point(word.BoundingBox.BottomRight.X, word.BoundingBox.BottomRight.Y), 0.5, 0.5);

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
    }
}

