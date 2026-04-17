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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia;
using Caly.Core.Utilities;
using SkiaSharp;

namespace Caly.Core.Rendering;

/// <summary>
/// Background tile rendering service. One instance per document.
/// Renders SKPicture regions into tile-sized bitmaps on background threads.
/// </summary>
public sealed class TileRenderService : IAsyncDisposable
{
    private readonly struct TileRequest
    {
        public TileKey Key { get; }
        public IRef<SKPicture> Picture { get; }
        public double PpiScale { get; }
        public Size PageDisplaySize { get; }
        public CancellationToken Token { get; }

        public TileRequest(in TileKey key, IRef<SKPicture> picture, double ppiScale, in Size pageDisplaySize, CancellationToken token)
        {
            Key = key;
            Picture = picture;
            PpiScale = ppiScale;
            PageDisplaySize = pageDisplaySize;
            Token = token;
        }
    }

    private sealed class TileRequestComparer : IComparer<TileRequest>
    {
        public static readonly TileRequestComparer Instance = new();

        public int Compare(TileRequest x, TileRequest y)
        {
            // Prioritize by page number first, then by tile position (top-to-bottom, left-to-right)
            int cmp = x.Key.PageNumber.CompareTo(y.Key.PageNumber);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = x.Key.Row.CompareTo(y.Key.Row);
            if (cmp != 0)
            {
                return cmp;
            }

            return x.Key.Column.CompareTo(y.Key.Column);
        }
    }

    private readonly ChannelWriter<TileRequest> _requestWriter;
    private readonly ChannelReader<TileRequest> _requestReader;
    private readonly CancellationTokenSource _mainCts = new();
    private readonly CancellationToken _mainToken;
    private readonly Task _processingLoopTask;

    /// <summary>
    /// Tracks in-flight tile requests to avoid duplicate renders.
    /// </summary>
    private readonly ConcurrentDictionary<TileKey, byte> _inFlight = new();

    /// <summary>
    /// Per-page cancellation tokens for cancelling requests when pages scroll out of view.
    /// </summary>
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _pageTokens = new();

    /// <summary>
    /// Per-thread reusable SKPaint to avoid allocating one per tile render.
    /// </summary>
    [ThreadStatic]
    private static SKPaint? t_renderPaint;

    /// <summary>
    /// Fired when a tile has been rendered and is available in the cache.
    /// The handler receives the <see cref="TileKey"/> of the completed tile.
    /// This event may be raised from a background thread.
    /// </summary>
    public event Action<TileKey>? TileReady;

    /// <summary>
    /// Gets the tile cache used by this service.
    /// </summary>
    public TileCache Cache { get; }

    public TileRenderService() : this(new TileCache())
    {
    }

    public TileRenderService(TileCache cache)
    {
        _mainToken = _mainCts.Token;
        Cache = cache;

        var channel = Channel.CreateUnboundedPrioritized(new UnboundedPrioritizedChannelOptions<TileRequest>()
        {
            Comparer = TileRequestComparer.Instance,
            SingleWriter = false,
            SingleReader = false
        });

        _requestWriter = channel.Writer;
        _requestReader = channel.Reader;

        _processingLoopTask = Task.Run(ProcessingLoop);
    }

    private async Task ProcessingLoop()
    {
        var options = new ParallelOptions()
        {
            MaxDegreeOfParallelism = 2,
            CancellationToken = _mainToken
        };

        try
        {
            await Parallel.ForEachAsync(_requestReader.ReadAllAsync(_mainToken), options, (request, ct) =>
            {
                try
                {
                    if (request.Token.IsCancellationRequested)
                    {
                        return ValueTask.CompletedTask;
                    }

                    RenderTile(in request);
                }
                catch (OperationCanceledException)
                {
                    // Expected when pages scroll out of view
                }
                catch (Exception e)
                {
                    Debug.WriteExceptionToFile(e);
                }
                finally
                {
                    _inFlight.TryRemove(request.Key, out _);
                    request.Picture.Dispose();
                }

                return ValueTask.CompletedTask;
            });
        }
        catch (OperationCanceledException)
        {
            // Service is shutting down
        }
    }

