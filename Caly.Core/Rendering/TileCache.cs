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
using Caly.Core.Utilities;
using SkiaSharp;

namespace Caly.Core.Rendering;

/// <summary>
/// Thread-safe LRU tile cache with a configurable memory budget.
/// Tiles are stored as ref-counted <see cref="SKBitmap"/> instances.
/// </summary>
public sealed class TileCache : IDisposable
{
    private sealed class CacheEntry
    {
        public IRef<SKBitmap> Bitmap { get; }

        public TileKey Key { get; }

        public int MemorySize { get; }

        public LinkedListNode<TileKey>? LruNode { get; set; }

        public CacheEntry(IRef<SKBitmap> bitmap, TileKey key, int memorySize)
        {
            Bitmap = bitmap;
            Key = key;
            MemorySize = memorySize;
        }
    }

    private readonly Dictionary<TileKey, CacheEntry> _entries = new();
    private readonly LinkedList<TileKey> _lruList = new();
    private readonly Lock _lock = new();
    private readonly long _maxMemoryBytes;

    private long _currentMemoryBytes;

    /// <summary>
    /// Creates a new tile cache with the specified memory budget.
    /// </summary>
    /// <param name="maxMemoryBytes">Maximum memory budget in bytes. Default is 256 MB.</param>
    public TileCache(long maxMemoryBytes = 256L * 1024 * 1024)
    {
        _maxMemoryBytes = maxMemoryBytes;
    }

    /// <summary>
    /// Gets the current memory usage of the cache in bytes.
    /// </summary>
    public long CurrentMemoryBytes
    {
        get
        {
            lock (_lock)
            {
                return _currentMemoryBytes;
            }
        }
    }

    /// <summary>
    /// Gets the number of tiles currently in the cache.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// Tries to get a tile from the cache, moving it to the front of the LRU list.
    /// Returns a cloned reference that the caller must dispose.
    /// </summary>
    public bool TryGet(TileKey key, out IRef<SKBitmap>? bitmapRef)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                if (entry.LruNode is not null)
                {
                    _lruList.Remove(entry.LruNode);
                    _lruList.AddFirst(entry.LruNode);
                }

