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
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using SkiaSharp;

namespace Caly.Core.Controls
{
    [TemplatePart("PART_PdfPageTextLayerControl", typeof(PdfPageTextLayerControl))]
    public sealed class PdfPageItem : ContentControl
    {
        /// <summary>
        /// Defines the <see cref="IsPageRendering"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsPageRenderingProperty = AvaloniaProperty.Register<PdfPageItem, bool>(nameof(IsPageRendering));

        /// <summary>
        /// Defines the <see cref="Picture"/> property.
        /// </summary>
        public static readonly StyledProperty<IRef<SKPicture>?> PictureProperty = AvaloniaProperty.Register<PdfPageItem, IRef<SKPicture>?>(nameof(Picture), defaultBindingMode: BindingMode.OneWay);

        /// <summary>
        /// Defines the <see cref="IsPageVisible"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsPageVisibleProperty = AvaloniaProperty.Register<PdfPageItem, bool>(nameof(IsPageVisible), false);

        /// <summary>
        /// Defines the <see cref="VisibleArea"/> property.
        /// </summary>
        public static readonly StyledProperty<Rect?> VisibleAreaProperty = AvaloniaProperty.Register<PdfPageItem, Rect?>(nameof(VisibleArea), null, defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Defines the <see cref="Exception"/> property.
        /// </summary>
        public static readonly StyledProperty<ExceptionViewModel?> ExceptionProperty = AvaloniaProperty.Register<PdfPageItem, ExceptionViewModel?>(nameof(Exception), defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Defines the <see cref="PdfPageTextLayerControl"/> property.
        /// </summary>
        public static readonly DirectProperty<PdfPageItem, PdfPageTextLayerControl?> TextLayerProperty =
            AvaloniaProperty.RegisterDirect<PdfPageItem, PdfPageTextLayerControl?>(nameof(LayoutTransformControl), o => o.TextLayer);
        
        static PdfPageItem()
        {
            AffectsRender<PdfPageItem>(PictureProperty, IsPageVisibleProperty);
        }

        public bool IsPageRendering
        {
            get => GetValue(IsPageRenderingProperty);
            set => SetValue(IsPageRenderingProperty, value);
        }

        public IRef<SKPicture>? Picture
        {
            get => GetValue(PictureProperty);
            set => SetValue(PictureProperty, value);
        }

        public bool IsPageVisible
        {
            get => GetValue(IsPageVisibleProperty);
            set => SetValue(IsPageVisibleProperty, value);
        }

        public Rect? VisibleArea
        {
            get => GetValue(VisibleAreaProperty);
            set => SetValue(VisibleAreaProperty, value);
        }

        public ExceptionViewModel? Exception
        {
            get => GetValue(ExceptionProperty);
            set => SetValue(ExceptionProperty, value);
        }

        private PdfPageTextLayerControl? _textLayer;

        /// <summary>
        /// Gets the text layer.
        /// </summary>
        public PdfPageTextLayerControl? TextLayer
        {
            get => _textLayer;
            private set => SetAndRaise(TextLayerProperty, ref _textLayer, value);
        }

        public PdfPageItem()
        {
#if DEBUG
            if (Design.IsDesignMode)
            {
                // Only if in design mode
                DataContext = new PdfPageViewModel();
            }
#endif
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            TextLayer = e.NameScope.FindFromNameScope<PdfPageTextLayerControl>("PART_PdfPageTextLayerControl");
        }

        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromLogicalTree(e);
            Picture?.Dispose();
        }
    }
}
