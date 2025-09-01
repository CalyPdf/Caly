using Avalonia.Controls;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Caly.Core.Utilities
{
    internal class DarkModeRender
    {

        public static SKCanvas GenerateDarkModePage(SKCanvas canvas, SKPicture picture, SKPath imageMask, SKFilterQuality filterQuality = SKFilterQuality.None, SKMatrix scale = default)
        {
            canvas.Clear(SKColors.Black);
            using (var invertPaint = new SKPaint())
            {
                invertPaint.FilterQuality = filterQuality;

                // Invert lightness across whole page 
                SKHighContrastConfig config = new()
                {
                    Grayscale = false,
                    InvertStyle = SKHighContrastConfigInvertStyle.InvertLightness,
                    Contrast = 0.0f
                };

                invertPaint.ColorFilter = SKColorFilter.CreateHighContrast(config);

                if(scale == default)
                {
                    canvas.DrawPicture(picture, invertPaint);
                }
                canvas.DrawPicture(picture, ref scale, invertPaint);
            }

            // Image mask is used for drawing unprocessed images - pictures in the PDF that should not be inverted
            if (imageMask != null)
            {
                if (scale != default)
                {
                    imageMask.Transform(scale);
                }

                using (var imagePaint = new SKPaint())
                {
                    imagePaint.FilterQuality = filterQuality;

                    canvas.Save();

                    canvas.ClipPath(imageMask);

                    if (scale == default)
                    {
                        canvas.DrawPicture(picture, imagePaint);
                    }
                    canvas.DrawPicture(picture, ref scale, imagePaint);

                    canvas.Restore();
                }
            }
            return canvas;
        }
    }
}
