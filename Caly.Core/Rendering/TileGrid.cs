
using System;
using Avalonia;
using SkiaSharp;

namespace Caly.Core.Rendering;

/// <summary>
/// A (column, row) coordinate in the tile grid.
/// </summary>
public readonly record struct TileCoord(int Column, int Row);

/// <summary>
/// Identifies a single tile in the tile grid for a given page and zoom level.
/// </summary>
public readonly record struct TileKey(int PageNumber, int TileLevel, int Column, int Row);

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
        return 1 << tileLevel; // Same as Math.Pow(2, tileLevel);
    }

    /// <summary>
    /// Gets the number of tile columns and rows for a page at a given tile level.
    /// </summary>
    /// <param name="pageDisplaySize">Page size in display coordinates (already scaled by ppiScale).</param>
    /// <param name="tileLevel">The tile level.</param>
    /// <returns>Number of columns (width) and rows (height) in the tile grid.</returns>
    public static PixelSize GetGridDimensions(in Size pageDisplaySize, int tileLevel)
    {
        double tileScale = GetTileLevelScale(tileLevel);
        int pixelWidth = (int)Math.Ceiling(pageDisplaySize.Width * tileScale);
        int pixelHeight = (int)Math.Ceiling(pageDisplaySize.Height * tileScale);

        int columns = (pixelWidth + TilePixelSize - 1) / TilePixelSize;
        int rows = (pixelHeight + TilePixelSize - 1) / TilePixelSize;

        return new PixelSize(Math.Max(1, columns), Math.Max(1, rows));
    }

    /// <summary>
    /// Gets the rectangle in display coordinates that a tile covers,
    /// clamped to the page bounds for edge tiles.
    /// Adjacent tiles share exact edge coordinates (same expression for right/left),
    /// so seam prevention is handled at draw time by disabling edge anti-aliasing.
    /// </summary>
    public static Rect GetTileDisplayRect(int col, int row, int tileLevel, in Size pageDisplaySize)
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
