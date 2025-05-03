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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Caly.Core.Models;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using Caly.Pdf;
using Caly.Pdf.Models;
using Caly.Pdf.PageFactories;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;
using UglyToad.PdfPig.Outline;
using UglyToad.PdfPig.Rendering.Skia;

namespace Caly.Core.Services
{
    /// <summary>
    /// One instance per document.
    /// </summary>
    internal sealed partial class PdfPigPdfService : IPdfService
    {
        private const string PdfVersionFormat = "0.0";
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss zzz";

        private readonly IDialogService _dialogService;
        private readonly ITextSearchService _textSearchService;
        
        private MemoryStream? _fileStream;
        private PdfDocument? _document;
        private Uri? _filePath;

        public string? LocalPath => _filePath?.LocalPath;

        public string? FileName => Path.GetFileNameWithoutExtension(LocalPath);

        public long? FileSize => _fileStream?.Length;

        public int NumberOfPages { get; private set; }

        public PdfPigPdfService(IDialogService dialogService, ITextSearchService textSearchService)
        {
            _dialogService = dialogService ?? throw new NullReferenceException("Missing Dialog Service instance.");
            _textSearchService = textSearchService;

            // Priority to rendering page
            _priorityRequests = [_pendingHighPriorityRequests, _pendingOtherRequests];

            var channel = Channel.CreateUnbounded<RenderRequest>();
            _requestsWriter = channel.Writer;
            _requestsReader = channel.Reader;

            _processingLoopTask = Task.Run(ProcessingLoop, _mainCts.Token);
            _enqueuingLoopTask = Task.Run(EnqueuingLoop, _mainCts.Token);
        }
        
