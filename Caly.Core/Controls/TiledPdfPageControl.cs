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
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Caly.Core.Rendering;
using Caly.Core.Utilities;
using SkiaSharp;

namespace Caly.Core.Controls;

/// <summary>
/// Renders PDF pages using pre-rendered bitmap tiles for efficient zooming and scrolling.
/// Falls back to direct SKPicture rendering for areas where tiles are not yet available.
/// </summary>
public sealed class TiledPdfPageControl : Control
{
    /// <summary>
    /// A single tile entry for the draw operation, holding a cloned bitmap reference,
    /// the source rect within the bitmap, and its destination rect on the canvas.
    /// </summary>
    private readonly struct TileDrawEntry : IDisposable
    {
        public IRef<SKBitmap> BitmapRef { get; }

        /// <summary>
        /// Source rectangle within the bitmap. For exact-level tiles this is the full bitmap.
        /// For lower-level fallback tiles this is a sub-region that covers the missing tile's area.
        /// </summary>
        public SKRect SrcRect { get; }

        public SKRect DestRect { get; }

        public TileDrawEntry(IRef<SKBitmap> bitmapRef, SKRect srcRect, SKRect destRect)
        {
            BitmapRef = bitmapRef;
            SrcRect = srcRect;
            DestRect = destRect;
        }

        public void Dispose() => BitmapRef.Dispose();
    }

    private sealed class TiledDrawOperation : ICustomDrawOperation
    {
        // Shared across all draw operations — these are only used on the render thread
        // which is single-threaded, so no synchronization needed.
        //
        // IsAntialias is deliberately false: with AA on, tile edges at fractional screen
        // pixel positions (after the zoom transform) get partial coverage which blends
        // with the canvas background, creating visible white seams between tiles.
        // With AA off, each screen pixel is either fully in one tile or fully in the next,
        // eliminating the bleed-through. Image content itself is still smoothly sampled
        // via s_samplingOptions below, independently of this flag.
        private static readonly SKPaint s_paint = new()
        {
            IsAntialias = false,
            IsDither = true
        };

        // Bilinear filtering for smooth tile scaling when the zoom ratio
        // is not exactly 1:1 with the tile level resolution.
        private static readonly SKSamplingOptions s_samplingOptions = new(SKFilterMode.Linear, SKMipmapMode.Nearest);

        private readonly List<TileDrawEntry> _tiles;
        private readonly IRef<SKPicture>? _fallbackPicture;
        private readonly double _ppiScale;
        private readonly Lock _lock = new();

        public TiledDrawOperation(Rect bounds, List<TileDrawEntry> tiles, IRef<SKPicture>? fallbackPicture, double ppiScale)
        {
            Bounds = bounds;
            _tiles = tiles;
            _fallbackPicture = fallbackPicture;
            _ppiScale = ppiScale;
        }

        public Rect Bounds { get; }