                bitmapRef = entry.Bitmap.Clone();
                return true;
            }
        }

        bitmapRef = null;
        return false;
    }

    /// <summary>
    /// Adds a tile bitmap to the cache. If adding exceeds the memory budget,
    /// LRU tiles are evicted. If the key already exists, the call is ignored.
    /// The cache takes ownership of the bitmap.
    /// </summary>
    public void Add(TileKey key, SKBitmap bitmap)
    {
        int memorySize = bitmap.ByteCount;
        IRef<SKBitmap> bitmapRef = RefCountable.Create(bitmap);
        List<CacheEntry>? evicted = null;

        lock (_lock)
        {
            if (_entries.ContainsKey(key))
            {
                // Already in cache, dispose the new ref
                bitmapRef.Dispose();
                return;
            }

            // Reject tiles that exceed the entire budget � adding them would evict
            // everything and still blow past the limit.
            if (memorySize > _maxMemoryBytes)
            {
                bitmapRef.Dispose();
                return;
            }

            // Evict until under budget, collecting entries to dispose outside the lock
            while (_currentMemoryBytes + memorySize > _maxMemoryBytes && _lruList.Count > 0)
            {
                var evictedEntry = EvictOldestLocked();
                if (evictedEntry is not null)
                {
                    (evicted ??= []).Add(evictedEntry);
                }
            }

            var entry = new CacheEntry(bitmapRef, key, memorySize);
            var node = _lruList.AddFirst(key);
            entry.LruNode = node;

            _entries[key] = entry;
            _currentMemoryBytes += memorySize;
        }

        // Dispose evicted entries outside the lock
        if (evicted is not null)
        {
            foreach (var entry in evicted)
            {
                entry.Bitmap.Dispose();
            }
        }
    }

    /// <summary>
    /// Removes all tiles for a given page from the cache.
    /// </summary>
    public void InvalidatePage(int pageNumber)
    {
        List<CacheEntry> toDispose;

        lock (_lock)
        {
            // Collect entries to remove
            List<TileKey>? keysToRemove = null;
            foreach (var kvp in _entries)
            {
                if (kvp.Key.PageNumber == pageNumber)
                {
                    (keysToRemove ??= []).Add(kvp.Key);
                }
            }

            if (keysToRemove is null)
            {
                return;
            }

            toDispose = new List<CacheEntry>(keysToRemove.Count);
            foreach (var key in keysToRemove)
            {
                if (_entries.TryGetValue(key, out var entry))
                {
                    toDispose.Add(entry);
                    RemoveEntryLocked(entry);
                }
            }
        }

        // Dispose outside the lock
        foreach (var entry in toDispose)
        {
            entry.Bitmap.Dispose();
        }
    }

    /// <summary>
    /// Removes all tiles for a given page whose tile level differs from <paramref name="keepLevel"/>.
    /// This prevents stale high-res (or low-res) tiles from consuming budget after a zoom change.
    /// </summary>
    public void EvictPageLevelsExcept(int pageNumber, int keepLevel)
    {
        List<CacheEntry> toDispose;

        lock (_lock)
        {
            List<TileKey>? keysToRemove = null;
            foreach (var kvp in _entries)
            {
                if (kvp.Key.PageNumber == pageNumber && kvp.Key.TileLevel != keepLevel)
                {
                    (keysToRemove ??= []).Add(kvp.Key);
                }
            }

            if (keysToRemove is null)
            {
                return;
            }

            toDispose = new List<CacheEntry>(keysToRemove.Count);
            foreach (var key in keysToRemove)
            {
                if (_entries.TryGetValue(key, out var entry))
                {
                    toDispose.Add(entry);
                    RemoveEntryLocked(entry);
                }
            }
        }

        foreach (var entry in toDispose)
        {
            entry.Bitmap.Dispose();
        }
    }

    /// <summary>
    /// Checks whether a tile exists in the cache without modifying LRU order.
    /// </summary>
    public bool Contains(TileKey key)
    {
        lock (_lock)
        {
            return _entries.ContainsKey(key);
        }
    }

    /// <summary>
    /// Returns the distinct tile levels strictly above <paramref name="baseLevel"/> for which
    /// the given page has any cached tiles, sorted ascending (closest level first).
    /// Used to drive the finer-level fallback search when the exact-level tile for a
    /// zoomed-out view is not yet rendered.
    /// </summary>
    public IReadOnlyCollection<int>? GetCachedLevelsAbove(int pageNumber, int baseLevel)
    {
        SortedSet<int>? seen = null;

        lock (_lock)
        {
            foreach (var key in _entries.Keys)
            {
                if (key.PageNumber == pageNumber && key.TileLevel > baseLevel)
                {
                    (seen ??= new SortedSet<int>()).Add(key.TileLevel);
                }
            }
        }

        return seen;
    }

    /// <summary>
    /// Removes the oldest entry from the cache and returns it for disposal outside the lock.
    /// Returns null if the LRU list is empty.
    /// </summary>
    private CacheEntry? EvictOldestLocked()
    {
        var oldest = _lruList.Last;
        if (oldest is null)
        {
            return null;
        }

        var entry = _entries[oldest.Value];
        RemoveEntryLocked(entry);
        return entry;
    }

    private void RemoveEntryLocked(CacheEntry entry)
    {
        _entries.Remove(entry.Key);
        if (entry.LruNode is not null)
        {
            _lruList.Remove(entry.LruNode);
            entry.LruNode = null;
        }

        _currentMemoryBytes -= entry.MemorySize;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var entry in _entries.Values)
            {
                entry.Bitmap.Dispose();
            }

            _entries.Clear();
            _lruList.Clear();
            _currentMemoryBytes = 0;
        }
    }
}
