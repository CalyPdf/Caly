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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Transformation;
using Avalonia.VisualTree;
using Caly.Core.Services;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Tabalonia.Controls;

namespace Caly.Core.Controls;

[TemplatePart("PART_ScrollViewer", typeof(ScrollViewer))]
[TemplatePart("PART_LayoutTransformControl", typeof(LayoutTransformControl))]
public sealed class PdfPageItemsControl : ItemsControl
{
    private const double _zoomFactor = 1.1;

    private bool _isSettingPageVisibility = false;
    private bool _isZooming = false;
    private bool _isTabDragging = false;

    /// <summary>
    /// The default value for the <see cref="PdfPageItemsControl.ItemsPanel"/> property.
    /// </summary>
    private static readonly FuncTemplate<Panel?> DefaultPanel = new(() => new VirtualizingStackPanel()
    {
        // On Windows desktop, 0 is enough
        // Need to test other platforms
        CacheLength = 0
    });

    /// <summary>
    /// Defines the <see cref="Scroll"/> property.
    /// </summary>
    public static readonly DirectProperty<PdfPageItemsControl, ScrollViewer?> ScrollProperty =
        AvaloniaProperty.RegisterDirect<PdfPageItemsControl, ScrollViewer?>(nameof(Scroll), o => o.Scroll);

    /// <summary>
    /// Defines the <see cref="LayoutTransform"/> property.
    /// </summary>
    public static readonly DirectProperty<PdfPageItemsControl, LayoutTransformControl?> LayoutTransformControlProperty =
        AvaloniaProperty.RegisterDirect<PdfPageItemsControl, LayoutTransformControl?>(nameof(LayoutTransform), o => o.LayoutTransform);

    /// <summary>
    /// Defines the <see cref="PageCount"/> property.
    /// </summary>
    public static readonly StyledProperty<int> PageCountProperty = AvaloniaProperty.Register<PdfPageItemsControl, int>(nameof(PageCount));

