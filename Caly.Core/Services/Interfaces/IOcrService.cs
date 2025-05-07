using Caly.Pdf.Models;
using SkiaSharp;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Caly.Core.Services.Interfaces
{
    internal interface IOcrService
    {
        PdfLetter[] GetWords(SKPicture page, SKRect? area);
    }
}
