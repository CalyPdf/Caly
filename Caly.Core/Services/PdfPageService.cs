using Caly.Core.Services.Interfaces;
using System;

namespace Caly.Core.Services
{
    public sealed class PdfPageService : IDisposable
    {
        private readonly IPdfDocumentService _pdfDocumentService;

        public PdfPageService(IPdfDocumentService pdfDocumentService)
        {
            _pdfDocumentService = pdfDocumentService;
        }
        
        public void Dispose()
        {

        }
    }
}