    /// <summary>
    /// Defines the <see cref="SelectedPageIndex"/> property. Starts at 1.
    /// </summary>
    public static readonly StyledProperty<int?> SelectedPageIndexProperty = AvaloniaProperty.Register<PdfPageItemsControl, int?>(nameof(SelectedPageIndex), 1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Defines the <see cref="MinZoomLevel"/> property.
    /// </summary>
    public static readonly StyledProperty<double> MinZoomLevelProperty = AvaloniaProperty.Register<PdfPageItemsControl, double>(nameof(MinZoomLevel));

    /// <summary>
    /// Defines the <see cref="MaxZoomLevel"/> property.
    /// </summary>
    public static readonly StyledProperty<double> MaxZoomLevelProperty = AvaloniaProperty.Register<PdfPageItemsControl, double>(nameof(MaxZoomLevel), 1);

    /// <summary>
    /// Defines the <see cref="ZoomLevel"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ZoomLevelProperty = AvaloniaProperty.Register<PdfPageItemsControl, double>(nameof(ZoomLevel), 1, defaultBindingMode: BindingMode.TwoWay);
    
    private ScrollViewer? _scroll;
    private LayoutTransformControl? _layoutTransform;
    private TabsControl? _tabsControl;

    static PdfPageItemsControl()
    {
        ItemsPanelProperty.OverrideDefaultValue<PdfPageItemsControl>(DefaultPanel);
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue(typeof(PdfPageItemsControl),
            KeyboardNavigationMode.Once);
    }
    
    /// <summary>
    /// Gets the scroll information for the <see cref="ListBox"/>.
    /// </summary>
    public ScrollViewer? Scroll
    {
        get => _scroll;
        private set => SetAndRaise(ScrollProperty, ref _scroll, value);
    }

    /// <summary>
    /// Gets the scroll information for the <see cref="ListBox"/>.
    /// </summary>
    public LayoutTransformControl? LayoutTransform
    {
        get => _layoutTransform;
        private set => SetAndRaise(LayoutTransformControlProperty, ref _layoutTransform, value);
    }

    public int PageCount
    {
        get => GetValue(PageCountProperty);
        set => SetValue(PageCountProperty, value);
    }

    /// <summary>
    /// Starts at 1.
    /// </summary>
    public int? SelectedPageIndex
    {
        get => GetValue(SelectedPageIndexProperty);
        set => SetValue(SelectedPageIndexProperty, value);
    }

    public double MinZoomLevel
    {
        get => GetValue(MinZoomLevelProperty);
        set => SetValue(MinZoomLevelProperty, value);
    }

    public double MaxZoomLevel
    {
        get => GetValue(MaxZoomLevelProperty);
        set => SetValue(MaxZoomLevelProperty, value);
    }

    public double ZoomLevel
    {
        get => GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    /// <summary>
    /// Get the page control for the page number.
    /// </summary>
    /// <param name="pageNumber">The page number. Starts at 1.</param>
    /// <returns>The page control, or <c>null</c> if not found.</returns>
    public PdfPageItem? GetPdfPageItem(int pageNumber)
    {
        System.Diagnostics.Debug.WriteLine($"GetPdfPageItem {pageNumber}.");
        if (ContainerFromIndex(pageNumber - 1) is PdfPageItem presenter)
        {
            return presenter;
        }

        return null;
    }

    /// <summary>
    /// Scrolls to the page number.
    /// </summary>
    /// <param name="pageNumber">The page number. Starts at 1.</param>
    public void GoToPage(int pageNumber)
    {
        if (_isSettingPageVisibility || pageNumber <= 0 || pageNumber > PageCount ||  ItemsView.Count == 0)
        {
            return;
        }

        ScrollIntoView(pageNumber - 1);
    }

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);

        if (_isTabDragging ||
            container is not PdfPageItem ||
            item is not PdfPageViewModel vm)
        {
            System.Diagnostics.Debug.WriteLine($"Skipping LoadPage() for page {index + 1} (IsTabDragging: {_isTabDragging})");
            return;
        }

        vm.VisibleArea = null;
        App.Messenger.Send(new LoadPageMessage(vm));
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        
        if (container is not PdfPageItem cp)
        {
            return;
        }

        if (cp.DataContext is PdfPageViewModel vm)
        {
            System.Diagnostics.Debug.WriteLine($"ClearContainerForItemOverride: doc vm: {this.DataContext}");
            System.Diagnostics.Debug.WriteLine($"ClearContainerForItemOverride: page vm: {vm}");
            System.Diagnostics.Debug.WriteLine($"ClearContainerForItemOverride: isTabDragging: {_isTabDragging}");
            System.Diagnostics.Debug.WriteLine($"ClearContainerForItemOverride: HasRealisedItems: {HasRealisedItems()}");
            System.Diagnostics.Debug.WriteLine($"ClearContainerForItemOverride: IsPageRealised: {IsPageRealised(vm)}");
            System.Diagnostics.Debug.WriteLine($"ClearContainerForItemOverride: ItemsView?.Count: {ItemsView?.Count}");
            System.Diagnostics.Debug.WriteLine($"ClearContainerForItemOverride: ItemsSource: {(ItemsSource as ObservableCollection<PdfPageViewModel>)?.Count}");
            System.Diagnostics.Debug.WriteLine($"ClearContainerForItemOverride:  vm.VisibleArea: {vm.VisibleArea}");

            if (vm.VisibleArea.HasValue)
            {
                vm.VisibleArea = null;
                App.Messenger.Send(new UnloadPageMessage(vm));
            }
            else
            {
                // This is a sign that the page won't load properly.
                // We are trying to cancel a page that needs to be
                // rendered. The page picture, text and visibility
                // will not be correct. To fix that, we do that once
                // when the layout is updated.
                cp.LayoutUpdated += PdfPageItemLayoutUpdated;
            }
        }
    }

