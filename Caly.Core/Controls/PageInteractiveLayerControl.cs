// Copyright (c) BobLd
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
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using Caly.Core.Models.Ai;
using Caly.Core.Utilities;
using System.Windows.Input;
using Caly.Pdf.Models;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Styling;
using UglyToad.PdfPig.Core;

namespace Caly.Core.Controls;

/// <summary>
/// Control that represents the text layer of a PDF page, handling text selection and interaction.
/// </summary>
public sealed class PageInteractiveLayerControl : Control
{
    // https://github.com/AvaloniaUI/Avalonia/blob/master/src/Avalonia.Controls/Primitives/TextSelectionCanvas.cs#L62
    // Check caret handle

    private static readonly Color SelectionColor = Color.FromArgb(169, 0x33, 0x99, 0xFF);
    private static readonly Color SearchColor = Color.FromArgb(120, 255, 0, 0);

    private static readonly ImmutableSolidColorBrush SelectionBrush = new(SelectionColor);
    private static readonly ImmutableSolidColorBrush SearchBrush = new(SearchColor);

    private const string AnnotationFlyoutClass = "calyAnnotationFlyout";

    private const string AnnotationFlyoutBackgroundKey = "CalyAnnotationFlyoutBackground";

    public static readonly StyledProperty<PdfTextLayer?> PdfTextLayerProperty =
        AvaloniaProperty.Register<PageInteractiveLayerControl, PdfTextLayer?>(nameof(PdfTextLayer));

    public static readonly StyledProperty<int?> PageNumberProperty =
        AvaloniaProperty.Register<PageInteractiveLayerControl, int?>(nameof(PageNumber));

    /// <summary>
    /// Defines the <see cref="VisibleArea"/> property.
    /// </summary>
    public static readonly StyledProperty<Rect?> VisibleAreaProperty =
        AvaloniaProperty.Register<PageInteractiveLayerControl, Rect?>(nameof(VisibleArea));

    /// <summary>
    /// Defines the <see cref="SelectedWords"/> property.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<PdfRectangle[]>?> SelectedWordsProperty =
        AvaloniaProperty.Register<PageInteractiveLayerControl, IReadOnlyList<PdfRectangle[]>?>(nameof(SelectedWords));

    /// <summary>
    /// Defines the <see cref="SearchResults"/> property.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<PdfRectangle[]>?> SearchResultsProperty =
        AvaloniaProperty.Register<PageInteractiveLayerControl, IReadOnlyList<PdfRectangle[]>?>(nameof(SearchResults));

    /// <summary>
    /// Defines the <see cref="AiBoxes"/> property.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<AiPageBox>?> AiBoxesProperty =
        AvaloniaProperty.Register<PageInteractiveLayerControl, IReadOnlyList<AiPageBox>?>(nameof(AiBoxes));

    /// <summary>
    /// Defines the <see cref="AiBoxCommand"/> property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> AiBoxCommandProperty =
        AvaloniaProperty.Register<PageInteractiveLayerControl, ICommand?>(nameof(AiBoxCommand));

    private static readonly Color AiColor = Color.FromRgb(0x1E, 0x90, 0xFF);
    private static readonly ImmutableSolidColorBrush AiFillBrush = new(AiColor, 0.12);
    private static readonly ImmutableSolidColorBrush AiSelectedFillBrush = new(AiColor, 0.30);
    private static readonly ImmutableSolidColorBrush AiStrokeBrush = new(AiColor);

    private StreamGeometry[]? _selectedWordsGeometry;
    private StreamGeometry[]? _searchResultsGeometry;

    static PageInteractiveLayerControl()
    {
        AffectsRender<PageInteractiveLayerControl>(PdfTextLayerProperty, VisibleAreaProperty,
            SelectedWordsProperty, SearchResultsProperty, AiBoxesProperty);
    }

