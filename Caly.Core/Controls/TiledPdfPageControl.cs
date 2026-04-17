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
        private static readonly SKPaint RenderPaint = new()
        {
            IsAntialias = false,
            IsDither = true
        };

        // Bilinear filtering for smooth tile scaling when the zoom ratio
        // is not exactly 1:1 with the tile level resolution.
        private static readonly SKSamplingOptions RenderSamplingOptions = new(SKFilterMode.Linear, SKMipmapMode.Nearest);

        private readonly List<TileDrawEntry> _tiles;

        public TiledDrawOperation(Rect bounds, List<TileDrawEntry> tiles)
        {
            Bounds = bounds;
            _tiles = tiles;
        }

        public Rect Bounds { get; }

        public bool HitTest(Point p) => Bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);

        public override bool Equals(object? obj) => obj is ICustomDrawOperation cdo && Equals(cdo);

        public override int GetHashCode() => HashCode.Combine(Bounds, _tiles.Count);

        /// <summary>
        /// Executed on the render thread. Blits pre-rendered tile bitmaps.
        /// </summary>
        public void Render(ImmediateDrawingContext context)
        {
            Debug.ThrowOnUiThread();

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

#if DEBUG
                using var backgroundPaint = new SKPaint();
                backgroundPaint.Style = SKPaintStyle.Fill;
                backgroundPaint.Color = SKColors.Aqua;
                canvas.DrawPaint(backgroundPaint);

                using var borderPaint = new SKPaint();
                borderPaint.Style = SKPaintStyle.Stroke;
                borderPaint.Color = SKColors.Red.WithAlpha(120);
                borderPaint.StrokeWidth = 1f;
#endif

                foreach (var tile in _tiles)
                {
                    if (tile.BitmapRef.IsAlive)
                    {
                        var bmp = tile.BitmapRef.Item;
                        if (bmp.DrawsNothing)
                        {
                            continue;
                        }

                        using var image = SKImage.FromBitmap(bmp);
                        canvas.DrawImage(image, tile.SrcRect, tile.DestRect, RenderSamplingOptions, RenderPaint);
                    }

#if DEBUG
                    canvas.DrawRect(tile.DestRect, borderPaint);
#endif
                }

                canvas.Restore();
            }
        }

        public void Dispose()
        {
            foreach (var tile in _tiles)
            {
                tile.Dispose();
            }

            _tiles.Clear();
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
    /// The tile level used on the previous render pass, used to detect zoom-level changes
    /// and trigger deferred eviction of stale tile levels from the cache.
    /// Only accessed on the UI thread in <see cref="Render"/>.
    /// </summary>
    private int _lastTileLevel = -1;

    /// <summary>
    /// When true, stale tile levels should be evicted once all visible tiles at the
    /// current level are cached. This defers eviction so that old-level tiles remain
    /// available as fallbacks while new-level tiles are being rendered.
    /// Only accessed on the UI thread in <see cref="Render"/>.
    /// </summary>
    private bool _staleLevelEvictionPending;

    /// <summary>
    /// Number of extra tile rows/columns rendered beyond the visible area, so
    /// short scrolls don't immediately reveal unrendered regions.
    /// </summary>
    private const int RenderTileMargin = 2;

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
                // Service just became available — request tiles if VisibleArea is already set.
                PrefetchVisibleTiles();
            }
        }
        else if (change.Property == VisibleAreaProperty || change.Property == ZoomLevelProperty
                 || change.Property == PictureProperty)
        {
            // Prefetch tiles for the new visible area. AffectsRender handles the
            // redraw side — we only need to queue missing tiles here.
            PrefetchVisibleTiles();
        }
    }

    /// <summary>
    /// Queues any uncached tiles inside the visible area + margin for background rendering.
    /// </summary>
    private void PrefetchVisibleTiles()
    {
        if (!VisibleArea.HasValue || VisibleArea.Value.IsEmpty())
        {
            return;
        }

        var service = TileRenderService;
        var pageDisplaySize = PageDisplaySize;
        if (service is null || pageDisplaySize.Width <= 0 || pageDisplaySize.Height <= 0)
        {
            return;
        }

        var picture = Picture;
        if (picture?.IsAlive != true)
        {
            return;
        }

        int tileLevel = TileGrid.ComputeTileLevel(ZoomLevel);
        int pageNumber = PageNumber;

        GetTileRange(VisibleArea.Value, in pageDisplaySize, tileLevel, RenderTileMargin,
            out int startCol, out int startRow, out int endCol, out int endRow);

        List<TileCoord>? missingTiles = null;

        for (int r = startRow; r <= endRow; r++)
        {
            for (int c = startCol; c <= endCol; c++)
            {
                var key = new TileKey(pageNumber, tileLevel, c, r);
                if (!service.Cache.Contains(key))
                {
                    (missingTiles ??= []).Add(new TileCoord(c, r));
                }
            }
        }

        if (missingTiles is not null && missingTiles.Count > 0)
        {
            service.RequestTiles(pageNumber, picture, tileLevel, missingTiles, PpiScale, pageDisplaySize);
        }
    }

    /// <summary>
    /// Computes the tile column/row range for the given visible area, expanded by a margin
    /// and clamped to the grid dimensions.
    /// </summary>
    private static void GetTileRange(in Rect visibleArea, in Size pageDisplaySize, int tileLevel, int margin,
        out int startCol, out int startRow, out int endCol, out int endRow)
    {
        var gridDims = TileGrid.GetGridDimensions(pageDisplaySize, tileLevel);
        double tileDisplaySize = TileGrid.TilePixelSize / TileGrid.GetTileLevelScale(tileLevel);

        startCol = Math.Max(0, (int)(visibleArea.Left / tileDisplaySize) - margin);
        startRow = Math.Max(0, (int)(visibleArea.Top / tileDisplaySize) - margin);
        endCol = Math.Min(gridDims.Width - 1, (int)Math.Ceiling(visibleArea.Right / tileDisplaySize) - 1 + margin);
        endRow = Math.Min(gridDims.Height - 1, (int)Math.Ceiling(visibleArea.Bottom / tileDisplaySize) - 1 + margin);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (TileRenderService is not null)
        {
            TileRenderService.TileReady -= OnTileReady;
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
    private static TileDrawEntry? TryGetFallbackTile(TileCache cache, int pageNumber, int tileLevel, int col, int row, Size pageDisplaySize)
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

            return new TileDrawEntry(fallbackRef, srcRect, displayRect.ToSKRect());
        }

        return null;
    }

    /// <summary>
    /// Searches cached higher tile levels (finer resolution) for tiles that overlap the
    /// display area of a missing tile. This handles zoom-out: the previously rendered
    /// higher-resolution tiles are drawn at their original display positions, covering
    /// parts of the missing coarser tile until it is rendered.
    /// </summary>
    /// <param name="higherCachedLevels">The set of tile levels above <paramref name="tileLevel"/>
    /// that have any cached tiles for this page, sorted ascending (closest level first).
    /// Queried once per render to skip empty levels without iterating empty regions.</param>
    private static void AddHigherLevelFallbackTiles(TileCache cache, int pageNumber, int tileLevel, int col, int row,
        Size pageDisplaySize, List<TileDrawEntry> entries, IReadOnlyCollection<int>? higherCachedLevels)
    {
        if (higherCachedLevels is null)
        {
            return;
        }

        foreach (var fl in higherCachedLevels)
        {
            int levelDiff = fl - tileLevel;
            int multiplier = 1 << levelDiff;

            // At finer level fl, the area of tile (col, row) at tileLevel is covered
            // by a multiplier x multiplier block of tiles starting at (col * multiplier, row * multiplier).
            int startCol = col * multiplier;
            int startRow = row * multiplier;
            int endCol = startCol + multiplier;
            int endRow = startRow + multiplier;

            // Clamp to grid bounds at the finer level
            var dims = TileGrid.GetGridDimensions(pageDisplaySize, fl);
            endCol = Math.Min(endCol, dims.Width);
            endRow = Math.Min(endRow, dims.Height);

            bool foundAny = false;
            for (int r = startRow; r < endRow; r++)
            {
                for (int c = startCol; c < endCol; c++)
                {
                    var finerKey = new TileKey(pageNumber, fl, c, r);
                    if (cache.TryGet(finerKey, out var bitmapRef) && bitmapRef is not null)
                    {
                        // Each finer tile maps to its own (smaller) display rect —
                        // draw the full bitmap at that position.
                        var displayRect = TileGrid.GetTileDisplayRect(c, r, fl, pageDisplaySize);
                        var destRect = new SKRect(
                            (float)displayRect.X, (float)displayRect.Y,
                            (float)displayRect.Right, (float)displayRect.Bottom);
                        var srcRect = new SKRect(0, 0, bitmapRef.Item.Width, bitmapRef.Item.Height);
                        entries.Add(new TileDrawEntry(bitmapRef, srcRect, destRect));
                        foundAny = true;
                    }
                }
            }

            // Use the closest higher level that has any cached tiles
            if (foundAny)
            {
                return;
            }
        }
    }

    /// <summary>
    /// This operation is executed on the UI thread.
    /// Draws cached tiles for visible area + margin. Tile requesting is handled
    /// separately in <see cref="PrefetchVisibleTiles"/>.
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
            return;
        }

        int tileLevel = TileGrid.ComputeTileLevel(ZoomLevel);
        int pageNumber = PageNumber;

        // Detect tile level changes and mark stale levels for deferred eviction.
        // We do NOT evict immediately — old-level tiles serve as fallbacks while
        // new-level tiles are being rendered in the background.
        if (_lastTileLevel != -1 && _lastTileLevel != tileLevel)
        {
            _staleLevelEvictionPending = true;
        }

        _lastTileLevel = tileLevel;

        // Draw visible tiles + margin. The margin ensures tiles just outside the
        // viewport are pre-drawn, so the compositor can handle short scrolls without
        // triggering a new render pass. At high zoom levels this keeps per-frame work
        // bounded by viewport size rather than growing with the full tile grid.
        GetTileRange(VisibleArea.Value, in pageDisplaySize, tileLevel, RenderTileMargin,
            out int startCol, out int startRow, out int endCol, out int endRow);

        int tileCount = (endCol - startCol + 1) * (endRow - startRow + 1);
        var tileEntries = new List<TileDrawEntry>(tileCount);
        bool allVisibleTilesCached = true;

        // Query cached higher levels once per render. Passed into the finer-level fallback
        // search so it iterates only levels that actually have tiles — this avoids scanning
        // large empty tile grids at finer levels (4x per level) while still finding fallbacks
        // when the user zooms out past many levels (otherwise tiles from deeply zoomed-in
        // views would be skipped, leaving the page blank until exact-level tiles render).
        IReadOnlyCollection<int>? higherCachedLevels = service.Cache.GetCachedLevelsAbove(pageNumber, tileLevel);

        for (int r = startRow; r <= endRow; r++)
        {
            for (int c = startCol; c <= endCol; c++)
            {
                var key = new TileKey(pageNumber, tileLevel, c, r);

                // Use the promoting TryGet so actively-visible exact-level tiles stay at the
                // front of the LRU list. Without this, on-screen tiles age while fallback
                // lookups below (TryGet) keep promoting coarser/finer tiles — inverting
                // priority and letting LRU pressure evict the tiles the user is currently
                // looking at, which then forces upscaled blurry fallbacks on the next render.
                if (service.Cache.TryGet(key, out var bitmapRef) && bitmapRef is not null)
                {
                    // Exact-level tile available — use full bitmap as source
                    var displayRect = TileGrid.GetTileDisplayRect(c, r, tileLevel, pageDisplaySize);
                    var destRect = new SKRect((float)displayRect.X, (float)displayRect.Y,
                        (float)displayRect.Right, (float)displayRect.Bottom);

                    var srcRect = new SKRect(0, 0, bitmapRef.Item.Width, bitmapRef.Item.Height);
                    tileEntries.Add(new TileDrawEntry(bitmapRef, srcRect, destRect));
                }
                else
                {
                    allVisibleTilesCached = false;

                    // Try coarser (lower-level) fallback first — single upscaled tile
                    var fallbackEntry = TryGetFallbackTile(service.Cache, pageNumber, tileLevel, c, r, pageDisplaySize);
                    if (fallbackEntry.HasValue)
                    {
                        tileEntries.Add(fallbackEntry.Value);
                    }
                    else
                    {
                        // Try finer (higher-level) fallback — multiple cached tiles may cover this area.
                        // This handles zoom-out: old higher-resolution tiles fill the gap until
                        // coarser tiles are rendered.
                        AddHigherLevelFallbackTiles(service.Cache, pageNumber, tileLevel, c, r, pageDisplaySize, tileEntries, higherCachedLevels);
                    }
                }
            }
        }

        // Evict stale tile levels only after all visible+margin tiles at the current level
        // are cached, so old tiles remain available as fallbacks during the transition.
        if (allVisibleTilesCached && _staleLevelEvictionPending)
        {
            service.EvictStaleLevels(pageNumber, tileLevel);
            _staleLevelEvictionPending = false;
        }

        context.Custom(new TiledDrawOperation(viewPort, tileEntries));
    }
}
