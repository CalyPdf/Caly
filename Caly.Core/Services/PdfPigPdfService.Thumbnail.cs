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

using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Caly.Core.ViewModels;
using SkiaSharp;

namespace Caly.Core.Services
{
    internal sealed partial class PdfPigPdfService
    {
        private readonly ConcurrentDictionary<int, PdfPageViewModel> _bitmaps = new();
        
        private async Task SetThumbnail(PdfPageViewModel vm, SKPicture picture, CancellationToken token)
        {
            Debug.ThrowOnUiThread();
            
            if (IsDisposed())
            {
                return;
            }

            token.ThrowIfCancellationRequested();

            int tWidth = (int)(vm.ThumbnailWidth / 1.5);
            int tHeight = (int)(vm.ThumbnailHeight / 1.5);

            if (tWidth <= 1 || tHeight <= 1)
            {
                tWidth = vm.ThumbnailWidth;
                tHeight = vm.ThumbnailHeight;
            }

            SKMatrix scale = SKMatrix.CreateScale(tWidth / (float)vm.Width, tHeight / (float)vm.Height);
            
            using (var surface = SKSurface.Create(new SKImageInfo(tWidth, tHeight)))
            using (var canvas = surface.Canvas)
            {
                token.ThrowIfCancellationRequested();

                bool _isDarkMode = _settingsService.GetSettings().IsDarkModeEnabled;

                if (_isDarkMode)
                {
                    canvas.Clear(SKColors.Black);

                    using (var invertPaint = new SKPaint())
                    {
                        // Invert lightness across whole page 
                        SKHighContrastConfig config = new()
                        {
                            Grayscale = false,
                            InvertStyle = SKHighContrastConfigInvertStyle.InvertLightness,
                            Contrast = 0.0f
                        };

                        invertPaint.ColorFilter = SKColorFilter.CreateHighContrast(config);

                        canvas.DrawPicture(picture, ref scale, invertPaint);
                    }

                    // Image mask is used for drawing unprocessed images - pictures in the PDF that should not be inverted
                    if (vm.ImageMask != null)
                    {

                        var _imageMask = vm.ImageMask.Copy();
                        using (SKCanvas maskResize = new(_imageMask))
                        {
                            maskResize.SetMatrix(scale);
                            maskResize.DrawBitmap(_imageMask, 0, 0);
                        }


                        using (var imagePaint = new SKPaint())
                        {
                            

                            canvas.Save();
                            using (var path = new SKPath())
                            {
                                // This approach is not optimal, but it supports any shape of images, not only rectangles
                                for (int y = 0; y < _imageMask.Height; y++)
                                {
                                    for (int x = 0; x < _imageMask.Width; x++)
                                    {
                                        if (_imageMask.GetPixel(x, y).Red > 127)
                                        {
                                            path.AddRect(new SKRect(x, y, x + 1, y + 1));
                                        }
                                    }
                                }

                                canvas.ClipPath(path);
                                canvas.DrawPicture(picture, ref scale, imagePaint);
                            }
                            canvas.Restore();
                        }

                    }  
                }
                // Original rendering (no dark mode)
                else
                {
                    canvas.Clear(SKColors.White);
                    canvas.DrawPicture(picture, ref scale);
                }

#if DEBUG
                using (var skFont = SKTypeface.Default.ToFont(tHeight / 5f, 1f))
                using (var fontPaint = new SKPaint(skFont))
                {
                    fontPaint.Style = SKPaintStyle.Fill;
                    fontPaint.Color = SKColors.Blue.WithAlpha(150);
                    canvas.DrawText(picture.UniqueId.ToString(), tWidth / 4f, tHeight / 2f, fontPaint);
                }
#endif

                using (var image = surface.Snapshot())
                using (SKData d = image.Encode(SKEncodedImageFormat.Jpeg, 50))
                await using (Stream stream = d.AsStream())
                {
                    var thumbnail = Bitmap.DecodeToWidth(stream, vm.ThumbnailWidth, BitmapInterpolationMode.LowQuality);

                    Dispatcher.UIThread.Invoke(() => vm.Thumbnail = thumbnail);

                    if (!_bitmaps.TryAdd(vm.PageNumber, vm))
                    {

                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[RENDER] Thumbnail Count {_bitmaps.Count}");
        }

        public void ClearAllThumbnail()
        {
            // WARNING / TODO - Currently runs on UI thread
            
            foreach (var p in _bitmaps.Keys.ToArray())
            {
                if (_bitmaps.TryRemove(p, out var vm))
                {
                    var bmp = vm.Thumbnail;
                    vm.Thumbnail = null;
                    bmp?.Dispose();
                }
            }
        }
    }
}