    public PageInteractiveLayerControl()
    {
        Styles.Add(new Style(x => x.OfType<FlyoutPresenter>().Class(AnnotationFlyoutClass))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, new DynamicResourceExtension(AnnotationFlyoutBackgroundKey))
            }
        });
    }

    public IReadOnlyList<PdfRectangle[]>? SelectedWords
    {
        get => GetValue(SelectedWordsProperty);
        set => SetValue(SelectedWordsProperty, value);
    }

    public IReadOnlyList<PdfRectangle[]>? SearchResults
    {
        get => GetValue(SearchResultsProperty);
        set => SetValue(SearchResultsProperty, value);
    }

    /// <summary>
    /// Boxes found by the AI tab, in 0..1 of this page.
    /// </summary>
    public IReadOnlyList<AiPageBox>? AiBoxes
    {
        get => GetValue(AiBoxesProperty);
        set => SetValue(AiBoxesProperty, value);
    }

    /// <summary>
    /// Run with the item index of the box that was clicked.
    /// </summary>
    public ICommand? AiBoxCommand
    {
        get => GetValue(AiBoxCommandProperty);
        set => SetValue(AiBoxCommandProperty, value);
    }

    /// <summary>
    /// Runs <see cref="AiBoxCommand"/> for the topmost AI box under <paramref name="point"/>.
    /// </summary>
    /// <returns><c>true</c> if a box took the click.</returns>
    internal bool TryActivateAiBox(Point point)
    {
        if (AiBoxes is not { Count: > 0 } boxes || AiBoxCommand is not { } command)
        {
            return false;
        }

        var size = Bounds.Size;
        for (int i = boxes.Count - 1; i >= 0; i--)
        {
            if (boxes[i].Box.ToRect(size).Contains(point) && command.CanExecute(boxes[i].ItemIndex))
            {
                command.Execute(boxes[i].ItemIndex);
                return true;
            }
        }

        return false;
    }

    public PdfTextLayer? PdfTextLayer
    {
        get => GetValue(PdfTextLayerProperty);
        set => SetValue(PdfTextLayerProperty, value);
    }

    public int? PageNumber
    {
        get => GetValue(PageNumberProperty);
        set => SetValue(PageNumberProperty, value);
    }

    public Rect? VisibleArea
    {
        get => GetValue(VisibleAreaProperty);
        set => SetValue(VisibleAreaProperty, value);
    }

    internal Matrix GetLayoutTransformMatrix()
    {
        return this.FindAncestorOfType<PageItemsControl>()?
            .LayoutTransform?
            .LayoutTransform?.Value ?? Matrix.Identity;
    }

    internal void SetIbeamCursor()
    {
        Debug.ThrowNotOnUiThread();

        var itemsControl = this.FindAncestorOfType<PageItemsControl>();
        if (itemsControl is not null && itemsControl.Cursor != App.IbeamCursor)
        {
            itemsControl.Cursor = App.IbeamCursor;
        }
    }

    internal void SetHandCursor()
    {
        Debug.ThrowNotOnUiThread();

        var itemsControl = this.FindAncestorOfType<PageItemsControl>();
        if (itemsControl is not null && itemsControl.Cursor != App.HandCursor)
        {
            itemsControl.Cursor = App.HandCursor;
        }
    }

    internal void SetDefaultCursor()
    {
        Debug.ThrowNotOnUiThread();

        var itemsControl = this.FindAncestorOfType<PageItemsControl>();
        if (itemsControl is not null && itemsControl.Cursor != App.DefaultCursor)
        {
            itemsControl.Cursor = App.DefaultCursor;
        }
    }

    internal void HideAnnotation()
    {
        if (FlyoutBase.GetAttachedFlyout(this) is not Flyout attachedFlyout)
        {
            return;
        }

        attachedFlyout.FlyoutPresenterClasses.Remove(AnnotationFlyoutClass);
        attachedFlyout.Hide();
        attachedFlyout.Content = null;
    }

    internal void ShowAnnotation(PdfAnnotation annotation)
    {
        if (FlyoutBase.GetAttachedFlyout(this) is not Flyout attachedFlyout)
        {
            return;
        }
        
        var contentText = new TextBlock()
        {
            MaxWidth = 200,
            TextWrapping = TextWrapping.Wrap,
            Text = annotation.Content
        };

        if (annotation.Colour is not null)
        {
            var rgb = annotation.Colour.ToRGBValues();
            byte r = Convert.ToByte(rgb.r * 0.5 * 255);
            byte g = Convert.ToByte(rgb.g * 0.5 * 255);
            byte b = Convert.ToByte(rgb.b * 0.5 * 255);

            var color = new Color(byte.MaxValue, r, g, b);

            Resources[AnnotationFlyoutBackgroundKey] = new ImmutableSolidColorBrush(color);
            if (!attachedFlyout.FlyoutPresenterClasses.Contains(AnnotationFlyoutClass))
            {
                attachedFlyout.FlyoutPresenterClasses.Add(AnnotationFlyoutClass);
            }
        }
        else
        {
            attachedFlyout.FlyoutPresenterClasses.Remove(AnnotationFlyoutClass);
        }

        if (!string.IsNullOrEmpty(annotation.Date))
        {
            attachedFlyout.Content = new StackPanel()
            {
                Orientation = Orientation.Vertical,
                Children =
                {
                    new TextBlock()
                    {
                        Text = annotation.Date
                    },
                    contentText
                }
            };
        }
        else
        {
            attachedFlyout.Content = contentText;
        }

        attachedFlyout.ShowAt(this);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedWordsProperty)
        {
            _selectedWordsGeometry = null;
            if (change.NewValue is IReadOnlyList<PdfRectangle[]> lines && lines.Count > 0)
            {
                _selectedWordsGeometry = lines.Select(PdfWordHelpers.GetLineGeometry).ToArray();
            }
            else
            {
                _selectedWordsGeometry = null;
            }
        }
        else if (change.Property == SearchResultsProperty)
        {
            _searchResultsGeometry = null;
            if (change.NewValue is IReadOnlyList<PdfRectangle[]> lines && lines.Count > 0)
            {
                _searchResultsGeometry = lines.Select(PdfWordHelpers.GetLineGeometry).ToArray();
            }
            else
            {
                _searchResultsGeometry = null;
            }
        }
