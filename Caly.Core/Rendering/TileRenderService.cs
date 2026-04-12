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
    private sealed class TileRequest : IEquatable<TileRequest>
    {
        public TileKey Key { get; }
        public IRef<SKPicture> Picture { get; }
        public double PpiScale { get; }
        public Size PageDisplaySize { get; }
        public CancellationToken Token { get; }

        public TileRequest(TileKey key, IRef<SKPicture> picture, double ppiScale, Size pageDisplaySize, CancellationToken token)
        {
            Key = key;
            Picture = picture;
            PpiScale = ppiScale;
            PageDisplaySize = pageDisplaySize;
            Token = token;
        }

        public bool Equals(TileRequest? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Key.Equals(other.Key);
        }

        public override bool Equals(object? obj) => obj is TileRequest other && Equals(other);

        public override int GetHashCode() => Key.GetHashCode();
    }

    private sealed class TileRequestComparer : IComparer<TileRequest>
    {
        public static readonly TileRequestComparer Instance = new();

        public int Compare(TileRequest? x, TileRequest? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (y is null)
            {
                return 1;
            }

            if (x is null)
            {
                return -1;
            }

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
    private readonly CancellationTokenSource _cts = new();
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
    /// Cancelled CTS instances awaiting disposal. We cannot dispose them immediately because
    /// in-flight requests may still hold a <see cref="CancellationToken"/> derived from the CTS,
    /// and <see cref="CancellationTokenSource.Dispose"/> would tear down the linked registration
    /// while a concurrent <c>RequestTiles</c> call might still read <c>.Token</c> (which throws
    /// <see cref="ObjectDisposedException"/> after disposal). They are disposed in <see cref="DisposeAsync"/>.
    /// </summary>
    private readonly ConcurrentBag<CancellationTokenSource> _retiredTokens = new();

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
    { }

    public TileRenderService(TileCache cache)
    {
        _mainToken = _cts.Token;
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
            await Parallel.ForEachAsync(_requestReader.ReadAllAsync(_mainToken), options, async (request, ct) =>
            {
                try
                {
                    if (request.Token.IsCancellationRequested)
                    {
                        return;
                    }

                    await RenderTile(request);
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
            });
        }
        catch (OperationCanceledException)
        {
            // Service is shutting down
        }
    }

    private Task RenderTile(TileRequest request)
    {
        // Skip if already in cache (another request may have rendered it)
        if (Cache.Contains(request.Key))
        {
            return Task.CompletedTask;
        }

        request.Token.ThrowIfCancellationRequested();

        var matrix = TileGrid.CreateRenderMatrix(request.Key.Column, request.Key.Row,
            request.PpiScale, request.Key.TileLevel);

        // Compute actual tile dimensions (edge tiles may be smaller)
        double tileScale = TileGrid.GetTileLevelScale(request.Key.TileLevel);
        int pagePixelWidth = (int)Math.Ceiling(request.PageDisplaySize.Width * tileScale);
        int pagePixelHeight = (int)Math.Ceiling(request.PageDisplaySize.Height * tileScale);

        int tileWidth = Math.Min(TileGrid.TilePixelSize, pagePixelWidth - request.Key.Column * TileGrid.TilePixelSize);
        int tileHeight = Math.Min(TileGrid.TilePixelSize, pagePixelHeight - request.Key.Row * TileGrid.TilePixelSize);

        if (tileWidth <= 0 || tileHeight <= 0)
        {
            return Task.CompletedTask;
        }

        var imageInfo = new SKImageInfo(tileWidth, tileHeight, SKColorType.Bgra8888, SKAlphaType.Premul);

        using var surface = SKSurface.Create(imageInfo);
        if (surface is null)
        {
            return Task.CompletedTask;
        }

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        request.Token.ThrowIfCancellationRequested();

        canvas.SetMatrix(matrix);

        using (var paint = new SKPaint())
        {
            paint.IsAntialias = true;
            paint.IsDither = true;

            canvas.DrawPicture(request.Picture.Item, paint);
        }

        request.Token.ThrowIfCancellationRequested();

        // Extract bitmap from surface
        var bitmap = new SKBitmap(imageInfo);
        try
        {
            surface.ReadPixels(imageInfo, bitmap.GetPixels(), imageInfo.RowBytes, 0, 0);
            Cache.Add(request.Key, bitmap);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }

        Cache.Add(request.Key, bitmap);
        TileReady?.Invoke(request.Key);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Requests tiles for a page. Missing tiles are queued for background rendering.
    /// The caller provides a cloned picture reference per tile.
    /// </summary>
    /// <param name="pageNumber">The page number.</param>
    /// <param name="picture">A cloned reference to the page's SKPicture. The caller retains ownership of this reference;
    /// the service will clone it internally for each tile request.</param>
    /// <param name="tileLevel">The tile level to render at.</param>
    /// <param name="tiles">List of (column, row) tile coordinates to render.</param>
    /// <param name="ppiScale">The PPI scale factor.</param>
    /// <param name="pageDisplaySize">The page display size (in display coordinates).</param>
    public void RequestTiles(int pageNumber, IRef<SKPicture> picture, int tileLevel, IReadOnlyList<TileCoord> tiles, double ppiScale, Size pageDisplaySize)
    {
        if (_mainToken.IsCancellationRequested)
        {
            return;
        }

        // Get or create per-page cancellation token.
        // If a previous CTS was cancelled (by CancelPage/InvalidatePage), replace it.
        CancellationTokenSource pageCts;
        while (true)
        {
            if (_mainToken.IsCancellationRequested)
            {
                return;
            }

            pageCts = _pageTokens.GetOrAdd(pageNumber, _ => CancellationTokenSource.CreateLinkedTokenSource(_mainToken));
            if (!pageCts.IsCancellationRequested)
            {
                break;
            }

            // CTS was cancelled — try to replace it with a fresh one
            var newCts = CancellationTokenSource.CreateLinkedTokenSource(_mainToken);
            if (_pageTokens.TryUpdate(pageNumber, newCts, pageCts))
            {
                _retiredTokens.Add(pageCts);
                continue;
            }

            // Another thread replaced it — dispose the one we just created
            newCts.Dispose();
        }

        // Capture the token now — if CancelPage races and cancels pageCts after
        // we exited the while-loop, the token is simply already-cancelled and the
        // processing loop will skip the request. No ObjectDisposedException risk
        // because retired CTS instances are not disposed until DisposeAsync.
        var pageToken = pageCts.Token;

        // Check, add to _inFlight, clone, and write in a single pass so that
        // _inFlight entries are never orphaned on partial failure.
        foreach (var coord in tiles)
        {
            var key = new TileKey(pageNumber, tileLevel, coord.Column, coord.Row);

            // Skip if already in cache or in-flight
            if (Cache.Contains(key) || !_inFlight.TryAdd(key, 0))
            {
                continue;
            }

            IRef<SKPicture> pictureClone;
            try
            {
                pictureClone = picture.Clone();
            }
            catch (ObjectDisposedException)
            {
                _inFlight.TryRemove(key, out _);
                return; // Picture is gone — no point continuing the loop
            }

            var request = new TileRequest(key, pictureClone, ppiScale, pageDisplaySize, pageToken);
            if (!_requestWriter.TryWrite(request))
            {
                pictureClone.Dispose();
                _inFlight.TryRemove(key, out _);
                return; // Channel is completed — no point continuing the loop
            }
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
            _retiredTokens.Add(cts);
        }

        Cache.InvalidatePage(pageNumber);
    }

    /// <summary>
    /// Cancels pending tile requests for a page without removing cached tiles.
    /// Call this when a page scrolls out of the visible area.
    /// </summary>
    public void CancelPage(int pageNumber)
    {
        if (_pageTokens.TryRemove(pageNumber, out var cts))
        {
            cts.Cancel();
            _retiredTokens.Add(cts);
        }
    }

    private int _disposed;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _cts.CancelAsync();
        _requestWriter.TryComplete();

        try
        {
            await _processingLoopTask;
        }
        catch
        {
            // No op
        }

        // Drain any remaining items in the channel to dispose their picture refs
        while (_requestReader.TryRead(out var leftover))
        {
            leftover.Picture.Dispose();
        }

        foreach (var kvp in _pageTokens)
        {
            kvp.Value.Dispose();
        }

        _pageTokens.Clear();

        while (_retiredTokens.TryTake(out var retired))
        {
            retired.Dispose();
        }

        _inFlight.Clear();
        Cache.Dispose();
        _cts.Dispose();
    }
}
