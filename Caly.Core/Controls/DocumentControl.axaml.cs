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

using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.VisualTree;
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Models;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;

namespace Caly.Core.Controls;

/// <summary>
/// Control that represents a PDF document.
/// </summary>
[TemplatePart("PART_PageItemsControl", typeof(PageItemsControl))]
public sealed class DocumentControl : CalyTemplatedControl
{
    private PageItemsControl? _pageItemsControl;

    /// <summary>
    /// Defines the <see cref="ItemsSource"/> property.
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<DocumentControl, IEnumerable?>(nameof(ItemsSource));

    /// <summary>
    /// Defines the <see cref="PageCount"/> property.
    /// </summary>
    public static readonly StyledProperty<int> PageCountProperty =
        AvaloniaProperty.Register<DocumentControl, int>(nameof(PageCount), 0);

    /// <summary>
    /// Defines the <see cref="ZoomLevel"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ZoomLevelProperty =
        AvaloniaProperty.Register<DocumentControl, double>(nameof(ZoomLevel), 1,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Defines the <see cref="SelectedPageIndex"/> property. Starts at 1.
    /// </summary>
    public static readonly StyledProperty<int?> SelectedPageIndexProperty =
        AvaloniaProperty.Register<DocumentControl, int?>(nameof(SelectedPageIndex), 1,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Defines the <see cref="SelectedBookmark"/> property.
    /// </summary>
    public static readonly StyledProperty<PdfBookmarkNode?> SelectedBookmarkProperty =
        AvaloniaProperty.Register<DocumentControl, PdfBookmarkNode?>(nameof(SelectedBookmark));

    public static readonly StyledProperty<TextSearchResultViewModel?> SelectedTextSearchResultProperty =
        AvaloniaProperty.Register<DocumentControl, TextSearchResultViewModel?>(nameof(SelectedTextSearchResult));

    /// <summary>
    /// Defines the <see cref="PageInteractiveLayerHandler"/> property.
    /// </summary>
    public static readonly StyledProperty<IPageInteractiveLayerHandler?> PageInteractiveLayerHandlerProperty =
        AvaloniaProperty.Register<DocumentControl, IPageInteractiveLayerHandler?>(nameof(PageInteractiveLayerHandler));

    public int PageCount
    {
        get => GetValue(PageCountProperty);
        set => SetValue(PageCountProperty, value);
    }

    public double ZoomLevel
    {
        get => GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    /// <summary>
    /// Starts at 1.
    /// </summary>
    public int? SelectedPageIndex
    {
        get => GetValue(SelectedPageIndexProperty);
        set => SetValue(SelectedPageIndexProperty, value);
    }

    public PdfBookmarkNode? SelectedBookmark
    {
        get => GetValue(SelectedBookmarkProperty);
        set => SetValue(SelectedBookmarkProperty, value);
    }

    public TextSearchResultViewModel? SelectedTextSearchResult
    {
        get => GetValue(SelectedTextSearchResultProperty);
        set => SetValue(SelectedTextSearchResultProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public IPageInteractiveLayerHandler? PageInteractiveLayerHandler
    {
        get => GetValue(PageInteractiveLayerHandlerProperty);
        set => SetValue(PageInteractiveLayerHandlerProperty, value);
    }

    public DocumentControl()
    {
#if DEBUG
        if (Design.IsDesignMode)
        {
            DataContext = new DocumentViewModel();
        }
#endif
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var pointer = e.GetCurrentPoint(this);

        if (PageInteractiveLayerHandler is not null &&
            pointer.Properties.IsLeftButtonPressed &&
            e.Source is not PageInteractiveLayerControl)
        {
            PageInteractiveLayerHandler.ClearSelection(this);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedPageIndexProperty)
        {
            if (change.NewValue is int p)
            {
                GoToPage(p);
            }
        }
        else if (change.Property == SelectedBookmarkProperty)
        {
            if (SelectedBookmark?.PageNumber.HasValue == true &&
                SelectedBookmark.PageNumber.Value != SelectedPageIndex)
            {
                SetCurrentValue(SelectedPageIndexProperty, SelectedBookmark.PageNumber.Value);
            }
        }
        else if (change.Property == SelectedTextSearchResultProperty)
        {
            if (change.NewValue is TextSearchResultViewModel { PageNumber: > 0 } r)
            {
                SetCurrentValue(SelectedPageIndexProperty, r.PageNumber);
            }
        }
        else if (change.Property == ZoomLevelProperty)
        {
            if (_pageItemsControl?.LayoutTransform is null || change.NewValue is not double newZoom)
            {
                return;
            }

            if (!_pageItemsControl.LayoutTransform.IsAttachedToVisualTree())
            {
                return;
            }

            double dZoom = newZoom / (double?)change.OldValue ?? 1.0;

            double w = 0, h = 0;
            if (!_pageItemsControl.DesiredSize.IsEmpty())
            {
                _pageItemsControl.DesiredSize.Deconstruct(out w, out h);
            }
            else if (!_pageItemsControl.Bounds.Size.IsEmpty())
            {
                _pageItemsControl.Bounds.Size.Deconstruct(out w, out h);
            }

            var pixelPoint = this.PointToScreen(new Point((int)(w / 2.0), (int)(h / 2.0)));
            var point = _pageItemsControl.LayoutTransform.PointToClient(pixelPoint);
            _pageItemsControl.ZoomTo(dZoom, point);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _pageItemsControl = e.NameScope.FindFromNameScope<PageItemsControl>("PART_PageItemsControl");
    }

    /// <summary>
    /// Scrolls to the page number.
    /// </summary>
    /// <param name="pageNumber">The page number. Starts at 1.</param>
    public void GoToPage(int pageNumber)
    {
        _pageItemsControl?.GoToPage(pageNumber);
    }

    /// <summary>
    /// Get the page control for the page number.
    /// </summary>
    /// <param name="pageNumber">The page number. Starts at 1.</param>
    /// <returns>The page control, or <c>null</c> if not found.</returns>
    public PageItem? GetPageItem(int pageNumber)
    {
        return _pageItemsControl?.GetPageItem(pageNumber);
    }

    public PageItem? GetPageItemOver(PointerEventArgs e)
    {
        return _pageItemsControl?.GetPageItemOver(e);
    }
}
