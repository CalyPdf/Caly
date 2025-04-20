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
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Caly.Core.Utilities;
using SkiaSharp;

namespace Caly.Core.Controls
{
    /// <summary>
    /// Skia Pdf page control.
    /// </summary>
    public sealed class SkiaPdfPageControl : Control
    {
        private sealed class SkiaDrawOperation : ICustomDrawOperation
        {
            private readonly IRef<SKPicture>? _picture;
            private readonly SKFilterQuality _filterQuality;
            private readonly SKRect _visibleArea;

            public SkiaDrawOperation(Rect bounds, SKRect visibleArea, IRef<SKPicture>? picture, SKFilterQuality filterQuality)
            {
                _picture = picture?.Clone();
                _visibleArea = visibleArea;
                _filterQuality = filterQuality;
                Bounds = bounds;
            }

            public void Dispose()
            {
                _picture?.Dispose();
            }

            public Rect Bounds { get; }

            public bool HitTest(Point p) => Bounds.Contains(p);

            public bool Equals(ICustomDrawOperation? other) => false;

            /// <summary>
            /// This operation is executed on Render thread.
            /// </summary>
            public void Render(ImmediateDrawingContext context)
            {
                Debug.ThrowOnUiThread();

                if (_picture?.Item is null || !context.TryGetFeature(out ISkiaSharpApiLeaseFeature leaseFeature))
                {
                    return;
                }

                using (ISkiaSharpApiLease lease = leaseFeature.Lease())
                {
                    var canvas = lease?.SkCanvas;
                    if (canvas is null)
                    {
                        return;
                    }

                    canvas.Save();
                    canvas.ClipRect(_visibleArea);
                    using (var p = new SKPaint())
                    {
                        p.FilterQuality = _filterQuality;
                        p.IsDither = false;
                        p.FakeBoldText = false;
                        p.IsAntialias = false;

                        canvas.DrawPicture(_picture.Item, p);
                    }
                    canvas.Restore();
                }
            }
        }

        /// <summary>
        /// Defines the <see cref="Picture"/> property.
        /// </summary>
        public static readonly StyledProperty<IRef<SKPicture>?> PictureProperty =
            AvaloniaProperty.Register<SkiaPdfPageControl, IRef<SKPicture>?>(nameof(Picture));

        /// <summary>
        /// Defines the <see cref="VisibleArea"/> property.
        /// </summary>
        public static readonly StyledProperty<Rect?> VisibleAreaProperty =
            AvaloniaProperty.Register<SkiaPdfPageControl, Rect?>(nameof(VisibleArea));

        /// <summary>
        /// Gets or sets the <see cref="SKPicture"/> picture.
        /// </summary>
        [Content]
        public IRef<SKPicture>? Picture
        {
            get => GetValue(PictureProperty);
            set => SetValue(PictureProperty, value);
        }

        public Rect? VisibleArea
        {
            get => GetValue(VisibleAreaProperty);
            set => SetValue(VisibleAreaProperty, value);
        }

        static SkiaPdfPageControl()
        {
            ClipToBoundsProperty.OverrideDefaultValue<SkiaPdfPageControl>(true);

            AffectsRender<SkiaPdfPageControl>(PictureProperty, VisibleAreaProperty);
            AffectsMeasure<SkiaPdfPageControl>(PictureProperty, VisibleAreaProperty);
        }

        /// <summary>
        /// This operation is executed on UI thread.
        /// </summary>
        public override void Render(DrawingContext context)
        {
            Debug.ThrowNotOnUiThread();

            var viewPort = new Rect(Bounds.Size);

            if (viewPort.IsEmpty())
            {
                base.Render(context);
                return;
            }

            if (!VisibleArea.HasValue || VisibleArea.Value.IsEmpty())
            {
                base.Render(context);
                return;
            }

            var picture = Picture;
            if (picture?.Item is null || picture.Item.CullRect.IsEmpty)
            {
                base.Render(context);
                return;
            }

            SKRect tile = VisibleArea.Value.ToSKRect();

            var filter = RenderOptions.GetBitmapInterpolationMode(this);

            /*
            context.PushRenderOptions(new RenderOptions()
            {
                BitmapInterpolationMode = filter
            });
            */

            context.Custom(new SkiaDrawOperation(viewPort, tile, picture, filter.ToSKFilterQuality()));

            base.Render(context);
        }
    }
}
