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
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Utilities;
using Caly.Pdf.Models;

namespace Caly.Core.Controls
{
    public sealed class PdfPageTextLayerControl : Control
    {
        // https://github.com/AvaloniaUI/Avalonia/blob/master/src/Avalonia.Controls/Primitives/TextSelectionCanvas.cs#L62
        // Check caret handle

        private CompositeDisposable? _pointerDisposables;

        public static readonly StyledProperty<PdfTextLayer?> PdfTextLayerProperty =
            AvaloniaProperty.Register<PdfPageTextLayerControl, PdfTextLayer?>(nameof(PdfTextLayer));

        public static readonly StyledProperty<int?> PageNumberProperty =
            AvaloniaProperty.Register<PdfPageTextLayerControl, int?>(nameof(PageNumber));

        public static readonly StyledProperty<ITextSelectionHandler?> TextSelectionHandlerProperty =
            AvaloniaProperty.Register<PdfPageTextLayerControl, ITextSelectionHandler?>(nameof(TextSelectionHandler));

        public static readonly StyledProperty<bool> SelectionChangedFlagProperty =
            AvaloniaProperty.Register<PdfPageTextLayerControl, bool>(nameof(SelectionChangedFlag));

        /// <summary>
        /// Defines the <see cref="VisibleArea"/> property.
        /// </summary>
        public static readonly StyledProperty<Rect?> VisibleAreaProperty =
            AvaloniaProperty.Register<SkiaPdfPageControl, Rect?>(nameof(VisibleArea));
        
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

        public ITextSelectionHandler? TextSelectionHandler
        {
            get => GetValue(TextSelectionHandlerProperty);
            set => SetValue(TextSelectionHandlerProperty, value);
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

        static PdfPageTextLayerControl()
        {
            AffectsRender<PdfPageTextLayerControl>(PdfTextLayerProperty, SelectionChangedFlagProperty, VisibleAreaProperty);
        }

        internal void SetIbeamCursor()
        {
            Debug.ThrowNotOnUiThread();

            var itemsControl = this.FindAncestorOfType<PdfPageItemsControl>();
            if (itemsControl is not null && itemsControl.Cursor != App.IbeamCursor)
            {
                itemsControl.Cursor = App.IbeamCursor;
            }
        }

        internal void SetHandCursor()
        {
            Debug.ThrowNotOnUiThread();

            var itemsControl = this.FindAncestorOfType<PdfPageItemsControl>();
            if (itemsControl is not null && itemsControl.Cursor != App.HandCursor)
            {
                itemsControl.Cursor = App.HandCursor;
            }
        }
        
        internal void SetDefaultCursor()
        {
            Debug.ThrowNotOnUiThread();

            var itemsControl = this.FindAncestorOfType<PdfPageItemsControl>();
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

            TextSelectionHandler?.RenderPage(this, context, VisibleArea.Value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == TextSelectionHandlerProperty)
            {
                // If the textSelectionHandler was already attached, we unsubscribe
                _pointerDisposables?.Dispose();
                
                if (TextSelectionHandler is not null)
                {
                    var pointerWheelChangedDisposable = this.GetObservable(PointerWheelChangedEvent, handledEventsToo: true)
                        .Subscribe(TextSelectionHandler!.OnPointerMoved);

                    var pointerMovedDisposable = this.GetObservable(PointerMovedEvent, handledEventsToo: false)
                        .Subscribe(TextSelectionHandler!.OnPointerMoved);

                    var pointerPressedDisposable = this.GetObservable(PointerPressedEvent, handledEventsToo: false)
                        .Subscribe(TextSelectionHandler.OnPointerPressed);

                    var pointerReleasedDisposable = this.GetObservable(PointerReleasedEvent, handledEventsToo: false)
                        .Subscribe(TextSelectionHandler.OnPointerReleased);

                    _pointerDisposables = new CompositeDisposable(
                        pointerMovedDisposable,
                        pointerWheelChangedDisposable,
                        pointerPressedDisposable,
                        pointerReleasedDisposable);
                }
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _pointerDisposables?.Dispose();
        }
    }
}
