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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Caly.Core.Services.Interfaces;
using SkiaSharp;

namespace Caly.Printing.Core;

/// <summary>
/// Shared rendering and encoding helpers used by all platform-specific print service implementations.
/// </summary>
public static class PrintServiceHelper
{
    /// <summary>
    /// Builds the standard CUPS IPP URI for a named printer on localhost.
    /// </summary>
    public static Uri BuildCupsUri(string printerName)
        => new($"ipp://localhost:631/printers/{Uri.EscapeDataString(printerName)}");

    /// <summary>
    /// Renders a single PDF page to an <see cref="SKBitmap"/> at the document's PPI scale,
    /// applying the per-page rotation stored in <paramref name="pageInfo"/>.
    /// Returns <c>null</c> if the page size or picture cannot be retrieved.
    /// </summary>
    public static async Task<SKBitmap?> RenderPageToBitmapAsync(
        IPdfDocumentService documentService,
        PrintPageInfo pageInfo,
        CancellationToken token)
    {
        var pageSize = await documentService.GetPageSizeAsync(pageInfo.PageNumber, token)
            .ConfigureAwait(false);
        if (pageSize is null)
            return null;

        using var picRef = await documentService.GetRenderPageAsync(pageInfo.PageNumber, token)
            .ConfigureAwait(false);
        if (picRef is null)
            return null;

        float ppiScale = (float)documentService.PpiScale;
        float pdfW = (float)pageSize.Value.Width;
        float pdfH = (float)pageSize.Value.Height;
        int rotation = pageInfo.Rotation;

        // Bitmap dimensions at PpiScale resolution, with rotation taken into account.
        int bitmapW = (rotation == 90 || rotation == 270)
            ? (int)(pdfH * ppiScale)
            : (int)(pdfW * ppiScale);
        int bitmapH = (rotation == 90 || rotation == 270)
            ? (int)(pdfW * ppiScale)
            : (int)(pdfH * ppiScale);

        var bitmap = new SKBitmap(bitmapW, bitmapH, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        // Apply the same rotation transform used for on-screen rendering.
        // The SKPicture coordinate space is ppiScale × PDF-point units.
        switch (rotation)
        {
            case 90:
                canvas.Translate(bitmapW, 0);
                canvas.RotateDegrees(90);
                break;
            case 180:
                canvas.Translate(bitmapW, bitmapH);
                canvas.RotateDegrees(180);
                break;
            case 270:
                canvas.Translate(0, bitmapH);
                canvas.RotateDegrees(270);
                break;
        }

        canvas.DrawPicture(picRef.Item);
        canvas.Flush();

        return bitmap;
    }

    /// <summary>
    /// Encodes <paramref name="bitmap"/> as a JPEG (quality 100) into a new <see cref="MemoryStream"/>
    /// positioned at offset 0.
    /// </summary>
    public static MemoryStream EncodeJpeg(SKBitmap bitmap)
    {
        var stream = new MemoryStream();
        using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, quality: 100);
        data.SaveTo(stream);
        stream.Position = 0;
        return stream;
    }
}
