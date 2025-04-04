﻿// Copyright (c) 2025 BobLd
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
            private readonly SKPaint? _paint;

            private readonly SKRect _visibleArea;

            public SkiaDrawOperation(Rect bounds, SKRect visibleArea, SKPaint? paint)
            {
                _paint = paint;
                _visibleArea = visibleArea;
                Bounds = bounds;
            }

            public void Dispose()
            {
                _paint?.Shader?.Dispose();
                _paint?.Dispose();
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

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                if (_paint is null || !context.TryGetFeature(out ISkiaSharpApiLeaseFeature leaseFeature))
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
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
                    canvas.DrawPaint(_paint);
                    canvas.Restore();
                }
            }
        }

        private const SKShaderTileMode TileMode = SKShaderTileMode.Clamp;

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

            SKMatrix translate = SKMatrix.CreateTranslation((float)VisibleArea.Value.Left, (float)VisibleArea.Value.Top);
            SKRect tile = VisibleArea.Value.ToSKRect();

            SKPaint paint = new SKPaint() { Shader = picture.Item.ToShader(TileMode, TileMode, translate, tile) };
            context.Custom(new SkiaDrawOperation(viewPort, tile, paint));

            base.Render(context);
        }
    }
}
