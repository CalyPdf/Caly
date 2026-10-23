// Copyright (c) BobLd
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Caly.Core.Models.Ai;
using Caly.Core.Services.Interfaces;
using Caly.Pdf.Models;
using SkiaSharp;

namespace Caly.Core.Services.Ai;

/// <summary>
/// Gathers what the model is given for a page: its text, and a PNG of the page drawn once from
/// the cached picture (not through the tile cache, which is for the screen).
/// </summary>
internal sealed class AiPageContentProvider(IPdfDocumentService service)
{
    public const int ImageLongSide = 1024;

    public async Task<AiPageContent> GetAsync(int pageNumber, bool includeImage, CancellationToken token)
    {
        var layer = await service.GetPageTextLayerAsync(pageNumber, token).ConfigureAwait(false);
        string text = FormatText(layer);

        byte[]? png = null;
        if (includeImage)
        {
            png = await RenderPngAsync(pageNumber, token).ConfigureAwait(false);
        }

        return new AiPageContent(pageNumber, text, png);
    }

    /// <summary>
    /// Blocks in order, separated by a blank line; lines by a line break; words by a space.
    /// </summary>
    internal static string FormatText(PdfTextLayer? layer)
    {
        if (layer is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var block in layer.TextBlocks)
        {
            if (sb.Length > 0)
            {
                sb.Append("\n\n");
            }

            bool firstLine = true;
            foreach (var line in block.TextLines)
            {
                if (!firstLine)
                {
                    sb.Append('\n');
                }

                firstLine = false;

                int lineStart = sb.Length;
                foreach (var word in line.Words)
                {
                    // Spaces can be words of their own, so only separate two words that touch.
                    if (sb.Length > lineStart && word.Value.Length > 0 &&
                        !char.IsWhiteSpace(sb[^1]) && !char.IsWhiteSpace(word.Value[0]))
                    {
                        sb.Append(' ');
                    }

                    sb.Append(word.Value);
                }

                while (sb.Length > lineStart && char.IsWhiteSpace(sb[^1]))
                {
                    sb.Length--;
                }
            }
        }

        return sb.ToString();
    }

    private async Task<byte[]?> RenderPngAsync(int pageNumber, CancellationToken token)
    {
        using var picture = await service.GetRenderPageAsync(pageNumber, token).ConfigureAwait(false);
        if (picture is null)
        {
            return null;
        }

        SKRect bounds = picture.Item.CullRect;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        float scale = ImageLongSide / Math.Max(bounds.Width, bounds.Height);
        int width = Math.Max(1, (int)MathF.Round(bounds.Width * scale));
        int height = Math.Max(1, (int)MathF.Round(bounds.Height * scale));

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        canvas.Scale(scale);
        canvas.Translate(-bounds.Left, -bounds.Top);
        canvas.DrawPicture(picture.Item);

        using SKImage image = surface.Snapshot();
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);

        using var fs = new FileStream("page.png", FileMode.Create, FileAccess.Write);
        fs.Write(data.AsSpan());

        return data.ToArray();
    }
}
