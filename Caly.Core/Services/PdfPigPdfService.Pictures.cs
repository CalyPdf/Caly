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
using System.Threading;
using System.Threading.Tasks;
using Caly.Core.Utilities;
using Caly.Pdf.Models;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace Caly.Core.Services
{
    internal sealed partial class PdfPigPdfService
    {
        private async Task<IRef<SKPicture>?> GetRenderPageAsync(int pageNumber, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            SKPicture? pic = await ExecuteWithLockAsync(() =>
                {
                    try
                    {
                        return _document?.GetPage<SKPicture>(pageNumber);
                    }
                    catch (OperationCanceledException)
                    {
                        throw; // No error picture to generate
                    }
                    catch (Exception e)
                    {
                        Debug.WriteExceptionToFile(e);
                        return GetErrorPicture(pageNumber, e, token);
                    }
                },
                token);

            return pic is null ? null : RefCountable.Create(pic);
        }

        private SKPicture? GetErrorPicture(int pageNumber, Exception ex, CancellationToken cancellationToken)
        {
            // Try get page size
            PdfPageInformation info;

            try
            {
                info = _document!.GetPage<PdfPageInformation>(pageNumber);
            }
            catch (Exception e)
            {
                // TODO
                info = new PdfPageInformation()
                {
                    Width = 100,
                    Height = 100,
                    PageNumber = pageNumber
                };
            }

            float width = (float)info.Width;
            float height = (float)info.Height;

            using (var recorder = new SKPictureRecorder())
            using (var canvas = recorder.BeginRecording(SKRect.Create(width, height)))
            {
//#if DEBUG
                float size = 9;
                using (var drawTypeface = SKTypeface.CreateDefault())
                using (var skFont = drawTypeface.ToFont(size))
                using (var fontPaint = new SKPaint(skFont))
                {
                    fontPaint.Color = SKColors.Red;
                    fontPaint.IsAntialias = true;

                    float lineY = size + 1;
                    foreach (var textLine in ex.ToString().Split('\n'))
                    {
                        canvas.DrawShapedText(textLine, new SKPoint(0, lineY), fontPaint);
                        lineY += size;
                    }
                }
//#endif

                canvas.Flush();

                return recorder.EndRecording();
            }

        }
    }
}