        public bool HitTest(Point p) => Bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other)
        {
            if (other is not TiledDrawOperation op)
            {
                return false;
            }

            if (op.Bounds != Bounds || op._tiles.Count != _tiles.Count)
            {
                return false;
            }

            for (int i = 0; i < _tiles.Count; i++)
            {
                if (_tiles[i].DestRect != op._tiles[i].DestRect)
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is ICustomDrawOperation cdo && Equals(cdo);

        public override int GetHashCode() => HashCode.Combine(Bounds, _tiles.Count);

        /// <summary>
        /// Executed on the render thread. Blits pre-rendered tile bitmaps.
        /// </summary>
        public void Render(ImmediateDrawingContext context)
        {
            Debug.ThrowOnUiThread();

            lock (_lock)
            {
                if (
#pragma warning disable CS8600
                    !context.TryGetFeature(out ISkiaSharpApiLeaseFeature leaseFeature))
#pragma warning restore CS8600
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

                    /*
                    // Always draw the fallback picture first as a base layer.
                    // This ensures areas where tiles are not yet available still
                    // show page content (vector-rendered, same as before tiling).
                    if (_fallbackPicture?.IsAlive == true && _fallbackPicture.Item.Handle != IntPtr.Zero)
                    {
                        var scale = SKMatrix.CreateScale((float)_ppiScale, (float)_ppiScale);
                        canvas.DrawPicture(_fallbackPicture.Item, in scale, _paint);
                    }
                    */

                    // Draw available tiles on top, overwriting the fallback
                    // with sharper pre-rendered bitmaps.

#if DEBUG
                    using var borderPaint = new SKPaint();
                    borderPaint.Style = SKPaintStyle.Stroke;
                    borderPaint.Color = SKColors.Red.WithAlpha(120);
                    borderPaint.StrokeWidth = 5f;
#endif

                    foreach (var tile in _tiles)
                    {
                        if (tile.BitmapRef.IsAlive)
                        {
                            using var image = SKImage.FromBitmap(tile.BitmapRef.Item);
                            canvas.DrawImage(image, tile.SrcRect, tile.DestRect,
                                s_samplingOptions, s_paint);
#if DEBUG
                            canvas.DrawRect(tile.DestRect, borderPaint);
#endif
                        }
                    }

                    canvas.Restore();
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var tile in _tiles)
                {
                    tile.Dispose();
                }

                _tiles.Clear();
                _fallbackPicture?.Dispose();
            }
        }
    }

    public static readonly StyledProperty<double> PpiScaleProperty =
        AvaloniaProperty.Register<TiledPdfPageControl, double>(nameof(PpiScale));

    public static readonly StyledProperty<double> ZoomLevelProperty =
        AvaloniaProperty.Register<TiledPdfPageControl, double>(nameof(ZoomLevel), 1.0);

    public static readonly StyledProperty<IRef<SKPicture>?> PictureProperty =
        AvaloniaProperty.Register<TiledPdfPageControl, IRef<SKPicture>?>(nameof(Picture));

    public static readonly StyledProperty<Rect?> VisibleAreaProperty =
        AvaloniaProperty.Register<TiledPdfPageControl, Rect?>(nameof(VisibleArea));

    public static readonly StyledProperty<bool> IsPageVisibleProperty =
        AvaloniaProperty.Register<TiledPdfPageControl, bool>(nameof(IsPageVisible));

    public static readonly StyledProperty<int> PageNumberProperty =
        AvaloniaProperty.Register<TiledPdfPageControl, int>(nameof(PageNumber));

    public static readonly StyledProperty<Size> PageDisplaySizeProperty =
        AvaloniaProperty.Register<TiledPdfPageControl, Size>(nameof(PageDisplaySize));

    public static readonly StyledProperty<TileRenderService?> TileRenderServiceProperty =
        AvaloniaProperty.Register<TiledPdfPageControl, TileRenderService?>(nameof(TileRenderService));

    public double PpiScale
    {
        get => GetValue(PpiScaleProperty);
        set => SetValue(PpiScaleProperty, value);
    }