    private void PdfPageItemLayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is not PdfPageItem cp)
        {
            return;
        }

        System.Diagnostics.Debug.WriteLine($"PdfPageItemLayoutUpdated: {cp.DataContext}");

        cp.LayoutUpdated -= PdfPageItemLayoutUpdated; // Only once

        SetPagesVisibility();
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
    {
        return new PdfPageItem();
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        return NeedsContainer<PdfPageItem>(item, out recycleKey);
    }

    /// <summary>
    /// Starts at 0. Inclusive.
    /// </summary>
    private int GetMinPageIndex()
    {
        if (ItemsPanelRoot is VirtualizingStackPanel v)
        {
            return Math.Max(0, v.FirstRealizedIndex);
        }
        return 0;
    }

    /// <summary>
    /// Starts at 0. Exclusive.
    /// </summary>
    private int GetMaxPageIndex()
    {
        if (ItemsPanelRoot is VirtualizingStackPanel v && v.LastRealizedIndex != -1)
        {
            return Math.Min(PageCount, v.LastRealizedIndex + 1);
        }

        return PageCount;
    }

    public PdfPageItem? GetPdfPageItemOver(PointerEventArgs e)
    {
        if (Presenter is null)
        {
            // Should never happen
            return null;
        }

        Point point = e.GetPosition(Presenter);

        // Quick reject
        if (!Presenter.Bounds.Contains(point))
        {
            System.Diagnostics.Debug.WriteLine("GetPdfPageItemOver Quick reject.");
            return null;
        }

        int minPageIndex = GetMinPageIndex();
        int maxPageIndex = GetMaxPageIndex(); // Exclusive

        int startIndex = SelectedPageIndex.HasValue ? SelectedPageIndex.Value - 1 : 0; // Switch from one-indexed to zero-indexed

        bool isAfterSelectedPage = false;

        // Check selected current page
        if (ContainerFromIndex(startIndex) is PdfPageItem presenter)
        {
            System.Diagnostics.Debug.WriteLine($"GetPdfPageItemOver page {startIndex + 1}.");
            if (presenter.Bounds.Contains(point))
            {
                return presenter;
            }

            isAfterSelectedPage = point.Y > presenter.Bounds.Bottom;
        }

        if (isAfterSelectedPage)
        {
            // Start with checking forward
            for (int p = startIndex + 1; p < maxPageIndex; ++p)
            {
                System.Diagnostics.Debug.WriteLine($"GetPdfPageItemOver page {p + 1}.");
                if (ContainerFromIndex(p) is not PdfPageItem cp)
                {
                    continue;
                }

                if (cp.Bounds.Contains(point))
                {
                    return cp;
                }

                if (point.Y < cp.Bounds.Top)
                {
                    return null;
                }
            }
        }
        else
        {
            // Continue with checking backward
            for (int p = startIndex - 1; p >= minPageIndex; --p)
            {
                System.Diagnostics.Debug.WriteLine($"GetPdfPageItemOver page {p + 1}.");
                if (ContainerFromIndex(p) is not PdfPageItem cp)
                {
                    continue;
                }

                if (cp.Bounds.Contains(point))
                {
                    return cp;
                }

                if (point.Y > cp.Bounds.Bottom)
                {
                    return null;
                }
            }
        }

        return null;
    }

    private Point? _currentPosition = null;

    internal void SetPanCursor()
    {
        Debug.ThrowNotOnUiThread();
        Cursor = App.PanCursor;
    }

    internal void SetDefaultCursor()
    {
        Debug.ThrowNotOnUiThread();
        Cursor = App.DefaultCursor;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        Scroll = e.NameScope.FindFromNameScope<ScrollViewer>("PART_ScrollViewer");
        Scroll.AddHandler(ScrollViewer.ScrollChangedEvent, (_, _) => SetPagesVisibility());
        Scroll.AddHandler(SizeChangedEvent, (_, _) => SetPagesVisibility(), RoutingStrategies.Direct);
        Scroll.AddHandler(KeyDownEvent, OnKeyDownHandler);
        Scroll.AddHandler(KeyUpEvent, OnKeyUpHandler);
        Scroll.Focus(); // Make sure the Scroll has focus

        LayoutTransform = e.NameScope.FindFromNameScope<LayoutTransformControl>("PART_LayoutTransformControl");
        LayoutTransform.AddHandler(PointerWheelChangedEvent, OnPointerWheelChangedHandler);
        LayoutTransform.AddHandler(PointerPressedEvent, OnPointerPressed);
        LayoutTransform.AddHandler(PointerMovedEvent, OnPointerMoved);
        LayoutTransform.AddHandler(PointerReleasedEvent, OnPointerReleased);
        
        _tabsControl = this.FindAncestorOfType<TabsControl>();
        if (_tabsControl is not null)
        {
            _tabsControl.TabDragStarted += TabControlOnTabDragStarted;
            _tabsControl.TabDragCompleted += TabControlOnTabDragCompleted;
        }

        if (CalyExtensions.IsMobilePlatform())
        {
            LayoutTransform.GestureRecognizers.Add(new PinchGestureRecognizer());
            Gestures.AddPinchHandler(LayoutTransform, _onPinchChangedHandler);
            Gestures.AddPinchEndedHandler(LayoutTransform, _onPinchEndedHandler);
            Gestures.AddHoldingHandler(LayoutTransform, _onHoldingChangedHandler);
        }
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        
        if (Scroll is not null)
        {
            Scroll.RemoveHandler(ScrollViewer.ScrollChangedEvent, (_, _) => SetPagesVisibility());
            Scroll.RemoveHandler(SizeChangedEvent, (_, _) => SetPagesVisibility());
            Scroll.RemoveHandler(KeyDownEvent, OnKeyDownHandler);
            Scroll.RemoveHandler(KeyUpEvent, OnKeyUpHandler);
        }

        if (LayoutTransform is not null)
        {
            LayoutTransform.RemoveHandler(PointerWheelChangedEvent, OnPointerWheelChangedHandler);
            LayoutTransform.RemoveHandler(PointerPressedEvent, OnPointerPressed);
            LayoutTransform.RemoveHandler(PointerMovedEvent, OnPointerMoved);
            LayoutTransform.RemoveHandler(PointerReleasedEvent, OnPointerReleased);
            
            if (CalyExtensions.IsMobilePlatform())
            {
                Gestures.RemovePinchHandler(LayoutTransform, _onPinchChangedHandler);
                Gestures.RemovePinchEndedHandler(LayoutTransform, _onPinchEndedHandler);
                LayoutTransform.RemoveHandler(Gestures.HoldingEvent, _onHoldingChangedHandler);
                //Gestures.RemoveHoldingHandler(LayoutTransformControl, _onHoldingChangedHandler);
            }
        }

        if (_tabsControl is not null)
        {
            _tabsControl.TabDragStarted -= TabControlOnTabDragStarted;
            _tabsControl.TabDragCompleted -= TabControlOnTabDragCompleted;
        }
    }
    
    private void TabControlOnTabDragStarted(object? sender, Tabalonia.Events.DragTabDragStartedEventArgs e)
    {
        _isTabDragging = true;
    }

    private void TabControlOnTabDragCompleted(object? sender, Tabalonia.Events.DragTabDragCompletedEventArgs e)
    {
        if (!_isTabDragging)
        {
            return;
        }
        
        _isTabDragging = false;
        foreach (Control cp in GetRealizedContainers())
        {
            if (cp.DataContext is PdfPageViewModel vm)
            {
                vm.VisibleArea = null;
                App.Messenger.Send(new LoadPageMessage(vm));
            }
        }
        SetPagesVisibility();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ItemsPanelRoot!.DataContextChanged += ItemsPanelRoot_DataContextChanged;
        ItemsPanelRoot.LayoutUpdated += ItemsPanelRoot_LayoutUpdated;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        ItemsPanelRoot!.DataContextChanged -= ItemsPanelRoot_DataContextChanged;
        ItemsPanelRoot.LayoutUpdated -= ItemsPanelRoot_LayoutUpdated;
    }

    /// <summary>
    /// The number of first layout updates to listen to.
    /// </summary>
    private int _layoutUpdateCount = 5;
    private void ItemsPanelRoot_LayoutUpdated(object? sender, EventArgs e)
    {
        // When ItemsPanelRoot is first loaded, there is a chance that a container
        // (i.e. the second page) is realised after the last SetPagesVisibility()
        // call. When this happens the page will not be rendered because it
        // is seen as 'not visible'.
        // To prevent that we listen to the first layout updates and check visibility.

        if (GetMaxPageIndex() > 0)
        {
            // We have enough containers realised, we can stop listening to layout updates.
            _layoutUpdateCount = 0;
        }
        
        if (_layoutUpdateCount == 0)
        {
            ItemsPanelRoot!.LayoutUpdated -= ItemsPanelRoot_LayoutUpdated;
        }
        
        try
        {
            SetPagesVisibility();
        }
        finally
        {
            _layoutUpdateCount--;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            if (change.OldValue is not IEnumerable<PdfPageViewModel> items)
            {
                return;
            }

            foreach (var vm in items)
            {
                System.Diagnostics.Debug.Assert(!vm.VisibleArea.HasValue);

                if (vm.PdfPicture is not null)
                {
                    // TODO - Check why this happens
                    App.Messenger.Send(new UnloadPageMessage(vm));
                }
            }
        }
        else if (change.Property == DataContextProperty)
        {
            Scroll?.Focus();

            if (ItemsPanelRoot is VirtualizingStackPanel panel)
            {
                // This is a hack to ensure PdfPageItem that belongs to not Active documents are not visible
                // See https://github.com/CalyPdf/Caly/issues/11
                var children = panel.Children.OfType<PdfPageItem>().ToArray();
                foreach (var child in children)
                {
                    if (child is { IsVisible: true, DataContext: PdfPageViewModel { PdfService.IsActive: false } })
                    {
                        child.SetCurrentValue(Visual.IsVisibleProperty, false);
                    }
                }
            }
        }
    }
    
    private void ItemsPanelRoot_DataContextChanged(object? sender, EventArgs e)
    {
        LayoutUpdated += OnLayoutUpdatedOnce;
    }

    private void OnLayoutUpdatedOnce(object? sender, EventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdatedOnce;

        // Ensure the pages visibility is set when OnApplyTemplate()
        // is not called, i.e. when a new document is opened but the
        // page has exactly the same dimension of the visible page
        SetPagesVisibility();
    }
    
    private bool HasRealisedItems()
    {
        if (ItemsPanelRoot is VirtualizingStackPanel vsp)
        {
            return vsp.FirstRealizedIndex != -1 && vsp.LastRealizedIndex != -1;
        }

        return false;
    }

    private bool IsPageRealised(PdfPageViewModel vm)
    {
        var index = ItemsView.IndexOf(vm);
        if (index >= 0 && ItemsPanelRoot is VirtualizingStackPanel vsp)
        {
            return index >= vsp.FirstRealizedIndex && index <= vsp.LastRealizedIndex;
        }

        return false;
    }

    private void SetPagesVisibility()
    {
        System.Diagnostics.Debug.WriteLine($"SetPagesVisibility: {(DataContext as PdfDocumentViewModel)}");

        if (_isSettingPageVisibility || _isTabDragging)
        {
            return;
        }

        if (_isZooming)
        {
            return;
        }

        if (LayoutTransform is null || Scroll is null ||
            Scroll.Viewport.IsEmpty() || ItemsView.Count == 0 ||
            !HasRealisedItems())
        {
            return;
        }

        Debug.AssertIsNullOrScale(LayoutTransform.LayoutTransform?.Value);

        double invScale = 1.0 / (LayoutTransform.LayoutTransform?.Value.M11 ?? 1.0);
        Matrix fastInverse = Matrix.CreateScale(invScale, invScale);

        Rect viewPort = Scroll.GetViewportRect().TransformToAABB(fastInverse);

        // Use the following: not visible pages cannot be between visible pages
        // We cannot have:
        // nv - v - v - nv - v - nv
        // We will always have:
        // nv - v - v - v - nv - nv
        //
        // There are 3 possible splits:
        // [nv] | [v] [nv]
        // [nv] [v] | [nv]
        //
        // [nv] | [nv] [v] [nv]
        // [nv] [v] [nv] | [nv]
        //
        // [nv] [v] | [v] [nv]

        bool isPreviousPageVisible = false;
        bool needMoreChecks = true;

        double maxOverlap = double.MinValue;
        int indexMaxOverlap = -1;

        bool CheckPageVisibility(int p, out bool isPageVisible)
        {
            isPageVisible = false;

            if (ContainerFromIndex(p) is not PdfPageItem { Content: PdfPageViewModel vm } cp)
            {
                // Page is not realised
                return !isPreviousPageVisible;
            }

            if (!needMoreChecks || cp.Bounds.IsEmpty())
            {
                if (!vm.IsPageVisible)
                {
                    // Page is not visible and no need for more checks.
                    // All following pages are already set to IsPageVisible = false
                    return false;
                }

                vm.VisibleArea = null;
                return true;
            }

            Rect view = cp.Bounds;

            if (view.Height == 0)
            {
                // No need for further checks, not visible
                vm.VisibleArea = null;
                return true;
            }

            double vmWidth = vm.IsPortrait ? vm.Width : vm.Height;
            if (Math.Abs(view.Width - vmWidth) > double.Epsilon)
            {
                double delta = (view.Width - vmWidth) / 2.0; // Centered
                view = new Rect(
                    view.Position.X + delta,
                    view.Position.Y,
                    vmWidth,
                    view.Height);
            }

            double top = view.Top;
            double left = view.Left;
            double bottom = view.Bottom;

            // Quick check if height overlap
            if (OverlapsHeight(viewPort.Top, viewPort.Bottom, top, bottom))
            {
                // Compute overlap
                view = view.Intersect(viewPort);

                double overlapArea = view.Height * view.Width;

                // Actual check if page is visible
                if (overlapArea == 0)
                {
                    vm.VisibleArea = null;
                    // If previous page was visible but current page is not, we have the last visible page
                    needMoreChecks = !isPreviousPageVisible;
                    return true;
                }

                System.Diagnostics.Debug.Assert(view.Height.Equals(Overlap(viewPort.Top, viewPort.Bottom, top, bottom)));

                if (overlapArea > maxOverlap)
                {
                    maxOverlap = overlapArea;
                    indexMaxOverlap = p;
                }

                isPreviousPageVisible = true;
                isPageVisible = true;

                // Set overlap area (Translate and inverse transform)
                Rect visibleArea = view.Translate(new Vector(-left, -top));

                switch (vm.Rotation)
                {
                    case 90:
                        visibleArea = new Rect(visibleArea.Y, cp.Bounds.Width - visibleArea.Right, visibleArea.Height, visibleArea.Width);
                        break;

                    case 180:
                        visibleArea = new Rect(cp.Bounds.Width - visibleArea.Right, cp.Bounds.Height - visibleArea.Bottom, visibleArea.Width, visibleArea.Height);
                        break;

                    case 270:
                        visibleArea = new Rect(cp.Bounds.Height - visibleArea.Bottom, visibleArea.X, visibleArea.Height, visibleArea.Width);
                        break;

#if DEBUG
                    default:
                        System.Diagnostics.Debug.Assert(vm.Rotation == 0);
                        break;
#endif
                }

                vm.VisibleArea = visibleArea;

                return true;
            }

            vm.VisibleArea = null;
            // If previous page was visible but current page is not, we have the last visible page
            needMoreChecks = !isPreviousPageVisible;
            return true;
        }

        // Check current page visibility
        int startIndex = SelectedPageIndex.HasValue ? SelectedPageIndex.Value - 1 : 0; // Switch from one-indexed to zero-indexed
        CheckPageVisibility(startIndex, out bool isSelectedPageVisible);

        int minPageIndex = GetMinPageIndex();
        int maxPageIndex = GetMaxPageIndex(); // Exclusive

        System.Diagnostics.Debug.WriteLine($"SetPagesVisibility: minPageIndex={minPageIndex} maxPageIndex={maxPageIndex}");
        
        // Start with checking forward.
        // TODO - While scrolling down, the current selected page can become invisible and force
        // a full iteration if starting backward
        isPreviousPageVisible = isSelectedPageVisible; // Previous page is SelectedPageIndex
        int forwardIndex = startIndex + 1;
        while (forwardIndex < maxPageIndex && CheckPageVisibility(forwardIndex, out _))
        {
            forwardIndex++;
        }

        // Continue with checking backward
        isPreviousPageVisible = isSelectedPageVisible; // Previous page is SelectedPageIndex
        needMoreChecks = true;
        int backwardIndex = startIndex - 1;
        while (backwardIndex >= minPageIndex && CheckPageVisibility(backwardIndex, out _))
        {
            backwardIndex--;
        }

        indexMaxOverlap++; // Switch to base 1 indexing

        if (indexMaxOverlap == 0 || SelectedPageIndex == indexMaxOverlap)
        {
            return;
        }

        try
        {
            _isSettingPageVisibility = true;
            SetCurrentValue(SelectedPageIndexProperty, indexMaxOverlap);
        }
        finally
        {
            _isSettingPageVisibility = false;
        }
    }

    private static double Overlap(double top1, double bottom1, double top2, double bottom2)
    {
        return Math.Max(0, Math.Min(bottom1, bottom2) - Math.Max(top1, top2));
    }

    /// <summary>
    /// Works for vertical scrolling.
    /// </summary>
    private static bool OverlapsHeight(double top1, double bottom1, double top2, double bottom2)
    {
        return !(top1 > bottom2 || bottom1 < top2);
    }

    private void OnKeyUpHandler(object? sender, KeyEventArgs e)
    {
        if (Scroll is not null)
        {
            // We re-subscribe to key down events, even if no
            // unsubscribe happened.
            Scroll.RemoveHandler(KeyDownEvent, OnKeyDownHandler);
            Scroll.AddHandler(KeyDownEvent, OnKeyDownHandler);
        }
        
        if (e.IsPanningOrZooming())
        {
            ResetPanTo();
        }
    }

    private void OnKeyDownHandler(object? sender, KeyEventArgs e)
    {
        if (e.IsPanningOrZooming())
        {
            // We stop listening to key down events when panning / zooming.
            // Keeping the 'ctrl' key down involves continuously firing 
            // key down events.
            Scroll?.RemoveHandler(KeyDownEvent, OnKeyDownHandler);
            ResetPanTo();
        }
        
        switch (e.Key)
        {
            case Key.Right:
                Scroll!.PageDown();
                break;
            case Key.Down:
                Scroll!.LineDown();
                break;
            case Key.Left:
                Scroll!.PageUp();
                break;
            case Key.Up:
                Scroll!.LineUp();
                break;
        }
    }

    #region Mobile handling

    private void _onHoldingChangedHandler(object? sender, HoldingRoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Holding {e.HoldingState}: {e.Position.X}, {e.Position.Y}");
    }

    private double _pinchZoomReference = 1.0;
    private void _onPinchEndedHandler(object? sender, PinchEndedEventArgs e)
    {
        _pinchZoomReference = ZoomLevel;
    }

    private void _onPinchChangedHandler(object? sender, PinchEventArgs e)
    {
        if (e.Scale != 0)
        {
            ZoomTo(e);
            e.Handled = true;
        }
    }

    private void ZoomTo(PinchEventArgs e)
    {
        if (LayoutTransform is null)
        {
            return;
        }

        if (_isZooming)
        {
            return;
        }

        try
        {
            _isZooming = true;

            // Pinch zoom always starts with a scale of 1, then increase/decrease until PinchEnded
            double dZoom = (e.Scale * _pinchZoomReference) / ZoomLevel;

            // TODO - Origin still not correct
            var point = LayoutTransform.PointToClient(new PixelPoint((int)e.ScaleOrigin.X, (int)e.ScaleOrigin.Y));
            ZoomToInternal(dZoom, point);
            SetCurrentValue(ZoomLevelProperty, LayoutTransform.LayoutTransform?.Value.M11);
        }
        finally
        {
            _isZooming = false;
        }
    }
    #endregion

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.IsPanning())
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        _currentPosition = point.Position;
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!e.IsPanningOrZooming())
        {
            return;
        }
        
        if (e.IsPanning())
        {
            SetPanCursor();
            PanTo(e);
        }
        
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        ResetPanTo();
    }

    private void PanTo(PointerEventArgs e)
    {
        if (Scroll is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);

        if (!_currentPosition.HasValue)
        {
            _currentPosition = point.Position;
            return;
        }

        var delta = point.Position - _currentPosition;

        var offset = Scroll.Offset - delta.Value;
        Scroll.SetCurrentValue(ScrollViewer.OffsetProperty, offset);
        _currentPosition = point.Position;
    }

    private void ResetPanTo()
    {
        _currentPosition = null;
        SetDefaultCursor();
    }

    private void OnPointerWheelChangedHandler(object? sender, PointerWheelEventArgs e)
    {
        var hotkeys = Application.Current!.PlatformSettings?.HotkeyConfiguration;
        var ctrl = hotkeys is not null && e.KeyModifiers.HasFlag(hotkeys.CommandModifiers);

        if (ctrl && e.Delta.Y != 0)
        {
            ZoomTo(e);
            e.Handled = true;
            e.PreventGestureRecognition();
        }
    }

    private void ZoomTo(PointerWheelEventArgs e)
    {
        if (LayoutTransform is null)
        {
            return;
        }

        if (_isZooming)
        {
            return;
        }

        try
        {
            _isZooming = true;
            double dZoom = Math.Round(Math.Pow(_zoomFactor, e.Delta.Y), 4); // If IsScrollInertiaEnabled = false, Y is only 1 or -1
            ZoomToInternal(dZoom, e.GetPosition(LayoutTransform));
            SetCurrentValue(ZoomLevelProperty, LayoutTransform.LayoutTransform?.Value.M11);
        }
        finally
        {
            _isZooming = false;
        }
    }

    internal void ZoomTo(double dZoom, Point point)
    {
        if (LayoutTransform is null || Scroll is null)
        {
            return;
        }

        if (_isZooming)
        {
            return;
        }

        try
        {
            _isZooming = true;
            ZoomToInternal(dZoom, point);
        }
        finally
        {
            _isZooming = false;
        }
    }

    private void ZoomToInternal(double dZoom, Point point)
    {
        if (LayoutTransform is null || Scroll is null)
        {
            return;
        }

        double oldZoom = LayoutTransform.LayoutTransform?.Value.M11 ?? 1.0;
        double newZoom = oldZoom * dZoom;

        if (newZoom < MinZoomLevel)
        {
            if (oldZoom.Equals(MinZoomLevel))
            {
                return;
            }

            newZoom = MinZoomLevel;
            dZoom = newZoom / oldZoom;
        }
        else if (newZoom > MaxZoomLevel)
        {
            if (oldZoom.Equals(MaxZoomLevel))
            {
                return;
            }

            newZoom = MaxZoomLevel;
            dZoom = newZoom / oldZoom;
        }

        var builder = TransformOperations.CreateBuilder(1);
        builder.AppendScale(newZoom, newZoom);
        LayoutTransform.LayoutTransform = builder.Build();

        var offset = Scroll.Offset - GetOffset(dZoom, point.X, point.Y);
        if (newZoom > oldZoom)
        {
            // When zooming-in, we need to re-arrange the scroll viewer
            Scroll.Measure(Size.Infinity);
            Scroll.Arrange(new Rect(Scroll.DesiredSize));
        }

        Scroll.SetCurrentValue(ScrollViewer.OffsetProperty, offset);
    }

    private static Vector GetOffset(double scale, double x, double y)
    {
        double s = 1 - scale;
        return new Vector(x * s, y * s);
    }
}