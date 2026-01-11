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
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Caly.Core.Events;
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Utilities;
using Caly.Pdf.Models;
using System;
using System.Reactive.Disposables;

namespace Caly.Core.Controls;

/// <summary>
/// Control that represents the text layer of a PDF page, handling text selection and interaction.
/// </summary>
public sealed class PageInteractiveLayerControl : Control
{
    // https://github.com/AvaloniaUI/Avalonia/blob/master/src/Avalonia.Controls/Primitives/TextSelectionCanvas.cs#L62
    // Check caret handle

    private Point? _startPointerPressed;
    private bool _isDragging;

    private DocumentControl? _documentControl;
    private CompositeDisposable? _pointerDisposables;

    public static readonly StyledProperty<PdfTextLayer?> PdfTextLayerProperty =
        AvaloniaProperty.Register<PageInteractiveLayerControl, PdfTextLayer?>(nameof(PdfTextLayer));

    public static readonly StyledProperty<int?> PageNumberProperty =
        AvaloniaProperty.Register<PageInteractiveLayerControl, int?>(nameof(PageNumber));

    public static readonly StyledProperty<IPageInteractiveLayerHandler?> PageInteractiveLayerHandlerProperty =
        AvaloniaProperty.Register<PageInteractiveLayerControl, IPageInteractiveLayerHandler?>(
            nameof(PageInteractiveLayerHandler));

    public static readonly StyledProperty<bool> SelectionChangedFlagProperty =
        AvaloniaProperty.Register<PageInteractiveLayerControl, bool>(nameof(SelectionChangedFlag));

    /// <summary>
    /// Defines the <see cref="VisibleArea"/> property.
    /// </summary>
    public static readonly StyledProperty<Rect?> VisibleAreaProperty =
        AvaloniaProperty.Register<PageInteractiveLayerControl, Rect?>(nameof(VisibleArea));
    
    public event EventHandler<PageTextSelectionChangedEventArgs>? PageTextSelectionChanged;
    public event EventHandler<PageInteractiveLayerPointerPressedEventArgs>? PageInteractiveLayerPointerPressed;
    public event EventHandler<PageInteractiveLayerPointerReleasedEventArgs>? PageInteractiveLayerPointerReleased;
    public event EventHandler<PageInteractiveLayerPointerMovedEventArgs>? PageInteractiveLayerPointerMoved;
    public event EventHandler<PageInteractiveLayerPointerExitedEventArgs>? PageInteractiveLayerPointerExited;

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

    public IPageInteractiveLayerHandler? PageInteractiveLayerHandler
    {
        get => GetValue(PageInteractiveLayerHandlerProperty);
        set => SetValue(PageInteractiveLayerHandlerProperty, value);
    }

    public bool SelectionChangedFlag
    {
        get => GetValue(SelectionChangedFlagProperty);
        set => SetValue(SelectionChangedFlagProperty, value);
    }

    public Rect? VisibleArea
    {
        get => GetValue(VisibleAreaProperty);
        set => SetValue(VisibleAreaProperty, value);
    }