    public double ZoomLevel
    {
        get => GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

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

    public bool IsPageVisible
    {
        get => GetValue(IsPageVisibleProperty);
        set => SetValue(IsPageVisibleProperty, value);
    }

    public int PageNumber
    {
        get => GetValue(PageNumberProperty);
        set => SetValue(PageNumberProperty, value);
    }

    public Size PageDisplaySize
    {
        get => GetValue(PageDisplaySizeProperty);
        set => SetValue(PageDisplaySizeProperty, value);
    }

    public TileRenderService? TileRenderService
    {
        get => GetValue(TileRenderServiceProperty);
        set => SetValue(TileRenderServiceProperty, value);
    }

    private int _invalidateScheduled;

    /// <summary>
    /// Cached page number for thread-safe access from the <see cref="OnTileReady"/> callback,
    /// which fires on a background thread and cannot read styled properties.
    /// </summary>
    private volatile int _cachedPageNumber;

    /// <summary>
    /// Reusable list for visible tile coordinates, avoiding per-frame allocation.
    /// Only used on the UI thread in <see cref="Render"/>.
    /// </summary>
    private List<(int Col, int Row)>? _visibleTilesBuffer;

    static TiledPdfPageControl()
    {
        ClipToBoundsProperty.OverrideDefaultValue<TiledPdfPageControl>(true);

        AffectsRender<TiledPdfPageControl>(PictureProperty, IsPageVisibleProperty, ZoomLevelProperty, VisibleAreaProperty);
        AffectsMeasure<TiledPdfPageControl>(PictureProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PageNumberProperty)
        {
            _cachedPageNumber = change.GetNewValue<int>();
        }
        else if (change.Property == TileRenderServiceProperty)
        {
            var oldService = change.GetOldValue<TileRenderService?>();
            var newService = change.GetNewValue<TileRenderService?>();

            if (oldService is not null)
            {
                oldService.TileReady -= OnTileReady;
            }

            if (newService is not null)
            {
                newService.TileReady += OnTileReady;
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (TileRenderService is not null)
        {
            TileRenderService.TileReady += OnTileReady;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (TileRenderService is not null)
        {
            TileRenderService.TileReady -= OnTileReady;
            TileRenderService.CancelPage(PageNumber);
        }
    }

    private void OnTileReady(TileKey key)
    {
        if (key.PageNumber != _cachedPageNumber)
        {
            return;
        }

        // Coalesce invalidation requests to avoid flooding the UI thread
        if (Interlocked.Exchange(ref _invalidateScheduled, 1) == 0)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Interlocked.Exchange(ref _invalidateScheduled, 0);
                InvalidateVisual();
            }, DispatcherPriority.Render);
        }
    }

    /// <summary>
    /// Searches cached lower tile levels for a coarser tile that covers the area of a missing tile.
    /// Returns a <see cref="TileDrawEntry"/> with the appropriate sub-region of the fallback bitmap,
    /// or null if no fallback is available.
    /// </summary>
    /// <param name="cache">The tile cache to search.</param>
    /// <param name="pageNumber">The page number.</param>
    /// <param name="tileLevel">The tile level of the missing tile.</param>
    /// <param name="col">Column of the missing tile.</param>
    /// <param name="row">Row of the missing tile.</param>
    /// <param name="pageDisplaySize">The page display size.</param>
    /// <returns>A fallback tile entry with upscaled source rect, or null.</returns>
    private static TileDrawEntry? TryGetFallbackTile(
        TileCache cache, int pageNumber, int tileLevel, int col, int row, Size pageDisplaySize)
    {
        // Search lower levels (coarser tiles) for a cached tile that covers this area.
        // At fallback level fl (where fl < tileLevel), the covering tile is at
        // (col >> d, row >> d) where d = tileLevel - fl.
        for (int fl = tileLevel - 1; fl >= 0; fl--)
        {
            int levelDiff = tileLevel - fl;
            int divisor = 1 << levelDiff;
            int fallbackCol = col >> levelDiff;
            int fallbackRow = row >> levelDiff;

            var fallbackKey = new TileKey(pageNumber, fl, fallbackCol, fallbackRow);
            if (!cache.TryGet(fallbackKey, out var fallbackRef) || fallbackRef is null)
            {
                continue;
            }

            // Compute the sub-region within the fallback bitmap that corresponds
            // to the missing tile's display area.
            //
            // The fallback bitmap is TilePixelSize x TilePixelSize (or smaller for edge tiles).
            // Each current-level tile maps to a (TilePixelSize / divisor) pixel-wide strip
            // within the fallback bitmap.
            float subPixelSize = (float)TileGrid.TilePixelSize / divisor;
            int subCol = col - (fallbackCol << levelDiff);
            int subRow = row - (fallbackRow << levelDiff);

            float srcX = subCol * subPixelSize;
            float srcY = subRow * subPixelSize;

            // Clamp to actual bitmap dimensions for edge tiles
            float srcRight = Math.Min(srcX + subPixelSize, fallbackRef.Item.Width);
            float srcBottom = Math.Min(srcY + subPixelSize, fallbackRef.Item.Height);

            if (srcRight <= srcX || srcBottom <= srcY)
            {
                fallbackRef.Dispose();
                continue;
            }

            var srcRect = new SKRect(srcX, srcY, srcRight, srcBottom);

            // The destination is the display area of the missing tile
            var displayRect = TileGrid.GetTileDisplayRect(col, row, tileLevel, pageDisplaySize);
            var destRect = new SKRect(
                (float)displayRect.X, (float)displayRect.Y,
                (float)displayRect.Right, (float)displayRect.Bottom);

            return new TileDrawEntry(fallbackRef, srcRect, destRect);
        }

        return null;
    }

    /// <summary>
    /// This operation is executed on the UI thread.
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
        if (picture?.IsAlive != true)
        {
            base.Render(context);
            return;
        }

        var service = TileRenderService;
        var pageDisplaySize = PageDisplaySize;

        if (service is null || pageDisplaySize.Width <= 0 || pageDisplaySize.Height <= 0)
        {
            // Fall back to direct picture rendering when no tile service
            var fallbackPic = picture.Clone();
            context.Custom(new TiledDrawOperation(viewPort, [], fallbackPic, PpiScale));
            return;
        }

        var visibleArea = VisibleArea.Value;
        int tileLevel = TileGrid.ComputeTileLevel(ZoomLevel);
        var visibleTiles = TileGrid.GetVisibleTiles(visibleArea, pageDisplaySize, tileLevel, _visibleTilesBuffer);
        _visibleTilesBuffer = visibleTiles; // Keep reference for next frame reuse

        var tileEntries = new List<TileDrawEntry>(visibleTiles.Count);
        List<(int Col, int Row)>? missingTiles = null;
        int pageNumber = PageNumber;

        foreach (var (col, row) in visibleTiles)
        {
            var key = new TileKey(pageNumber, tileLevel, col, row);

            if (service.Cache.TryGet(key, out var bitmapRef) && bitmapRef is not null)
            {
                // Exact-level tile available — use full bitmap as source
                var displayRect = TileGrid.GetTileDisplayRect(col, row, tileLevel, pageDisplaySize);
                var destRect = new SKRect(
                    (float)displayRect.X, (float)displayRect.Y,
                    (float)displayRect.Right, (float)displayRect.Bottom);

                var srcRect = new SKRect(0, 0, bitmapRef.Item.Width, bitmapRef.Item.Height);
                tileEntries.Add(new TileDrawEntry(bitmapRef, srcRect, destRect));
            }
            else
            {
                // Try to find a lower-level (coarser) tile as a fallback to avoid blank areas
                var fallbackEntry = TryGetFallbackTile(service.Cache, pageNumber, tileLevel, col, row, pageDisplaySize);
                if (fallbackEntry.HasValue)
                {
                    tileEntries.Add(fallbackEntry.Value);
                }

                (missingTiles ??= []).Add((col, row));
            }
        }

        // Request missing tiles in the background
        if (missingTiles is not null && missingTiles.Count > 0)
        {
            service.RequestTiles(pageNumber, picture, tileLevel, missingTiles, PpiScale, pageDisplaySize);
        }

        // Always provide the fallback picture. Even if all tiles are currently in cache,
        // they could be evicted between this Render() call and the actual draw on the render thread.
        IRef<SKPicture>? fallbackPicture = null;
        try
        {
            fallbackPicture = picture.Clone();
        }
        catch (ObjectDisposedException)
        {
            // Picture was disposed; proceed without fallback
        }

        context.Custom(new TiledDrawOperation(viewPort, tileEntries, fallbackPicture, PpiScale));
    }
}
