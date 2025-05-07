using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Caly.Core.Services.Interfaces;
using Caly.Pdf.Models;
using Lifti.Tokenization;
using RapidOcrNet;
using SkiaSharp;
using UglyToad.PdfPig.Core;

namespace Caly.Core.Services
{
    internal class RapidOcrService : IOcrService, IDisposable
    {
        private readonly RapidOcr _ocrEngin;
        public RapidOcrService()
        {
            _ocrEngin = new RapidOcr();
            _ocrEngin.InitModels();
        }
        
        public PdfLetter[] GetWords(SKPicture page, SKRect? area)
        {
            
                using (var surface =
                       SKSurface.Create(new SKImageInfo((int)page.CullRect.Width, (int)page.CullRect.Height)))
                using (var canvas = surface.Canvas)
                {
                    canvas.Clear(SKColors.White);
                    canvas.DrawPicture(page);

                    using (var image = surface.Snapshot())
                    using (var skBitmap = SKBitmap.FromImage(image))
                    {
                        OcrResult ocrResult = _ocrEngin.Detect(skBitmap, RapidOcrOptions.Default);

                        if (ocrResult.TextBlocks.Length == 0)
                        {
                            return [];
                        }
                        
                        var words = new PdfLetter[ocrResult.TextBlocks.Length];

                        for (var i = 0; i < ocrResult.TextBlocks.Length; ++i)
                        {
                            var textBlock = ocrResult.TextBlocks[i];
                            var bbox = textBlock.BoxPoints;

                            words[i] = new PdfLetter(textBlock.GetText().AsMemory(), ToPdfRectangle(bbox), 10, 0);
                        }

                        return words;
                    }
                }

            static PdfRectangle ToPdfRectangle(SKPointI[] bbox)
            {
                return new PdfRectangle(
                    ToPdfPoint(bbox[0]),
                    ToPdfPoint(bbox[1]),
                    ToPdfPoint(bbox[2]),
                    ToPdfPoint(bbox[3]));
            }

            static PdfPoint ToPdfPoint(SKPointI point)
            {
                return new PdfPoint(point.X, point.Y);
            }
        }

        public void Dispose()
        {
            _ocrEngin.Dispose();
        }
    }
}
