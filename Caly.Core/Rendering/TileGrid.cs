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
using SkiaSharp;

namespace Caly.Core.Rendering;

/// <summary>
/// Computes tile layout geometry for a page at a given tile level.
/// </summary>
public static class TileGrid
{
    /// <summary>
    /// The size of each tile in pixels (at the tile level's resolution).
    /// </summary>
    public const int TilePixelSize = 256;

    /// <summary>
    /// Computes the tile level for a given zoom level.
    /// Tile level is the ceiling of log2(zoomLevel), clamped to >= 0.
    /// This ensures tiles are always rendered at or above the needed resolution.
    /// </summary>
    public static int ComputeTileLevel(double zoomLevel)
    {
        if (zoomLevel <= 1.0)
        {
            return 0;
        }

        return (int)Math.Ceiling(Math.Log2(zoomLevel));
    }

    /// <summary>
    /// Gets the scale factor for a given tile level: 2^tileLevel.
    /// </summary>
    public static double GetTileLevelScale(int tileLevel)
    {
        return Math.Pow(2, tileLevel);
    }

    /// <summary>
    /// Gets the number of tile columns and rows for a page at a given tile level.
    /// </summary>
    /// <param name="pageDisplaySize">Page size in display coordinates (already scaled by ppiScale).</param>
    /// <param name="tileLevel">The tile level.</param>
    /// <returns>Number of columns and rows in the tile grid.</returns>
    public static (int Columns, int Rows) GetGridDimensions(Size pageDisplaySize, int tileLevel)
    {
        double tileScale = GetTileLevelScale(tileLevel);
        int pixelWidth = (int)Math.Ceiling(pageDisplaySize.Width * tileScale);
        int pixelHeight = (int)Math.Ceiling(pageDisplaySize.Height * tileScale);

        int columns = (pixelWidth + TilePixelSize - 1) / TilePixelSize;
        int rows = (pixelHeight + TilePixelSize - 1) / TilePixelSize;

        return (Math.Max(1, columns), Math.Max(1, rows));
    }

    /// <summary>
    /// Gets the rectangle in display coordinates that a tile covers,
    /// clamped to the page bounds for edge tiles.
    /// Adjacent tiles share exact edge coordinates (same expression for right/left),
    /// so seam prevention is handled at draw time by disabling edge anti-aliasing.
    /// </summary>
    public static Rect GetTileDisplayRect(int col, int row, int tileLevel, Size pageDisplaySize)
    {
        double invScale = 1.0 / GetTileLevelScale(tileLevel);
        double tileDisplaySize = TilePixelSize * invScale;

        double left = col * tileDisplaySize;
        double top = row * tileDisplaySize;
        double right = Math.Min((col + 1) * tileDisplaySize, pageDisplaySize.Width);
        double bottom = Math.Min((row + 1) * tileDisplaySize, pageDisplaySize.Height);

        return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    /// <summary>
    /// Gets the list of tile coordinates that intersect the visible area.
    /// </summary>
    /// <param name="visibleArea">Visible area in display coordinates.</param>
    /// <param name="pageDisplaySize">Page size in display coordinates.</param>
    /// <param name="tileLevel">The tile level.</param>
    /// <param name="results">Optional pre-allocated list to reuse. If provided, it will be cleared and filled.
    /// If null, a new list is allocated.</param>
    /// <returns>List of (column, row) pairs for visible tiles.</returns>
    public static List<(int Col, int Row)> GetVisibleTiles(Rect visibleArea, Size pageDisplaySize, int tileLevel,
        List<(int Col, int Row)>? results = null)
    {
        var (totalCols, totalRows) = GetGridDimensions(pageDisplaySize, tileLevel);
        double tileScale = GetTileLevelScale(tileLevel);
        double tileDisplaySize = TilePixelSize / tileScale;

        int startCol = Math.Max(0, (int)(visibleArea.Left / tileDisplaySize));
        int startRow = Math.Max(0, (int)(visibleArea.Top / tileDisplaySize));
        int endCol = Math.Min(totalCols - 1, (int)(visibleArea.Right / tileDisplaySize));
        int endRow = Math.Min(totalRows - 1, (int)(visibleArea.Bottom / tileDisplaySize));

        int capacity = (endCol - startCol + 1) * (endRow - startRow + 1);
        if (results is null)
        {
            results = new List<(int, int)>(capacity);
        }
        else
        {
            results.Clear();
            if (results.Capacity < capacity)
            {
                results.Capacity = capacity;
            }
        }

        for (int r = startRow; r <= endRow; r++)
        {
            for (int c = startCol; c <= endCol; c++)
            {
                results.Add((c, r));
            }
        }

        return results;
    }

    /// <summary>
    /// Creates the SKMatrix to render a tile from an SKPicture.
    /// The matrix translates and scales so that the tile's region of the page
    /// maps to a (0, 0, TilePixelSize, TilePixelSize) output surface.
    /// </summary>
    /// <param name="col">Tile column.</param>
    /// <param name="row">Tile row.</param>
    /// <param name="ppiScale">PPI scale factor (e.g. 2.0).</param>
    /// <param name="tileLevel">The tile level.</param>
    /// <returns>Matrix to apply before drawing the SKPicture onto a tile-sized surface.</returns>
    public static SKMatrix CreateRenderMatrix(int col, int row, double ppiScale, int tileLevel)
    {
        float renderScale = (float)(ppiScale * GetTileLevelScale(tileLevel));

        // For a PDF point (x, y) we need:
        //   surfaceX = x * renderScale - col * TilePixelSize
        //   surfaceY = y * renderScale - row * TilePixelSize
        //
        // This is a scale followed by a translation, expressed directly
        // to avoid ambiguity with SKMatrix.Concat argument order.
        return new SKMatrix(
            renderScale, 0, -col * TilePixelSize,
            0, renderScale, -row * TilePixelSize,
            0, 0, 1);
    }
}
