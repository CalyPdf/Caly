﻿// Copyright (c) 2025 BobLd
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
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Caly.Core.ViewModels;

namespace Caly.Core.Services.Interfaces
{
    public interface IPdfService : IAsyncDisposable, IDisposable
    {
        int NumberOfPages { get; }

        string? FileName { get; }

        long? FileSize { get; }

        string? LocalPath { get; }

        /// <summary>
        /// Open the pdf document.
        /// </summary>
        /// <returns>The number of pages in the opened document. <c>0</c> if the document was not opened.</returns>
        Task<int> OpenDocument(IStorageFile? storageFile, string? password, CancellationToken token);

        ValueTask SetDocumentPropertiesAsync(PdfDocumentViewModel page, CancellationToken token);
        
        Task SetPdfBookmark(PdfDocumentViewModel pdfDocument, CancellationToken token);

        Task BuildIndex(PdfDocumentViewModel pdfDocument, IProgress<int> progress, CancellationToken token);

        Task<IEnumerable<TextSearchResultViewModel>> SearchText(PdfDocumentViewModel pdfDocument, string query, CancellationToken token);


        Task SetPageSizeAsync(PdfPageViewModel page, CancellationToken token);

        void AskPagePicture(PdfPageViewModel page, CancellationToken token);

        void AskRemovePagePicture(PdfPageViewModel page);

        
        void AskPageThumbnail(PdfPageViewModel page, CancellationToken token);

        void AskRemoveThumbnail(PdfPageViewModel page);

        void ClearAllThumbnail();

        
        void AskPageTextLayer(PdfPageViewModel page, CancellationToken token);

        void AskRemovePageTextLayer(PdfPageViewModel page);

        Task SetPageTextLayer(PdfPageViewModel page, CancellationToken token);
    }
}
