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
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using Caly.Core.Utilities;
using Caly.Pdf.Models;
using System.Collections.Generic;
using System.Linq;
using UglyToad.PdfPig.Core;

namespace Caly.Core.Controls;

/// <summary>
/// Control that represents the text layer of a PDF page, handling text selection and interaction.
/// </summary>
public sealed class PageInteractiveLayerControl : Control
{
    // https://github.com/AvaloniaUI/Avalonia/blob/master/src/Avalonia.Controls/Primitives/TextSelectionCanvas.cs#L62
    // Check caret handle

    private static readonly Color SelectionColor = Color.FromArgb(0xa9, 0x33, 0x99, 0xFF);
    private static readonly Color SearchColor = Color.FromArgb(0xa9, 255, 0, 0);

    private static readonly ImmutableSolidColorBrush SelectionBrush = new(SelectionColor);
    private static readonly ImmutableSolidColorBrush SearchBrush = new(SearchColor);

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
    public static readonly StyledProperty<IReadOnlyList<PdfRectangle>?> SelectedWordsProperty =
        AvaloniaProperty.Register<PageInteractiveLayerControl, IReadOnlyList<PdfRectangle>?>(nameof(SelectedWords));

    /// <summary>
    /// Defines the <see cref="SearchResults"/> property.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<PdfRectangle>?> SearchResultsProperty =
        AvaloniaProperty.Register<PageInteractiveLayerControl, IReadOnlyList<PdfRectangle>?>(nameof(SearchResults));

    static PageInteractiveLayerControl()
    {
        AffectsRender<PageInteractiveLayerControl>(PdfTextLayerProperty, VisibleAreaProperty,
            SelectedWordsProperty, SearchResultsProperty);
    }

    public IReadOnlyList<PdfRectangle>? SelectedWords
    {
        get => GetValue(SelectedWordsProperty);
        set => SetValue(SelectedWordsProperty, value);
    }
    
    public IReadOnlyList<PdfRectangle>? SearchResults
    {
        get => GetValue(SearchResultsProperty);
        set => SetValue(SearchResultsProperty, value);
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

    public void HideAnnotation()
    {
        if (FlyoutBase.GetAttachedFlyout(this) is not Flyout attachedFlyout)
        {
            return;
        }

        attachedFlyout.Hide();
        attachedFlyout.Content = null;
    }

    public void ShowAnnotation(PdfAnnotation annotation)
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

    private StreamGeometry[]? _selectedWordsGeometry;
    private StreamGeometry[]? _searchResultsGeometry;
    
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedWordsProperty)
        {
            if (change.NewValue is IReadOnlyCollection<PdfRectangle> rects)
            {
                _selectedWordsGeometry = rects.Count != 0 ? rects.Select(r => PdfWordHelpers.GetGeometry(r, true)).ToArray() : null;
            }
            else
            {
                _selectedWordsGeometry = null;
            }
        }
        else if (change.Property == SearchResultsProperty)
        {
            if (change.NewValue is IReadOnlyCollection<PdfRectangle> rects)
            {
                _searchResultsGeometry = rects.Count != 0 ? rects.Select(r => PdfWordHelpers.GetGeometry(r, true)).ToArray() : null;
            }
            else
            {
                _searchResultsGeometry = null;
            }
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

        if (PdfTextLayer?.TextBlocks is null)
        {
            return;
        }

        DebugRender.RenderAnnotations(this, context, VisibleArea.Value);
        DebugRender.RenderText(this, context, VisibleArea.Value);

        // Draw search results first
        if (_searchResultsGeometry is not null && _searchResultsGeometry.Length > 0)
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
        if (_selectedWordsGeometry is not null && _selectedWordsGeometry.Length > 0)
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
}