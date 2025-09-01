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
            private readonly bool _isDarkMode;

            private readonly object _lock = new object();

            public SkiaDrawOperation(Rect bounds, SKRect visibleArea, IRef<SKPicture>? picture, SKFilterQuality filterQuality, bool isDarkMode, SKBitmap imageMask)
            {
                _picture = picture;
                _visibleArea = visibleArea;
                _filterQuality = filterQuality;
                _isDarkMode = isDarkMode;
                _imageMask = imageMask;
                Bounds = bounds;
            }

            public void Dispose()
            {
                lock (_lock)
                {
                    _picture?.Dispose();
                }
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

                lock (_lock)
                {
                    if (_picture?.Item is null || _picture.Item.Handle == IntPtr.Zero ||
                        !context.TryGetFeature(out ISkiaSharpApiLeaseFeature leaseFeature))
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


                        if (_isDarkMode)
                        {
                            using (var invertPaint = new SKPaint())
                            {
                                invertPaint.FilterQuality = _filterQuality;
                                invertPaint.IsDither = false;
                                invertPaint.FakeBoldText = false;
                                invertPaint.IsAntialias = false;

                                // Invert lightness across whole page 
                                SKHighContrastConfig config = new()
                                {
                                    Grayscale = false,
                                    InvertStyle = SKHighContrastConfigInvertStyle.InvertLightness,
                                    Contrast = 0.0f
                                };
                                
                                invertPaint.ColorFilter = SKColorFilter.CreateHighContrast(config);

                                canvas.DrawPicture(_picture.Item, invertPaint);
                            }

                            // Image mask is used for drawing unprocessed images - pictures in the PDF that should not be inverted
                            if (_imageMask != null)
                            {
                                
                                using (var imagePaint = new SKPaint())
                                {
                                    imagePaint.FilterQuality = _filterQuality;
                                    imagePaint.IsDither = false;
                                    imagePaint.FakeBoldText = false;
                                    imagePaint.IsAntialias = false;

                                    
                                    canvas.Save();
                                    using (var path = new SKPath())
                                    {
                                        // This approach is not optimal, but it supports any shape of images, not only rectangles
                                        for (int y = 0; y < _imageMask.Height; y++)
                                        {
                                            for (int x = 0; x < _imageMask.Width; x++)
                                            {
                                                if (_imageMask.GetPixel(x, y).Red > 127)
                                                {
                                                    path.AddRect(new SKRect(x, y, x + 1, y + 1));
                                                }
                                            }
                                        }

                                        canvas.ClipPath(path);
                                        canvas.DrawPicture(_picture.Item, imagePaint);
                                    }
                                    canvas.Restore();
                                }
                            }
                            
                        }
                        // Original rendering (no dark mode)
                        else
                        {
                            
                            using (var p = new SKPaint())
                            {
                                p.FilterQuality = _filterQuality;
                                p.IsDither = false;
                                p.FakeBoldText = false;
                                p.IsAntialias = false;

                                canvas.DrawPicture(_picture.Item, p);



#if DEBUG
                                using (var skFont = SKTypeface.Default.ToFont(_picture.Item.CullRect.Height / 4f, 1f))
                                using (var fontPaint = new SKPaint(skFont))
                                {
                                    fontPaint.Style = SKPaintStyle.Fill;
                                    fontPaint.Color = SKColors.Blue.WithAlpha(100);
                                    canvas.DrawText(_picture.Item.UniqueId.ToString(), _picture.Item.CullRect.Width / 4f, _picture.Item.CullRect.Height / 2f, fontPaint);
                                }
#endif
                            }
                        }
                        canvas.Restore();
                    }
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


        public static readonly StyledProperty<bool> IsDarkModeProperty =
        AvaloniaProperty.Register<SkiaPdfPageControl, bool>(nameof(IsDarkMode));

        public bool IsDarkMode
        {
            get => GetValue(IsDarkModeProperty);
            set => SetValue(IsDarkModeProperty, value);
        }

        public static readonly StyledProperty<SKBitmap> ImageMaskProperty =
    AvaloniaProperty.Register<SkiaPdfPageControl, SKBitmap>(nameof(ImageMask));


        public SKBitmap ImageMask
        {
            get => GetValue(ImageMaskProperty);
            set => SetValue(ImageMaskProperty, value);
        }
        static SkiaPdfPageControl()
        {
            ClipToBoundsProperty.OverrideDefaultValue<SkiaPdfPageControl>(true);

            AffectsRender<SkiaPdfPageControl>(PictureProperty, VisibleAreaProperty);
            AffectsMeasure<SkiaPdfPageControl>(PictureProperty, VisibleAreaProperty);
            IsDarkModeProperty.Changed.AddClassHandler<SkiaPdfPageControl>((x, e) => x.OnDarkModeChanged(e));
        }

        private void OnDarkModeChanged(AvaloniaPropertyChangedEventArgs e)
        {
            InvalidateVisual();
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

            var picture = Picture?.Clone();
            if (picture?.Item is null || picture.Item.CullRect.IsEmpty)
            {
                base.Render(context);
                return;
            }

            SKRect tile = VisibleArea.Value.ToSKRect();

            var filter = RenderOptions.GetBitmapInterpolationMode(this);

            context.Custom(new SkiaDrawOperation(viewPort, tile, picture, filter.ToSKFilterQuality(), IsDarkMode, ImageMask));
        }
    }
}
