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
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Transformation;
using Avalonia.VisualTree;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
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
    private static readonly FuncTemplate<Panel?> DefaultPanel = new(() => new VirtualizingStackPanel());

    /// <summary>
    /// Defines the <see cref="Scroll"/> property.
    /// </summary>
    public static readonly DirectProperty<PdfPageItemsControl, ScrollViewer?> ScrollProperty =
        AvaloniaProperty.RegisterDirect<PdfPageItemsControl, ScrollViewer?>(nameof(Scroll), o => o.Scroll);

    /// <summary>
    /// Defines the <see cref="LayoutTransformControl"/> property.
    /// </summary>
    public static readonly DirectProperty<PdfPageItemsControl, LayoutTransformControl?> LayoutTransformControlProperty =
        AvaloniaProperty.RegisterDirect<PdfPageItemsControl, LayoutTransformControl?>(nameof(LayoutTransformControl), o => o.LayoutTransformControl);

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
    private LayoutTransformControl? _layoutTransformControl;
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
    public LayoutTransformControl? LayoutTransformControl
    {
        get => _layoutTransformControl;
        private set => SetAndRaise(LayoutTransformControlProperty, ref _layoutTransformControl, value);
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
            container is not PdfPageItem cp ||
            item is not PdfPageViewModel vm)
        {
            System.Diagnostics.Debug.WriteLine($"Skipping LoadPage() for page {index + 1} (IsTabDragging: {_isTabDragging})");
            return;
        }

        cp.PropertyChanged += OnContainerPropertyChanged;
        vm.VisibleArea = null;
        vm.LoadPage();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);

        if (container is not PdfPageItem cp)
        {
            return;
        }

        cp.PropertyChanged -= OnContainerPropertyChanged;
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
    {
        return new PdfPageItem();
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        return NeedsContainer<PdfPageItem>(item, out recycleKey);
    }

    private static void OnContainerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ContentPresenter.ContentProperty &&
            e.OldValue is PdfPageViewModel vm)
        {
            vm.VisibleArea = null;
            vm.UnloadPage();
        }
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

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        Scroll = e.NameScope.FindFromNameScope<ScrollViewer>("PART_ScrollViewer");
        Scroll.AddHandler(ScrollViewer.ScrollChangedEvent, (_, _) => SetPagesVisibility());
        Scroll.AddHandler(SizeChangedEvent, (_, _) => SetPagesVisibility(), RoutingStrategies.Direct);
        Scroll.AddHandler(KeyDownEvent, OnKeyDownHandler);
        Scroll.Focus(); // Make sure the Scroll has focus

        LayoutTransformControl = e.NameScope.FindFromNameScope<LayoutTransformControl>("PART_LayoutTransformControl");
        LayoutTransformControl.AddHandler(PointerWheelChangedEvent, OnPointerWheelChangedHandler);

        _tabsControl = this.FindAncestorOfType<TabsControl>();
        if (_tabsControl is not null)
        {
            _tabsControl.OnTabDragStarted += TabControlOnTabDragStarted;
            _tabsControl.OnTabDragCompleted += TabControlOnTabDragCompleted;
        }

        if (CalyExtensions.IsMobilePlatform())
        {
            LayoutTransformControl.GestureRecognizers.Add(new PinchGestureRecognizer());
            Gestures.AddPinchHandler(LayoutTransformControl, _onPinchChangedHandler);
            Gestures.AddPinchEndedHandler(LayoutTransformControl, _onPinchEndedHandler);
            Gestures.AddHoldingHandler(LayoutTransformControl, _onHoldingChangedHandler);
        }
    }

    private void TabControlOnTabDragStarted(object? sender, Tabalonia.Events.DragTabDragStartedEventArgs e)
    {
        _isTabDragging = true;
    }

    private void TabControlOnTabDragCompleted(object? sender, Tabalonia.Events.DragTabDragCompletedEventArgs e)
    {
        _isTabDragging = false;
        foreach (Control cp in this.GetRealizedContainers())
        {
            if (cp.DataContext is PdfPageViewModel vm)
            {
                cp.PropertyChanged += OnContainerPropertyChanged;
                vm.VisibleArea = null;
                vm.LoadPage();
            }
        }
        SetPagesVisibility();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ItemsPanelRoot!.DataContextChanged += ItemsPanelRoot_DataContextChanged;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataContextProperty)
        {
            Scroll?.Focus();
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

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);

        Scroll?.RemoveHandler(ScrollViewer.ScrollChangedEvent, (_, _) => SetPagesVisibility());
        Scroll?.RemoveHandler(SizeChangedEvent, (_, _) => SetPagesVisibility());
        Scroll?.RemoveHandler(KeyDownEvent, OnKeyDownHandler);
        LayoutTransformControl?.RemoveHandler(PointerWheelChangedEvent, OnPointerWheelChangedHandler);
        ItemsPanelRoot!.DataContextChanged -= ItemsPanelRoot_DataContextChanged;

        if (_tabsControl is not null)
        {
            _tabsControl.OnTabDragStarted -= TabControlOnTabDragStarted;
            _tabsControl.OnTabDragCompleted -= TabControlOnTabDragCompleted;
        }

        if (CalyExtensions.IsMobilePlatform() && LayoutTransformControl is not null)
        {
            Gestures.RemovePinchHandler(LayoutTransformControl, _onPinchChangedHandler);
            Gestures.RemovePinchEndedHandler(LayoutTransformControl, _onPinchEndedHandler);
            LayoutTransformControl.RemoveHandler(Gestures.HoldingEvent, _onHoldingChangedHandler);
            //Gestures.RemoveHoldingHandler(LayoutTransformControl, _onHoldingChangedHandler);
        }
    }

    private bool HasRealisedItems()
    {
        if (ItemsPanelRoot is VirtualizingStackPanel vsp)
        {
            return vsp.FirstRealizedIndex != -1 && vsp.LastRealizedIndex != -1;
        }

        return false;
    }

    private void SetPagesVisibility()
    {
        if (_isSettingPageVisibility || _isTabDragging)
        {
            return;
        }

        if (_isZooming)
        {
            return;
        }

        if (LayoutTransformControl is null || Scroll is null ||
            Scroll.Viewport.IsEmpty() || ItemsView.Count == 0 ||
            !HasRealisedItems())
        {
            return;
        }

        Debug.AssertIsNullOrScale(LayoutTransformControl.LayoutTransform?.Value);

        double invScale = 1.0 / (LayoutTransformControl.LayoutTransform?.Value.M11 ?? 1.0);
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

    private void OnKeyDownHandler(object? sender, KeyEventArgs e)
    {
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
        if (LayoutTransformControl is null)
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
            var point = LayoutTransformControl.PointToClient(new PixelPoint((int)e.ScaleOrigin.X, (int)e.ScaleOrigin.Y));
            ZoomToInternal(dZoom, point);
            SetCurrentValue(ZoomLevelProperty, LayoutTransformControl.LayoutTransform?.Value.M11);
        }
        finally
        {
            _isZooming = false;
        }
    }
    #endregion

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
        if (LayoutTransformControl is null)
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
            ZoomToInternal(dZoom, e.GetPosition(LayoutTransformControl));
            SetCurrentValue(ZoomLevelProperty, LayoutTransformControl.LayoutTransform?.Value.M11);
        }
        finally
        {
            _isZooming = false;
        }
    }

    internal void ZoomTo(double dZoom, Point point)
    {
        if (LayoutTransformControl is null || Scroll is null)
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
        if (LayoutTransformControl is null || Scroll is null)
        {
            return;
        }

        double oldZoom = LayoutTransformControl.LayoutTransform?.Value.M11 ?? 1.0;
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
        LayoutTransformControl.LayoutTransform = builder.Build();

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