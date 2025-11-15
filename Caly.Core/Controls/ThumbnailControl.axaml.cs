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
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Caly.Core.Controls;

/// <summary>
/// Control that represents a single page thumbnail in a PDF document, with its visible area rectangle.
/// </summary>
public sealed class ThumbnailControl : TemplatedControl
{
    /*
     * See PDF Reference 1.7 - C.2 Architectural limits
     * Thumbnail images should be no larger than 106 by 106 samples, and should be created at one-eighth scale for 8.5-by-11-inch and A4-size pages.
     */

    private const double _borderThickness = 2;

    private static readonly Color AreaColor = Colors.DodgerBlue;
    private static readonly Brush AreaBrush = new SolidColorBrush(AreaColor);
    private static readonly Brush AreaTransparentBrush = new SolidColorBrush(AreaColor, 0.3);
    private static readonly Pen AreaPen = new Pen() { Brush = AreaBrush, Thickness = _borderThickness };

    private Matrix _scale = Matrix.Identity;

    /// <summary>
    /// Defines the <see cref="VisibleArea"/> property.
    /// </summary>
    public static readonly StyledProperty<Rect?> VisibleAreaProperty =
        AvaloniaProperty.Register<ThumbnailControl, Rect?>(nameof(VisibleArea), null);

    /// <summary>
    /// Defines the <see cref="ThumbnailWidth"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ThumbnailWidthProperty =
        AvaloniaProperty.Register<ThumbnailControl, double>(nameof(ThumbnailWidth));

    /// <summary>
    /// Defines the <see cref="ThumbnailHeight"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ThumbnailHeightProperty =
        AvaloniaProperty.Register<ThumbnailControl, double>(nameof(ThumbnailHeight));

    /// <summary>
    /// Defines the <see cref="PageWidth"/> property.
    /// </summary>
    public static readonly StyledProperty<double> PageWidthProperty =
        AvaloniaProperty.Register<ThumbnailControl, double>(nameof(PageWidth));

    /// <summary>
    /// Defines the <see cref="PageHeight"/> property.
    /// </summary>
    public static readonly StyledProperty<double> PageHeightProperty =
        AvaloniaProperty.Register<ThumbnailControl, double>(nameof(PageHeight));

    /// <summary>
    /// Defines the <see cref="Thumbnail"/> property.
    /// </summary>
    public static readonly StyledProperty<IImage?> ThumbnailProperty =
        AvaloniaProperty.Register<ThumbnailControl, IImage?>(nameof(Thumbnail));

    public Rect? VisibleArea
    {
        get => GetValue(VisibleAreaProperty);
        set => SetValue(VisibleAreaProperty, value);
    }

    public double ThumbnailWidth
    {
        get => GetValue(ThumbnailWidthProperty);
        set => SetValue(ThumbnailWidthProperty, value);
    }

    public double ThumbnailHeight
    {
        get => GetValue(ThumbnailHeightProperty);
        set => SetValue(ThumbnailHeightProperty, value);
    }

    public double PageWidth
    {
        get => GetValue(PageWidthProperty);
        set => SetValue(PageWidthProperty, value);
    }

    public double PageHeight
    {
        get => GetValue(PageHeightProperty);
        set => SetValue(PageHeightProperty, value);
    }

    public IImage? Thumbnail
    {
        get => GetValue(ThumbnailProperty);
        set => SetValue(ThumbnailProperty, value);
    }

    static ThumbnailControl()
    {
        AffectsRender<ThumbnailControl>(ThumbnailProperty, VisibleAreaProperty, ThumbnailHeightProperty,
            PageHeightProperty);
        AffectsMeasure<ThumbnailControl>(ThumbnailProperty, VisibleAreaProperty, ThumbnailHeightProperty,
            PageHeightProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ThumbnailWidthProperty ||
            change.Property == ThumbnailHeightProperty ||
            change.Property == PageWidthProperty ||
            change.Property == PageHeightProperty)
        {
            UpdateScaleMatrix();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        try
        {
            // Bitmap might not be null here but already disposed.
            // We use Dispatcher.UIThread.Invoke(() => t?.Dispose(), DispatcherPriority.Loaded);
            // in the PageViewModel to avoid this issue

            var thumbnail = Thumbnail;
            if (thumbnail is not null && Bounds is { Width: > 0, Height: > 0 })
            {
                context.DrawImage(thumbnail, Bounds);
            }
        }
        catch (Exception e)
        {
            // We just ignore for the moment
            Debug.WriteExceptionToFile(e);
        }

        if (VisibleArea.HasValue)
        {
            var area = VisibleArea.Value.TransformToAABB(_scale);

            if (area is { Width: > _borderThickness, Height: > _borderThickness })
            {
                context.DrawRectangle(AreaTransparentBrush.ToImmutable(), AreaPen.ToImmutable(),
                    area.Deflate(_borderThickness / 2.0));
            }
            else
            {
                context.DrawRectangle(AreaBrush.ToImmutable(), null, area);
            }
        }
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        if (Thumbnail is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void UpdateScaleMatrix()
    {
        if (IsNotValid(PageHeight) || IsNotValid(ThumbnailHeight))
        {
            return;
        }

        double ratio = Math.Round(ThumbnailHeight / PageHeight, 7);
        _scale = Matrix.CreateScale(ratio, ratio);
    }

    private static bool IsNotValid(double v)
    {
        return v <= 0 || double.IsInfinity(v) || double.IsNaN(v);
    }
}