        public async Task<int> OpenDocument(IStorageFile? storageFile, string? password, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            // TODO - Ensure method is called only once (one instance per document)

            try
            {
                if (storageFile is null)
                {
                    return 0; // no pdf loaded
                }

                if (Path.GetExtension(storageFile.Path.LocalPath) != ".pdf" && !CalyExtensions.IsMobilePlatform())
                {
                    // TODO - Need to handle Mobile
                    throw new ArgumentOutOfRangeException($"The loaded file '{Path.GetFileName(storageFile.Path.LocalPath)}' is not a pdf document.");
                }

                _filePath = storageFile.Path;
                System.Diagnostics.Debug.WriteLine($"[INFO] Opening {FileName}...");

                _fileStream = new MemoryStream();
                await using (var fs = await storageFile.OpenReadAsync())
                {
                    await fs.CopyToAsync(_fileStream, token);
                    _fileStream.Position = 0;
                }
                
                return await Task.Run(() =>
                {
                    var pdfParsingOptions = new ParsingOptions()
                    {
                        SkipMissingFonts = true,
                        FilterProvider = SkiaRenderingFilterProvider.Instance,
                        Logger = new CalyPdfPigLogger(_dialogService)
                    };

                    if (!string.IsNullOrEmpty(password))
                    {
                        pdfParsingOptions.Password = password;
                    }

                    _document = PdfDocument.Open(_fileStream, pdfParsingOptions);
                    _document.AddPageFactory<PdfPageInformation, PageInformationFactory>();
                    _document.AddPageFactory<IAsyncEnumerable<SKPicture>, SkiaPageFactory>();
                    _document.AddPageFactory<PageTextLayerContent, TextLayerFactory>();

                    NumberOfPages = _document.NumberOfPages;
                    return NumberOfPages;
                }, token);
            }
            catch (PdfDocumentEncryptedException)
            {
                if (!string.IsNullOrEmpty(password))
                {
                    // Only stay at first level, do not recurse: If password is NOT null, this is recursion
                    return 0;
                }

                bool shouldContinue = true;
                while (shouldContinue)
                {
                    string? pw = await _dialogService.ShowPdfPasswordDialogAsync();
                    Debug.ThrowOnUiThread();

                    shouldContinue = !string.IsNullOrEmpty(pw);
                    if (!shouldContinue)
                    {
                        continue;
                    }

                    var pageCount = await OpenDocument(storageFile, pw, token);
                    if (pageCount > 0)
                    {
                        // Password OK and document opened
                        return pageCount;
                    }
                }

                return 0;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
        }

        public async Task SetPageSizeAsync(PdfPageViewModel pdfPage, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            PdfPageInformation? pageInfo = await ExecuteWithLockAsync(
                () => _document?.GetPage<PdfPageInformation>(pdfPage.PageNumber),
                token);

            if (pageInfo.HasValue && !token.IsCancellationRequested)
            {
                pdfPage.Width = pageInfo.Value.Width;
                pdfPage.Height = pageInfo.Value.Height;
            }
        }

        public async Task SetPageTextLayer(PdfPageViewModel page, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            if (page.PdfTextLayer is null)
            {
                var pageTextLayer = await ExecuteWithLockAsync(
                    () => _document?.GetPage<PageTextLayerContent>(page.PageNumber),
                    token);

                if (pageTextLayer is null)
                {
                    return;
                }

                page.PdfTextLayer = PdfTextLayerHelper.GetTextLayer(pageTextLayer, token);
            }

            if (page.PdfTextLayer is not null)
            {
                // We ensure the correct selection is set now that we have the text layer
                page.TextSelectionHandler.Selection.SelectWordsInRange(page);
            }
        }

        public ValueTask SetDocumentPropertiesAsync(PdfDocumentViewModel document, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            if (_document is null || IsDisposed())
            {
                return ValueTask.CompletedTask;
            }

            var info = _document.Information;

            var others =
                _document.Information.DocumentInformationDictionary?.Data?
                    .Where(x => x.Value is not null)
                    .ToDictionary(x => x.Key,
                        x => x.Value.ToString()!);

            document.Properties = new PdfDocumentProperties()
            {
                PdfVersion = _document.Version.ToString(PdfVersionFormat),
                Title = info.Title,
                Author = info.Author,
                CreationDate = FormatPdfDate(info.CreationDate),
                Creator = info.Creator,
                Keywords = info.Keywords,
                ModifiedDate = FormatPdfDate(info.ModifiedDate),
                Producer = info.Producer,
                Subject = info.Subject,
                Others = others
            };

            return ValueTask.CompletedTask;
        }

        private static string? FormatPdfDate(string? rawDate)
        {
            if (string.IsNullOrEmpty(rawDate))
            {
                return rawDate;
            }

            if (rawDate.StartsWith("D:"))
            {
                rawDate = rawDate.Substring(2, rawDate.Length - 2);
            }

            if (UglyToad.PdfPig.Util.DateFormatHelper.TryParseDateTimeOffset(rawDate, out DateTimeOffset offset))
            {
                return offset.ToString(DateTimeFormat);
            }

            return rawDate;
        }

        public async Task SetPdfBookmark(PdfDocumentViewModel pdfDocument, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            Bookmarks? bookmarks = await ExecuteWithLockAsync(() =>
                {
                    if (_document!.TryGetBookmarks(out var b))
                    {
                        return b;
                    }

                    return null;
                },
                token);
            
            try
            {
                if (bookmarks is null || IsDisposed() || bookmarks.Roots.Count == 0)
                {
                    return;
                }

                var children = new ObservableCollection<PdfBookmarkNode>();
                foreach (BookmarkNode node in bookmarks.Roots)
                {
                    var n = BuildPdfBookmarkNode(node, token);
                    if (n is not null)
                    {
                        children.Add(n);
                    }
                }

                pdfDocument.Bookmarks = children;
            }
            catch (OperationCanceledException) { }
        }

        public async Task BuildIndex(PdfDocumentViewModel pdfDocument, IProgress<int> progress, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            await _textSearchService.BuildPdfDocumentIndex(pdfDocument, progress, token);
        }

        public Task<IEnumerable<TextSearchResultViewModel>> SearchText(PdfDocumentViewModel pdfDocument, string query, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            return _textSearchService.Search(pdfDocument, query, token);
        }

        private static PdfBookmarkNode? BuildPdfBookmarkNode(BookmarkNode node, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int? pageNumber = null;
            if (node is DocumentBookmarkNode bookmarkNode)
            {
                pageNumber = bookmarkNode.PageNumber;
            }

            if (node.IsLeaf)
            {
                return new PdfBookmarkNode(node.Title, pageNumber, null);
            }

            var children = new List<PdfBookmarkNode>();
            foreach (var child in node.Children)
            {
                var n = BuildPdfBookmarkNode(child, cancellationToken);
                if (n is not null)
                {
                    children.Add(n);
                }
            }

            return new PdfBookmarkNode(node.Title, pageNumber, children.Count == 0 ? null : children);
        }

        private bool IsDisposed()
        {
            return Interlocked.Read(ref _isDisposed) != 0;
        }

        private long _isDisposed;

        [Conditional("DEBUG")]
        private static void AssertTokensCancelled(ConcurrentDictionary<int, CancellationTokenSource> tokens)
        {
            foreach (var kvp in tokens)
            {
                System.Diagnostics.Debug.Assert(kvp.Value.IsCancellationRequested);
            }
        }

        public async ValueTask DisposeAsync()
        {
            Debug.ThrowOnUiThread();

            try
            {
                if (IsDisposed())
                {
                    System.Diagnostics.Debug.WriteLine($"[WARN] Trying to dispose but already disposed for {FileName}.");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[INFO] Disposing document async for {FileName}.");

                Interlocked.Increment(ref _isDisposed); // Flag as disposed

                await _mainCts.CancelAsync();

                AssertTokensCancelled(_thumbnailTokens);
                AssertTokensCancelled(_textLayerTokens);
                AssertTokensCancelled(_pictureTokens);

                _pendingOtherRequests.CompleteAdding();
                _pendingHighPriorityRequests.CompleteAdding();
                _requestsWriter.Complete();

                _semaphore.Dispose();

                if (_fileStream is not null)
                {
                    await _fileStream.DisposeAsync();
                    _fileStream = null;
                }

                if (_document is not null)
                {
                    _document.Dispose();
                    _document = null;
                }

                await _processingLoopTask;
                await _enqueuingLoopTask;
                
                _pendingOtherRequests.Dispose();
                _pendingHighPriorityRequests.Dispose();

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[INFO] ERROR DisposeAsync for {FileName}: {ex.Message}");
            }
        }

        public async void Dispose()
        {
            await DisposeAsync();
        }
    }
}
