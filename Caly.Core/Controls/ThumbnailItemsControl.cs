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
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Caly.Core.Utilities;
using System;
using System.Linq;
using System.Windows.Input;

namespace Caly.Core.Controls;

public sealed class ThumbnailItemsControl : ListBox
{
    private bool _isScrollingToPage;

    private ScrollViewer? _scrollViewer;

    protected override Type StyleKeyOverride => typeof(ListBox);

    /// <summary>
    /// Defines the <see cref="RealisedThumbnails"/> property. Starts at 1.
    /// </summary>
    public static readonly StyledProperty<Range?> RealisedThumbnailsProperty =
        AvaloniaProperty.Register<ThumbnailItemsControl, Range?>(nameof(RealisedThumbnails), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Defines the <see cref="VisibleThumbnails"/> property. Starts at 1.
    /// </summary>
    public static readonly StyledProperty<Range?> VisibleThumbnailsProperty =
        AvaloniaProperty.Register<ThumbnailItemsControl, Range?>(nameof(VisibleThumbnails), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Defines the <see cref="RefreshThumbnails"/> property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> RefreshThumbnailsProperty =
        AvaloniaProperty.Register<ThumbnailItemsControl, ICommand?>(nameof(RefreshThumbnails));

    public ICommand? RefreshThumbnails
    {
        get => GetValue(RefreshThumbnailsProperty);
        set => SetValue(RefreshThumbnailsProperty, value);
    }

    /// <summary>
    /// Starts at 1.
    /// </summary>
    public Range? RealisedThumbnails
    {
        get => GetValue(RealisedThumbnailsProperty);
        set => SetValue(RealisedThumbnailsProperty, value);
    }

    /// <summary>
    /// Starts at 1.
    /// </summary>
    public Range? VisibleThumbnails
    {
        get => GetValue(VisibleThumbnailsProperty);
        set => SetValue(VisibleThumbnailsProperty, value);
    }

    public ThumbnailItemsControl()
    {
        ResetState();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _scrollViewer = Scroll as ScrollViewer ?? throw new Exception("Scroll is not ScrollViewer.");
        _scrollViewer.AddHandler(ScrollViewer.ScrollChangedEvent, (_, _) => PostUpdateThumbnailsVisibility());
        _scrollViewer.AddHandler(SizeChangedEvent, (_, _) => PostUpdateThumbnailsVisibility(), RoutingStrategies.Direct);
        _scrollViewer.AddHandler(LoadedEvent, (_, _) => PostUpdateThumbnailsVisibility());
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);

        if (_scrollViewer is not null)
        {
            _scrollViewer.RemoveHandler(ScrollViewer.ScrollChangedEvent, (_, _) => PostUpdateThumbnailsVisibility());
            _scrollViewer.RemoveHandler(SizeChangedEvent, (_, _) => PostUpdateThumbnailsVisibility());
            _scrollViewer.RemoveHandler(LoadedEvent, (_, _) => PostUpdateThumbnailsVisibility());
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ItemsPanelRoot!.LayoutUpdated += ItemsPanelRoot_LayoutUpdated;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        ItemsPanelRoot!.LayoutUpdated -= ItemsPanelRoot_LayoutUpdated;
    }

    private void ItemsPanelRoot_LayoutUpdated(object? sender, EventArgs e)
    {
        // When ItemsPanelRoot is first loaded, there is a chance that a container
        // (i.e. the second page) is realised after the last SetPagesVisibility()
        // call. When this happens the page will not be rendered because it
        // is seen as 'not visible'.
        // To prevent that we listen to the first layout updates and check visibility.

        if (GetMaxPageIndex() > 0 && UpdateThumbnailsVisibility())
        {
            // We have enough containers realised, we can stop listening to layout updates.
            ItemsPanelRoot!.LayoutUpdated -= ItemsPanelRoot_LayoutUpdated;
        }
    }

    private void PostUpdateThumbnailsVisibility()
    {
        Dispatcher.UIThread.Post(() => UpdateThumbnailsVisibility(), DispatcherPriority.Loaded);
    }

    private bool UpdateThumbnailsVisibility()
    {
        if (_scrollViewer is null)
        {
            return false;
        }

        if (_isScrollingToPage)
        {
            return false;
        }

        int firstRealisedIndex = GetMinPageIndex();
        int lastRealisedIndex = GetMaxPageIndex();

        if (firstRealisedIndex == -1 || lastRealisedIndex == -1)
        {
            SetCurrentValue(RealisedThumbnailsProperty, null);

            if (VisibleThumbnails.HasValue)
            {
                SetCurrentValue(VisibleThumbnailsProperty, null);
                RefreshThumbnails?.Execute(null);
            }
            
            return true;
        }

        bool previousVisible = false;
        int firstVisibleIndex = -1;
        int lastVisibleIndex = -1;
        for (int index = firstRealisedIndex; index < lastRealisedIndex; ++index)
        {
            if (ContainerFromIndex(index) is not ThumbnailItem thumbnailItem)
            {
                continue;
            }

            // Check thumbnails visibility
            if (_scrollViewer.GetViewportRect().Intersects(thumbnailItem.Bounds))
            {
                // Visible
                if (!previousVisible)
                {
                    firstVisibleIndex = index;
                    lastVisibleIndex = index;
                    previousVisible = true;
                }
                else
                {
                    lastVisibleIndex = index;
                }
            }
            else
            {
                // Not visible
                if (previousVisible)
                {
                    break;
                }
            }
        }

        // Update bound properties
        SetCurrentValue(RealisedThumbnailsProperty, new Range(firstRealisedIndex + 1, lastRealisedIndex + 2));

        Range? currentVisibleThumbnails = null;
        if (firstVisibleIndex != -1 && lastVisibleIndex != -1) // No visible pages
        {
            currentVisibleThumbnails = new Range(firstVisibleIndex + 1, lastVisibleIndex + 2);
        }

        if (!VisibleThumbnails.HasValue || !VisibleThumbnails.Value.Equals(currentVisibleThumbnails))
        {
            SetCurrentValue(VisibleThumbnailsProperty, currentVisibleThumbnails);
            RefreshThumbnails?.Execute(null);
        }

        return true;
    }

    /// <summary>
    /// Starts at 0. Inclusive.
    /// </summary>
    private int GetMinPageIndex()
    {
        if (ItemsPanelRoot is VirtualizingStackPanel v)
        {
            return v.FirstRealizedIndex;
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
            if (v.LastRealizedIndex == -1)
            {
                return -1;
            }

            return Math.Min(ItemCount, v.LastRealizedIndex + 1);
        }

        return ItemCount;
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
    {
        return new ThumbnailItem();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DataContextProperty)
        {
            ResetState();
            EnsureValidContainersVisibility();
            ItemsPanelRoot?.LayoutUpdated += ItemsPanelRoot_LayoutUpdated;
        }
        else if (change.Property == IsVisibleProperty)
        {
            if (change is { OldValue: false, NewValue: true })
            {
                try
                {
                    _isScrollingToPage = true;
                    ScrollIntoView(SelectedIndex);
                }
                finally
                {
                    _isScrollingToPage = false;
                }

                EnsureValidContainersVisibility();
                PostUpdateThumbnailsVisibility();
            }
            else if (change is { OldValue: true, NewValue: false })
            {
                // Thumbnails control is hidden
                SetCurrentValue(RealisedThumbnailsProperty, null);
                SetCurrentValue(VisibleThumbnailsProperty, null);
                RefreshThumbnails?.Execute(null);
            }
        }
    }

    private void EnsureValidContainersVisibility()
    {
        // This is a hack to ensure only valid containers (realised) are visible
        // See https://github.com/CalyPdf/Caly/issues/11

        if (ItemsPanelRoot is null)
        {
            return;
        }

        var realised = GetRealizedContainers().OfType<ThumbnailItem>().ToArray();
        var visibleChildren = ItemsPanelRoot.Children.Where(c => c.IsVisible).OfType<ThumbnailItem>().ToArray();

        if (realised.Length != visibleChildren.Length)
        {
            foreach (var child in visibleChildren.Except(realised))
            {
                child.SetCurrentValue(IsVisibleProperty, false);
            }
        }
    }
    
    private void ResetState()
    {
        SetCurrentValue(VisibleThumbnailsProperty, null);
        _isScrollingToPage = false;
    }
}