#if DEBUG
        else if (change.Property == PdfTextLayerProperty)
        {
            _annotationsGeometry = null;
            _textBlockGeometry = null;
            _textLineGeometry = null;
            _wordsGeometry = null;

            if (change.NewValue is not PdfTextLayer textLayer)
            {
                return;
            }

            if (textLayer.Annotations?.Count > 0)
            {
                _annotationsGeometry = textLayer.Annotations
                    .Select(a => PdfWordHelpers.GetGeometry(a.BoundingBox, true)).ToArray();
            }

            if (textLayer.TextBlocks?.Count > 0)
            {
                var blockGeometries = new List<StreamGeometry>();
                var lineGeometries = new List<StreamGeometry>();
                var wordGeometries = new List<StreamGeometry>();
                foreach (var block in textLayer.TextBlocks)
                {
                    blockGeometries.Add(PdfWordHelpers.GetGeometry(block.BoundingBox, true));
                    foreach (var line in block.TextLines)
                    {
                        lineGeometries.Add(PdfWordHelpers.GetGeometry(line.BoundingBox, true));
                        foreach (var word in line.Words)
                        {
                            wordGeometries.Add(PdfWordHelpers.GetGeometry(word.BoundingBox, false));
                        }
                    }
                }

                _textBlockGeometry = blockGeometries.ToArray();
                _textLineGeometry = lineGeometries.ToArray();
                _wordsGeometry = wordGeometries.ToArray();
            }
        }
