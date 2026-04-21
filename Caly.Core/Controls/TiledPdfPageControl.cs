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
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
    /// A single tile entry for the draw operation, holding a cloned image reference,
    /// the source rect within the image, and its destination rect on the canvas.
    /// </summary>
    private readonly struct TileDrawEntry : IDisposable
    {
        public IRef<SKImage> ImageRef { get; }

        /// <summary>
        /// Source rectangle within the image. For exact-level tiles this is the full image.
        /// For lower-level fallback tiles this is a sub-region that covers the missing tile's area.
        /// </summary>
        public SKRect SrcRect { get; }

        public SKRect DestRect { get; }

        public TileDrawEntry(IRef<SKImage> imageRef, SKRect srcRect, SKRect destRect)
        {
            ImageRef = imageRef;
            SrcRect = srcRect;
            DestRect = destRect;
        }

        public void Dispose() => ImageRef.Dispose();
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

        private TileDrawEntry[]? _tiles;
        private readonly int _tileCount;

        public TiledDrawOperation(Rect bounds, TileDrawEntry[] tiles, int tileCount)
        {
            Bounds = bounds;
            _tiles = tiles;
            _tileCount = tileCount;
        }

        public Rect Bounds { get; }

        public bool HitTest(Point p) => Bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);

        public override bool Equals(object? obj) => obj is ICustomDrawOperation cdo && Equals(cdo);

        public override int GetHashCode() => HashCode.Combine(Bounds, _tileCount);

        /// <summary>
        /// Executed on the render thread. Blits pre-rendered tile images.
        /// </summary>
        public void Render(ImmediateDrawingContext context)
        {
            Debug.ThrowOnUiThread();

            var tiles = _tiles;
            if (tiles is null)
            {
                return;
            }

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
                borderPaint.StrokeWidth = 5f;
#endif

                for (int i = 0; i < _tileCount; ++i)
                {
                    ref readonly var tile = ref tiles[i];
                    if (tile.ImageRef is { IsAlive: true, Item.Info.BytesSize: > 1 })
                    {
                        // BytesSize of 1 means it's empty
                        canvas.DrawImage(tile.ImageRef.Item, tile.SrcRect, tile.DestRect, RenderSamplingOptions, RenderPaint);
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
            var tiles = _tiles;
            if (tiles is null)
            {
                return;
            }

            _tiles = null;

            for (int i = 0; i < _tileCount; ++i)
            {
                tiles[i].Dispose();
                tiles[i] = default;
            }

            ArrayPool<TileDrawEntry>.Shared.Return(tiles, clearArray: false);
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
    /// Uses <see cref="int.MinValue"/> as the unset sentinel because tile levels can be
    /// negative (zoom-out), so any value a real level could take is ambiguous.
    /// </summary>
    private int _lastTileLevel = int.MinValue;

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

    /// <summary>
    /// Reusable buffer for building tile draw entries in <see cref="Render"/>.
    /// Entries are transferred to an <see cref="ArrayPool{T}"/>-backed array for the draw operation.
    /// Only accessed on the UI thread.
    /// </summary>
    private readonly List<TileDrawEntry> _renderTileEntries = new();

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
                newService.TileReady -= OnTileReady;
                newService.TileReady += OnTileReady;
                // Service just became available — request tiles if VisibleArea is already set.
                PrefetchVisibleTiles();
            }
        }
        else if (change.Property == VisibleAreaProperty
                 || change.Property == ZoomLevelProperty
                 || change.Property == PictureProperty)
        {
            // Prefetch tiles for the new visible area. AffectsRender handles the
            // redraw side — we only need to queue missing tiles here.
            PrefetchVisibleTiles();
        }
    }

    /// <summary>
    /// Snapshots what is needed to compute and queue missing tiles, then hands the work off to the
    /// thread pool. Runs on the UI thread, so the only per-call work here must be a few value-type
    /// reads and one <see cref="IRef{T}.Clone"/>. The cache lookup and channel writes happen on the
    /// background thread — doing them inline on the UI thread makes scrolling stutter because each
    /// scroll pixel bursts many locked cache operations and prioritized channel inserts.
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

        IRef<SKPicture> pictureClone;
        try
        {
            if (!picture.IsAlive)
            {
                return;
            }

            pictureClone = picture.Clone();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        var workItem = new PrefetchWorkItem(
            service,
            PageNumber,
            TileGrid.ComputeTileLevel(ZoomLevel),
            VisibleArea.Value,
            pageDisplaySize,
            PpiScale,
            pictureClone);

        ThreadPool.UnsafeQueueUserWorkItem(workItem, preferLocal: false);
    }

    /// <summary>
    /// Expands the visible area into a tile range, asks the cache which tiles are missing, and
    /// submits a batch request to the render service. Runs on a thread pool thread so the UI
    /// thread is never blocked on cache locks or channel inserts.
    /// </summary>
    private sealed class PrefetchWorkItem : IThreadPoolWorkItem
    {
        private readonly TileRenderService _service;
        private readonly int _pageNumber;
        private readonly int _tileLevel;
        private readonly Rect _visibleArea;
        private readonly Size _pageDisplaySize;
        private readonly double _ppiScale;
        private readonly IRef<SKPicture> _picture;

        public PrefetchWorkItem(TileRenderService service, int pageNumber, int tileLevel,
            Rect visibleArea, Size pageDisplaySize, double ppiScale, IRef<SKPicture> picture)
        {
            _service = service;
            _pageNumber = pageNumber;
            _tileLevel = tileLevel;
            _visibleArea = visibleArea;
            _pageDisplaySize = pageDisplaySize;
            _ppiScale = ppiScale;
            _picture = picture;
        }

        public void Execute()
        {
            try
            {
                GetTileRange(_visibleArea, _pageDisplaySize, _tileLevel, RenderTileMargin,
                    out int startCol, out int startRow, out int endCol, out int endRow);

                var missing = new List<TileCoord>();
                _service.Cache.FindMissing(_pageNumber, _tileLevel, startCol, startRow, endCol, endRow, missing);

                if (missing.Count > 0)
                {
                    _service.RequestTiles(_pageNumber, _picture, _tileLevel,
                        CollectionsMarshal.AsSpan(missing), _ppiScale, _pageDisplaySize, _visibleArea);
                }
            }
            catch (Exception e)
            {
                Debug.WriteExceptionToFile(e);
            }
            finally
            {
                _picture.Dispose();
            }
        }
    }

    /// <summary>
    /// Computes the tile column/row range for the given visible area, expanded by a margin
    /// and clamped to the grid dimensions.
    /// </summary>
    private static void GetTileRange(in Rect visibleArea, in Size pageDisplaySize, int tileLevel, int margin,
        out int startCol, out int startRow, out int endCol, out int endRow)
    {
        var gridDims = TileGrid.GetGridDimensions(in pageDisplaySize, tileLevel);
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
    /// Returns a <see cref="TileDrawEntry"/> with the appropriate sub-region of the fallback image,
    /// or null if no fallback is available.
    /// </summary>
    /// <param name="cache">The tile cache to search.</param>
    /// <param name="pageNumber">The page number.</param>
    /// <param name="tileLevel">The tile level of the missing tile.</param>
    /// <param name="col">Column of the missing tile.</param>
    /// <param name="row">Row of the missing tile.</param>
    /// <param name="pageDisplaySize">The page display size.</param>
    /// <returns>A fallback tile entry with upscaled source rect, or null.</returns>
    private static TileDrawEntry? TryGetFallbackTile(TileCache cache, int pageNumber, int tileLevel, int col, int row, in Size pageDisplaySize)
    {
        // Search lower levels (coarser tiles) for a cached tile that covers this area.
        // At fallback level fl (where fl < tileLevel), the covering tile is at
        // (col >> d, row >> d) where d = tileLevel - fl. Walks down to
        // TileGrid.MinTileLevel so that zoom-out fallbacks from a cached
        // negative-level tile are still found when the current level is also negative.
        for (int fl = tileLevel - 1; fl >= TileGrid.MinTileLevel; fl--)
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

            // Compute the sub-region within the fallback image that corresponds
            // to the missing tile's display area.
            //
            // The fallback image is TilePixelSize x TilePixelSize (or smaller for edge tiles).
            // Each current-level tile maps to a (TilePixelSize / divisor) pixel-wide strip
            // within the fallback image.
            float subPixelSize = (float)TileGrid.TilePixelSize / divisor;
            int subCol = col - (fallbackCol << levelDiff);
            int subRow = row - (fallbackRow << levelDiff);

            float srcX = subCol * subPixelSize;
            float srcY = subRow * subPixelSize;

            // Clamp to actual image dimensions for edge tiles
            float srcRight = Math.Min(srcX + subPixelSize, fallbackRef.Item.Width);
            float srcBottom = Math.Min(srcY + subPixelSize, fallbackRef.Item.Height);

            if (srcRight <= srcX || srcBottom <= srcY)
            {
                fallbackRef.Dispose();
                continue;
            }

            var srcRect = new SKRect(srcX, srcY, srcRight, srcBottom);

            // The destination is the display area of the missing tile
            var displayRect = TileGrid.GetTileDisplayRect(col, row, tileLevel, in pageDisplaySize);

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
        in Size pageDisplaySize, List<TileDrawEntry> entries, int[]? higherCachedLevels)
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
            var dims = TileGrid.GetGridDimensions(in pageDisplaySize, fl);
            endCol = Math.Min(endCol, dims.Width);
            endRow = Math.Min(endRow, dims.Height);

            bool foundAny = false;
            for (int r = startRow; r < endRow; ++r)
            {
                for (int c = startCol; c < endCol; ++c)
                {
                    var finerKey = new TileKey(pageNumber, fl, c, r);
                    if (cache.TryGet(finerKey, out var imageRef) && imageRef is not null)
                    {
                        // Each finer tile maps to its own (smaller) display (dest) rect 
                        // draw the full image at that position.
                        var destRect = TileGrid.GetTileDisplayRect(c, r, fl, in pageDisplaySize).ToSKRect();
                        var srcRect = new SKRect(0, 0, imageRef.Item.Width, imageRef.Item.Height);
                        entries.Add(new TileDrawEntry(imageRef, srcRect, destRect));
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
        if (_lastTileLevel != int.MinValue && _lastTileLevel != tileLevel)
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

        int rangeCols = endCol - startCol + 1;
        int rangeRows = endRow - startRow + 1;
        int tileCount = rangeCols * rangeRows;

        System.Diagnostics.Debug.Assert(tileCount >= 0);

        _renderTileEntries.Clear();
        _renderTileEntries.EnsureCapacity(tileCount);

        bool allVisibleTilesCached = true;

        // Batch the exact-level lookups under a single cache lock acquisition instead of N
        // locked TryGet calls. With a background renderer concurrently adding tiles and a
        // separate prefetch thread calling FindMissing, per-tile locking here was the main
        // source of frame-time jitter during fast scrolling.
        var exactLevelRefs = ArrayPool<IRef<SKImage>?>.Shared.Rent(tileCount);
        try
        {
            service.Cache.TryGetRange(pageNumber, tileLevel,
                startCol, startRow, endCol, endRow,
                exactLevelRefs.AsSpan(0, tileCount));

            // Query cached higher levels once per render. Passed into the finer-level fallback
            // search so it iterates only levels that actually have tiles — this avoids scanning
            // large empty tile grids at finer levels (4x per level) while still finding fallbacks
            // when the user zooms out past many levels (otherwise tiles from deeply zoomed-in
            // views would be skipped, leaving the page blank until exact-level tiles render).
            int[]? higherCachedLevels = null;
            bool higherCachedLevelsFetched = false;

            for (int r = 0; r < rangeRows; r++)
            {
                for (int c = 0; c < rangeCols; c++)
                {
                    int flatIndex = r * rangeCols + c;
                    int col = startCol + c;
                    int row = startRow + r;
                    var imageRef = exactLevelRefs[flatIndex];

                    if (imageRef is not null)
                    {
                        // Exact-level tile available — use full image as source.
                        var destRect = TileGrid.GetTileDisplayRect(col, row, tileLevel, in pageDisplaySize).ToSKRect();
                        var srcRect = new SKRect(0, 0, imageRef.Item.Width, imageRef.Item.Height);
                        _renderTileEntries.Add(new TileDrawEntry(imageRef, srcRect, destRect));
                    }
                    else
                    {
                        allVisibleTilesCached = false;

                        // Try coarser (lower-level) fallback first — single upscaled tile.
                        var fallbackEntry = TryGetFallbackTile(service.Cache, pageNumber, tileLevel, col, row, in pageDisplaySize);
                        if (fallbackEntry.HasValue)
                        {
                            _renderTileEntries.Add(fallbackEntry.Value);
                        }
                        else
                        {
                            // Only ask for the finer-level set when we actually need it. In the
                            // common case where every exact-level tile hits or a coarser fallback
                            // is available, we skip this locked snapshot entirely.
                            if (!higherCachedLevelsFetched)
                            {
                                higherCachedLevels = service.Cache.GetCachedLevelsAbove(pageNumber, tileLevel);
                                higherCachedLevelsFetched = true;
                            }

                            // Try finer (higher-level) fallback — multiple cached tiles may cover this area.
                            // This handles zoom-out: old higher-resolution tiles fill the gap until
                            // coarser tiles are rendered.
                            AddHigherLevelFallbackTiles(service.Cache, pageNumber, tileLevel, col, row, in pageDisplaySize, _renderTileEntries, higherCachedLevels);
                        }
                    }
                }
            }
        }
        finally
        {
            // Clear references before returning to the pool — the entries we handed to
            // _renderTileEntries hold the clones we still need; any untransferred slots are null.
            Array.Clear(exactLevelRefs, 0, tileCount);
            ArrayPool<IRef<SKImage>?>.Shared.Return(exactLevelRefs, clearArray: false);
        }

        // Evict stale tile levels only after all visible+margin tiles at the current level
        // are cached, so old tiles remain available as fallbacks during the transition.
        // Eviction runs on a background thread to avoid lock acquisition and bitmap
        // disposal during the render pass.
        if (allVisibleTilesCached && _staleLevelEvictionPending)
        {
            _staleLevelEvictionPending = false;
            _ = Task.Run(() =>
            {
                try
                {
                    service.EvictStaleLevels(pageNumber, tileLevel);
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine(e);
                }
            });
        }

        // Transfer entries to an ArrayPool-backed array for the draw operation.
        // The draw operation takes ownership and returns the array to the pool on Dispose.
        int entryCount = _renderTileEntries.Count;
        var tileBuffer = ArrayPool<TileDrawEntry>.Shared.Rent(Math.Max(entryCount, 1));
        CollectionsMarshal.AsSpan(_renderTileEntries).CopyTo(tileBuffer);
        _renderTileEntries.Clear();

        context.Custom(new TiledDrawOperation(viewPort, tileBuffer, entryCount));
    }
}