    static PageInteractiveLayerControl()
    {
        AffectsRender<PageInteractiveLayerControl>(PdfTextLayerProperty, SelectionChangedFlagProperty,
            VisibleAreaProperty);
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

        DebugRender.RenderAnnotations(this, context, VisibleArea.Value);
        DebugRender.RenderText(this, context, VisibleArea.Value);

        PageInteractiveLayerHandler?.RenderPage(this, context, VisibleArea.Value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageInteractiveLayerHandlerProperty)
        {
            // If the pageInteractiveLayerHandler was already attached, we unsubscribe
            _pointerDisposables?.Dispose();

            if (PageInteractiveLayerHandler is not null)
            {
                var pointerWheelChangedDisposable = this.GetObservable(PointerWheelChangedEvent, handledEventsToo: true)
                    .Subscribe(PageInteractiveLayerHandler!.OnPointerMoved);

                var pointerMovedDisposable = this.GetObservable(PointerMovedEvent, handledEventsToo: false)
                    .Subscribe(PageInteractiveLayerHandler!.OnPointerMoved);

                var pointerPressedDisposable = this.GetObservable(PointerPressedEvent, handledEventsToo: false)
                    .Subscribe(PageInteractiveLayerHandler.OnPointerPressed);

                var pointerReleasedDisposable = this.GetObservable(PointerReleasedEvent, handledEventsToo: false)
                    .Subscribe(PageInteractiveLayerHandler.OnPointerReleased);

                _pointerDisposables = new CompositeDisposable(
                    pointerMovedDisposable,
                    pointerWheelChangedDisposable,
                    pointerPressedDisposable,
                    pointerReleasedDisposable);
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _documentControl = this.FindAncestorOfType<DocumentControl>() ??
                           throw new NullReferenceException($"{typeof(DocumentControl)} not found.");
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _pointerDisposables?.Dispose();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (PdfTextLayer is null)
        {
            return;
        }

        if (e.IsPanningOrZooming())
        {
            // Panning pages is not handled here
            return;
        }

        var pointerPoint = e.GetCurrentPoint(this);
        var point = pointerPoint.Position;

        // TODO - Looks like there's a bug in Avalonia (TBC) where the position of the pointer
        // is 1 step behind the actual position.
        // We need to add back this step (1 scroll step is 50, see link below)
        // https://github.com/AvaloniaUI/Avalonia/blob/dadc9ab69284bb228ad460f36d5442b4eee4a82a/src/Avalonia.Controls/Presenters/ScrollContentPresenter.cs#L684

        var adjPoint = new Point(50, 50);
        var matrix = GetLayoutTransformMatrix();

        if (!matrix.IsIdentity && matrix.TryInvert(out var inverted))
        {
            adjPoint = inverted.Transform(adjPoint);
        }

        double x = Math.Max(point.X - e.Delta.X * adjPoint.X, 0);
        double y = Math.Max(point.Y - e.Delta.Y * adjPoint.Y, 0);

        point = new Point(x, y);

        // TODO - We have an issue when scrolling and changing page here, similar the TrySwitchCapture
        // not sure how we should address it

        HandlePointerMove(point, pointerPoint.Properties);
    }

    private void HandlePointerMove(Point point, PointerPointProperties properties)
    {
        if (!_isDragging && properties.IsLeftButtonPressed && _startPointerPressed.HasValue)
        {
            _isDragging = _startPointerPressed.Value.Euclidean(point) > 1.0;
        }

        PdfWord? word = PdfTextLayer!.FindWordOver(point.X, point.Y);
        if (word is not null)
        {
            SetIbeamCursor();
        }
        else if (!_isDragging)
        {
            SetDefaultCursor();
        }

        PdfAnnotation? annotation = PdfTextLayer.FindAnnotationOver(point.X, point.Y);
        if (annotation is not null)
        {
            if (!string.IsNullOrEmpty(annotation.Content))
            {
                ShowAnnotation(annotation);
            }

            if (annotation.IsInteractive)
            {
                SetHandCursor();
            }
        }
        else
        {
            HideAnnotation();
        }

        PageInteractiveLayerPointerMoved?.Invoke(this,
            new PageInteractiveLayerPointerMovedEventArgs(PageNumber!.Value,
                point, properties, word, annotation, _isDragging ? _startPointerPressed!.Value : null));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (PdfTextLayer is null)
        {
            return;
        }

        /*
        if (e.IsPanningOrZooming())
        {
            // Panning pages is not handled here
            return;
        }
        */

        var pointerPoint = e.GetCurrentPoint(this);
        var point = pointerPoint.Position;

        HandlePointerMove(point, pointerPoint.Properties);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (PdfTextLayer is null)
        {
            return;
        }

        /*
        if (e.IsPanningOrZooming())
        {
            // Panning pages is not handled here
            return;
        }
        */

        _startPointerPressed = null;
        _isDragging = false;

        var pointerPoint = e.GetCurrentPoint(this);
        var point = pointerPoint.Position;

        PdfWord? word = PdfTextLayer.FindWordOver(point.X, point.Y);
        PdfAnnotation? annotation = PdfTextLayer.FindAnnotationOver(point.X, point.Y);

        PageInteractiveLayerPointerReleased?.Invoke(this, new PageInteractiveLayerPointerReleasedEventArgs(PageNumber!.Value, point, pointerPoint.Properties, word, annotation));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (PdfTextLayer is null)
        {
            return;
        }

        if (e.IsPanningOrZooming())
        {
            // Panning pages is not handled here
            HideAnnotation();
            return;
        }

        var pointerPoint = e.GetCurrentPoint(this);
        var point = pointerPoint.Position;

        if (pointerPoint.Properties.IsLeftButtonPressed)
        {
            _startPointerPressed = point;
        }

        PdfWord? word = PdfTextLayer.FindWordOver(point.X, point.Y);
        PdfAnnotation? annotation = PdfTextLayer.FindAnnotationOver(point.X, point.Y);

        PageInteractiveLayerPointerPressed?.Invoke(this, new PageInteractiveLayerPointerPressedEventArgs(PageNumber!.Value, point, pointerPoint.Properties, word, annotation));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        SetDefaultCursor();
        HideAnnotation();

        var pointerPoint = e.GetCurrentPoint(this);
        PageInteractiveLayerPointerExited?.Invoke(this, new PageInteractiveLayerPointerExitedEventArgs(PageNumber!.Value, pointerPoint.Position, pointerPoint.Properties));
    }

    public void ClearSelection()
    {
        Debug.ThrowNotOnUiThread();

        if (PageNumber.HasValue)
        {
            PageTextSelectionChanged?.Invoke(this, new PageTextSelectionResetEventArgs(PageNumber.Value));
        }
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

    public void HideAnnotation()
    {
        if (FlyoutBase.GetAttachedFlyout(this) is not Flyout attachedFlyout)
        {
            return;
        }

        attachedFlyout.Hide();
        attachedFlyout.Content = null;
    }

    /// <summary>
    /// Handle mouse hover over words, links or others
    /// </summary>
    public void HandleMouseMoveOver(Point loc)
    {
        PdfAnnotation? annotation = PdfTextLayer!.FindAnnotationOver(loc.X, loc.Y);

        if (annotation is not null)
        {
            if (!string.IsNullOrEmpty(annotation.Content))
            {
                ShowAnnotation(annotation);
            }

            if (annotation.IsInteractive)
            {
                SetHandCursor();
                return;
            }
        }
        else
        {
            HideAnnotation();
        }

        PdfWord? word = PdfTextLayer!.FindWordOver(loc.X, loc.Y);
        if (word is not null)
        {
            if (PdfTextLayer.GetLine(word)?.IsInteractive == true)
            {
                SetHandCursor();
            }
            else
            {
                SetIbeamCursor();
            }
        }
        else
        {
            SetDefaultCursor();
        }
    }

    public void HandleMultipleClick(PointerPressedEventArgs e, PdfWord word)
    {
        if (PdfTextLayer is null)
        {
            return;
        }

        PdfWord? startWord;
        PdfWord? endWord;

        if (e.ClickCount == 2)
        {
            // Select whole word
            startWord = word;
            endWord = word;
        }
        else if (e.ClickCount == 3)
        {
            // Select whole line
            var block = PdfTextLayer.TextBlocks![word.TextBlockIndex];
            var line = block.TextLines![word.TextLineIndex - block.TextLines[0].IndexInPage];

            startWord = line.Words![0];
            endWord = line.Words[^1];
        }
        else if (e.ClickCount == 4)
        {
            // Select whole paragraph
            var block = PdfTextLayer.TextBlocks![word.TextBlockIndex];

            startWord = block.TextLines![0].Words![0];
            endWord = block.TextLines![^1].Words![^1];
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"HandleMultipleClick: Not handled, got {e.ClickCount} click(s).");
            return;
        }

        ClearSelection();

        PageTextSelectionChanged?.Invoke(this, new PageTextSelectionChangedEventArgs(PageNumber!.Value, startWord, endWord, true));
        
        System.Diagnostics.Debug.WriteLine($"HandleMultipleClick: {startWord} -> {endWord}.");
    }

    internal bool TrySwitchCapture(PointerEventArgs e)
    {
        PageItem? endPage = _documentControl?.GetPageItemOver(e);
        if (endPage is null)
        {
            // Cursor is not over any page, do nothing
            return false;
        }

        PageInteractiveLayerControl endTextLayer = endPage.InteractiveLayer ??
                                                   throw new NullReferenceException($"{typeof(PageInteractiveLayerControl)} not found.");

        e.Pointer.Capture(endTextLayer); // Switch capture to new page
        return true;
    }
}