#endif
    }

    private void RenderAiBoxes(DrawingContext context)
    {
        if (AiBoxes is not { Count: > 0 } boxes)
        {
            return;
        }

        // Keep the outline the same width on screen whatever the zoom.
        var matrix = GetLayoutTransformMatrix();
        double scale = Math.Sqrt(matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12);
        double thickness = 2.0 / (scale > 0 ? scale : 1.0);

        foreach (var box in boxes)
        {
            var rect = box.Box.ToRect(Bounds.Size);
            var pen = new ImmutablePen(AiStrokeBrush, box.IsSelected ? thickness * 1.5 : thickness);
            context.DrawRectangle(box.IsSelected ? AiSelectedFillBrush : AiFillBrush, pen, rect);
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        if (!VisibleArea.HasValue || VisibleArea.Value.IsEmpty())
        {
            return;
        }

        // We need to fill to get Pointer events
        context.FillRectangle(Brushes.Transparent, Bounds);

        RenderAiBoxes(context);

        if (PdfTextLayer?.TextBlocks is null)
        {
            return;
        }

#if DEBUG
        RenderDebug(context);
#endif

        // Draw search results first
        if (_searchResultsGeometry?.Length > 0)
        {
            foreach (var geometry in _searchResultsGeometry)
            {
                if (!geometry.Bounds.Intersects(VisibleArea.Value))
                {
                    continue;
                }

                context.DrawGeometry(SearchBrush, null, geometry);
            }
        }

        // Render Selection
        if (_selectedWordsGeometry?.Length > 0)
        {
            foreach (var geometry in _selectedWordsGeometry)
            {
                if (!geometry.Bounds.Intersects(VisibleArea.Value))
                {
                    continue;
                }

                context.DrawGeometry(SelectionBrush, null, geometry);
            }
        }
    }

#if DEBUG
    private static readonly ImmutableSolidColorBrush AnnotationsBrush = new(Colors.Purple, 0.4);
    private static readonly ImmutablePen AnnotationsPen = new(AnnotationsBrush, 0.5);
    private static readonly ImmutableSolidColorBrush YellowBrush = new(Colors.Yellow, 0.4);
    private static readonly ImmutableSolidColorBrush GreenBrush = new(Colors.Green, 0.4);
    private static readonly ImmutableSolidColorBrush RedBrush = new(Colors.Red, 0.4);
    private static readonly ImmutablePen RedPen = new(RedBrush, 0.5);

    private StreamGeometry[]? _annotationsGeometry;
    private StreamGeometry[]? _textBlockGeometry;
    private StreamGeometry[]? _textLineGeometry;
    private StreamGeometry[]? _wordsGeometry;

    private void RenderDebug(DrawingContext context)
    {
        if (!VisibleArea.HasValue || VisibleArea.Value.IsEmpty())
        {
            return;
        }

        // Render Annotations
        if (_annotationsGeometry?.Length > 0)
        {
            foreach (var geometry in _annotationsGeometry)
            {
                if (!geometry.Bounds.Intersects(VisibleArea.Value))
                {
                    continue;
                }

                context.DrawGeometry(AnnotationsBrush, AnnotationsPen, geometry);
            }
        }

        // Render Text Blocks
        if (_textBlockGeometry?.Length > 0)
        {
            foreach (var geometry in _textBlockGeometry)
            {
                if (!geometry.Bounds.Intersects(VisibleArea.Value))
                {
                    continue;
                }

                context.DrawGeometry(GreenBrush, null, geometry);
            }
        }

        // Render Text Lines
        if (_textLineGeometry?.Length > 0)
        {
            foreach (var geometry in _textLineGeometry)
            {
                if (!geometry.Bounds.Intersects(VisibleArea.Value))
                {
                    continue;
                }

                context.DrawGeometry(YellowBrush, null, geometry);
            }
        }

        // Render Words
        if (_wordsGeometry?.Length > 0)
        {
            foreach (var geometry in _wordsGeometry)
            {
                if (!geometry.Bounds.Intersects(VisibleArea.Value))
                {
                    continue;
                }

                context.DrawGeometry(RedBrush, RedPen, geometry);
            }
        }
    }
#endif
}