    private void RenderTile(in TileRequest request)
    {
        Debug.ThrowOnUiThread();

        // Skip if already in cache (another request may have rendered it)
        if (Cache.Contains(request.Key))
        {
            return;
        }

        request.Token.ThrowIfCancellationRequested();

        // Compute actual tile dimensions (edge tiles may be smaller)
        double tileScale = TileGrid.GetTileLevelScale(request.Key.TileLevel);
        int pagePixelWidth = (int)Math.Ceiling(request.PageDisplaySize.Width * tileScale);
        int pagePixelHeight = (int)Math.Ceiling(request.PageDisplaySize.Height * tileScale);

        int tileWidth = Math.Min(TileGrid.TilePixelSize, pagePixelWidth - request.Key.Column * TileGrid.TilePixelSize);
        int tileHeight = Math.Min(TileGrid.TilePixelSize, pagePixelHeight - request.Key.Row * TileGrid.TilePixelSize);

        if (tileWidth <= 0 || tileHeight <= 0)
        {
            return;
        }

        var imageInfo = new SKImageInfo(tileWidth, tileHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        var surface = SKSurface.Create(imageInfo);

        if (surface is null)
        {
            return;
        }

        try
        {
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            request.Token.ThrowIfCancellationRequested();

            var matrix = TileGrid.CreateRenderMatrix(request.Key.Column, request.Key.Row, request.PpiScale, request.Key.TileLevel);
            canvas.SetMatrix(in matrix);

            // Reuse thread-local paint
            var paint = t_renderPaint ??= new SKPaint { IsAntialias = false, IsDither = true };
            canvas.DrawPicture(request.Picture.Item, paint);

            request.Token.ThrowIfCancellationRequested();

            // Extract bitmap from surface, create SKImage, and dispose the bitmap.
            // SetImmutable allows SKImage.FromBitmap to share pixel data (zero-copy).
            // The SKImage holds its own native reference to the pixels, so the bitmap
            // can be safely disposed. Caching the SKImage avoids per-frame
            // SKImage.FromBitmap allocations in the draw loop.
            var bitmap = new SKBitmap(imageInfo);
            surface.ReadPixels(imageInfo, bitmap.GetPixels(), imageInfo.RowBytes, 0, 0);
            bitmap.SetImmutable();

            var image = SKImage.FromBitmap(bitmap);
            bitmap.Dispose();

            int memorySize = imageInfo.BytesSize;
            Cache.Add(request.Key, image, memorySize);

            TileReady?.Invoke(request.Key);
        }
        finally
        {
            surface.Dispose();
        }
    }

    /// <summary>
    /// Retrieves a cancellation token associated with the specified page number, enabling cooperative cancellation of
    /// operations for that page.
    /// </summary>
    /// <remarks>If a cancellation token for the specified page does not already exist, a new one is created
    /// and linked to the root cancellation token. If the token source has been disposed due to a prior cancellation,
    /// the method retries to ensure a valid token is returned.</remarks>
    /// <returns>A cancellation token that is linked to the specified page. The token can be used to observe cancellation
    /// requests for operations related to the given page.</returns>
    private CancellationToken GetPageCancellationToken(int pageNumber)
    {
        // Get or create per-page cancellation token
        CancellationToken pageToken;
        while (true)
        {
            var cts = _pageTokens.GetOrAdd(pageNumber, static (_, root) =>
                CancellationTokenSource.CreateLinkedTokenSource(root), _mainToken);
            try
            {
                pageToken = cts.Token;
                break;
            }
            catch (ObjectDisposedException)
            {
                // Raced with CancelPage — remove the stale entry and retry
                _pageTokens.TryRemove(KeyValuePair.Create(pageNumber, cts));
            }
        }

        return pageToken;
    }

    /// <summary>
    /// Requests tiles for a page. Missing tiles are queued for background rendering.
    /// The caller provides a cloned picture reference per tile.
    /// </summary>
    /// <param name="pageNumber">The page number.</param>
    /// <param name="picture">A cloned reference to the page's SKPicture. The caller retains ownership of this reference;
    /// the service will clone it internally for each tile request.</param>
    /// <param name="tileLevel">The tile level to render at.</param>
    /// <param name="tiles">Span of (column, row) tile coordinates to render.</param>
    /// <param name="ppiScale">The PPI scale factor.</param>
    /// <param name="pageDisplaySize">The page display size (in display coordinates).</param>
    public void RequestTiles(int pageNumber, IRef<SKPicture> picture, int tileLevel, ReadOnlySpan<TileCoord> tiles, double ppiScale, in Size pageDisplaySize)
    {
        if (_mainToken.IsCancellationRequested)
        {
            return;
        }

        // Get or create per-page cancellation token
        var pageToken = GetPageCancellationToken(pageNumber);

        // Clone the caller's picture once up front. Holding this local ref guarantees
        // the underlying SKPicture stays alive for the duration of this method, so the
        // per-tile clones below cannot race with disposal and fail partway through the
        // batch (which previously caused remaining tiles to be silently skipped and
        // never rendered).
        IRef<SKPicture> batchPicture;
        try
        {
            if (!picture.IsAlive)
            {
                return;
            }

            batchPicture = picture.Clone();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            foreach (ref readonly var tile in tiles)
            {
                var key = new TileKey(pageNumber, tileLevel, tile.Column, tile.Row);

                // _inFlight is sufficient as the dedup guard here: the caller already filtered
                // cached tiles via TileCache.FindMissing, and the render worker re-checks the
                // cache before allocating a surface. Re-acquiring the cache lock per tile just
                // to repeat the Contains check serialized batches with concurrent Cache.Add
                // operations for no benefit.
                if (!_inFlight.TryAdd(key, 0))
                {
                    continue;
                }

                // Safe: batchPicture is held alive for the entire loop, so Clone cannot
                // fail with ObjectDisposedException here.
                var pictureClone = batchPicture.Clone();

                var request = new TileRequest(in key, pictureClone, ppiScale, in pageDisplaySize, pageToken);
                if (!_requestWriter.TryWrite(request))
                {
                    pictureClone.Dispose();
                    _inFlight.TryRemove(key, out _);
                }
            }
        }
        finally
        {
            batchPicture.Dispose();
        }
    }

    /// <summary>
    /// Cancels pending tile requests for a page and removes its tiles from the cache.
    /// </summary>
    public void InvalidatePage(int pageNumber)
    {
        if (_pageTokens.TryRemove(pageNumber, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        Cache.InvalidatePage(pageNumber);
    }

    /// <summary>
    /// Evicts cached tiles for a page whose tile level differs from <paramref name="keepLevel"/>.
    /// Call this when the zoom level changes to free memory occupied by stale tile levels.
    /// </summary>
    public void EvictStaleLevels(int pageNumber, int keepLevel)
    {
        Cache.EvictPageLevelsExcept(pageNumber, keepLevel);
    }

    /// <summary>
    /// Cancels pending tile requests for a page without removing cached tiles.
    /// Call this when a page scrolls out of the visible area.
    /// </summary>
    public void CancelPage(int pageNumber)
    {
        if (!_pageTokens.TryRemove(pageNumber, out var cts))
        {
            return;
        }

        cts.Cancel();
        cts.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _mainCts.CancelAsync();

        try
        {
            await _processingLoopTask;
        }
        catch
        {
            // No op
        }

        foreach (var kvp in _pageTokens)
        {
            kvp.Value.Dispose();
        }

        _pageTokens.Clear();
        _inFlight.Clear();
        Cache.Dispose();
        _mainCts.Dispose();
    }
}
