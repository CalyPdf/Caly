﻿// Copyright (C) 2024 BobLd
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY - without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Caly.Core.Handlers.Interfaces;
using Caly.Pdf.Models;

namespace Caly.Core.Controls
{
    public sealed class PdfPageTextLayerControl : Control
    {
        // https://github.com/AvaloniaUI/Avalonia/blob/master/src/Avalonia.Controls/Primitives/TextSelectionCanvas.cs#L62
        // Check caret handle

        private static readonly Cursor IbeamCursor = new(StandardCursorType.Ibeam);
        private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

        private CompositeDisposable? _pointerDisposables;

        public static readonly StyledProperty<PdfTextLayer?> PdfTextLayerProperty =
            AvaloniaProperty.Register<PdfPageTextLayerControl, PdfTextLayer?>(nameof(PdfTextLayer));

        public static readonly StyledProperty<int?> PageNumberProperty =
            AvaloniaProperty.Register<PdfPageTextLayerControl, int?>(nameof(PageNumber));

        public static readonly StyledProperty<ITextSelectionHandler?> TextSelectionHandlerProperty =
            AvaloniaProperty.Register<PdfPageTextLayerControl, ITextSelectionHandler?>(nameof(TextSelectionHandler));

        public static readonly StyledProperty<bool> SelectionChangedFlagProperty =
            AvaloniaProperty.Register<PdfPageTextLayerControl, bool>(nameof(SelectionChangedFlag));

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

        static PdfPageTextLayerControl()
        {
            AffectsRender<PdfPageTextLayerControl>(PdfTextLayerProperty, SelectionChangedFlagProperty);
        }

        internal void SetIbeamCursor()
        {
            if (Cursor == IbeamCursor)
            {
                return;
            }

            Cursor = IbeamCursor;
        }

        internal void SetHandCursor()
        {
            if (Cursor == HandCursor)
            {
                return;
            }

            Cursor = HandCursor;
        }

        internal void SetDefaultCursor()
        {
            if (Cursor == Cursor.Default)
            {
                return;
            }

            Cursor = Cursor.Default;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (Bounds.Width <= 0 || Bounds.Height <= 0)
            {
                return;
            }

            // We need to fill to get Pointer events
            context.FillRectangle(Brushes.Transparent, Bounds);

            TextSelectionHandler?.RenderPage(this, context);
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
                    var pointerMovedDisposable = this.GetObservable(PointerMovedEvent)
                        .Subscribe(TextSelectionHandler!.OnPointerMoved);

                    var pointerWheelChangedDisposable = this.GetObservable(PointerWheelChangedEvent, handledEventsToo: true)
                        .Subscribe(TextSelectionHandler!.OnPointerMoved);

                    var pointerPressedDisposable = this.GetObservable(PointerPressedEvent)
                        .Subscribe(TextSelectionHandler.OnPointerPressed);

                    var pointerReleasedDisposable = this.GetObservable(PointerReleasedEvent)
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
