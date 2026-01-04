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
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Caly.Core.Handlers;
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Models;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using Caly.Pdf;
using Caly.Pdf.Models;
using Caly.Pdf.PageFactories;
using CommunityToolkit.Mvvm.Messaging;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;
using UglyToad.PdfPig.Outline;
using UglyToad.PdfPig.Rendering.Skia;
using UglyToad.PdfPig.Tokens;

namespace Caly.Core.Services
{
    /// <summary>
    /// One instance per document.
    /// </summary>
    internal sealed partial class PdfPigPdfService : IPdfService
    {
        private const string PdfVersionFormat = "0.0";
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss zzz";
        private const string PdfExtension = ".pdf";

        private readonly ITextSearchService _textSearchService;
        
        private Stream? _fileStream;
        private PdfDocument? _document;
        private Uri? _filePath;

        public string? LocalPath => _filePath?.LocalPath;

        public string? FileName => Path.GetFileNameWithoutExtension(LocalPath);

        public long? FileSize => _fileStream?.Length;

        public int NumberOfPages { get; private set; }

        public bool IsPasswordProtected { get; private set; } = false;

        public IPageInteractiveLayerHandler? PageInteractiveLayerHandler { get; private set; }

        private long _isActive = 0;
        public bool IsActive
        {
            // https://makolyte.com/csharp-thread-safe-primitive-properties-using-lock-vs-interlocked/
            get => Interlocked.Read(ref _isActive) == 1;
            set => Interlocked.Exchange(ref _isActive, Convert.ToInt64(value));
        }

        /// <summary>
        /// Gets the Pixel Per Inch (PPI) scaling factor used to convert measurements from PDF points (72 PPI is the default) to application pixels.
        /// </summary>
        /// <remarks>
        /// The application PPI is currently set to 144. We should make that configurable.
        /// </remarks>
        public double PpiScale => 144.0 / 72.0; // 72 should be document dependant, i.e. use PdfPig's UserSpaceUnit.

#if DEBUG
        public PdfPigPdfService()
        {
            if (!Design.IsDesignMode)
            {
                throw new InvalidOperationException("Should only be called in Design mode.");
            }

            _textSearchService = null!;
            _requestsWriter = null!;
            _requestsReader = null!;
            _processingLoopTask = Task.CompletedTask;
        }
#endif

        public PdfPigPdfService(ITextSearchService textSearchService)
        {
            _textSearchService = textSearchService;

            var channel = Channel.CreateUnboundedPrioritized(new UnboundedPrioritizedChannelOptions<RenderRequest>()
            {
                 Comparer = RenderRequestComparer.Instance,
                 SingleWriter = false,
                 SingleReader = false
            });
            
            _requestsWriter = channel.Writer;
            _requestsReader = channel.Reader;

            _processingLoopTask = Task.Run(ProcessingLoop, _mainCts.Token);
        }

        public async Task<int> OpenDocument(IStorageFile? storageFile, string? password, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            // TODO - Ensure method is called only once (one instance per document)

            return await GuardDispose(async ct =>
            {
                try
                {
                    if (storageFile is null)
                    {
                        return 0; // no pdf loaded
                    }

                    if (!PdfExtension.Equals(Path.GetExtension(storageFile.Path.LocalPath), StringComparison.OrdinalIgnoreCase) && !CalyExtensions.IsMobilePlatform())
                    {
                        // TODO - Need to handle Mobile
                        throw new ArgumentOutOfRangeException(
                            $"The loaded file '{Path.GetFileName(storageFile.Path.LocalPath)}' is not a pdf document.");
                    }

                    _filePath = storageFile.Path;
                    System.Diagnostics.Debug.WriteLine($"[INFO] Opening {FileName}...");

                    _fileStream = await storageFile.OpenReadAsync();
                    if (!_fileStream.CanSeek)
                    {
                        var ms = new MemoryStream((int)_fileStream.Length);
                        await _fileStream.CopyToAsync(ms, ct);
                        ms.Position = 0;
                        await _fileStream.DisposeAsync();
                        _fileStream = ms;
                    }

                    return await Task.Run(() =>
                    {
                        var pdfParsingOptions = new ParsingOptions()
                        {
                            SkipMissingFonts = true,
                            FilterProvider = SkiaRenderingFilterProvider.Instance,
                            Logger = CalyPdfPigLogger.Instance
                        };

                        if (!string.IsNullOrEmpty(password))
                        {
                            pdfParsingOptions.Password = password;
                        }

                        _document = PdfDocument.Open(_fileStream, pdfParsingOptions);

                        // We store the PPI as an indirect object so that it can be accessed in the TextLayerFactory.
                        // This is very hacky but PdfPig does not provide a better way to pass such information
                        // to the PageFactory for the moment.
                        // TODO - to remove.
                        _document.Advanced.ReplaceIndirectObject(CalyPdfHelper.FakePpiReference, new NumericToken(PpiScale));

                        _document.AddPageFactory<PdfPageInformation, PageInformationFactory>();
                        _document.AddPageFactory<SKPicture, SkiaPageFactory>();
                        _document.AddPageFactory<PageTextLayerContent, TextLayerFactory>();

                        NumberOfPages = _document.NumberOfPages;
                        PageInteractiveLayerHandler = new PageInteractiveLayerHandler(NumberOfPages);
                        return NumberOfPages;
                    }, ct);
                }
                catch (PdfDocumentEncryptedException)
                {
                    IsPasswordProtected = true;

                    if (!string.IsNullOrEmpty(password))
                    {
                        // Only stay at first level, do not recurse: If password is NOT null, this is recursion
                        return 0;
                    }

                    bool shouldContinue = true;
                    while (shouldContinue)
                    {
                        string? pw = await App.Messenger.Send(new ShowPdfPasswordDialogRequestMessage());
                        Debug.ThrowOnUiThread();

                        shouldContinue = !string.IsNullOrEmpty(pw);
                        if (!shouldContinue)
                        {
                            continue;
                        }

                        var pageCount = await OpenDocument(storageFile, pw, ct);
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
                finally
                {
                    // Only release on first pass
                    if (string.IsNullOrEmpty(password))
                    {
                        // The _semaphore starts with initial count set to 0 and maxCount to 1.
                        // By releasing here we allow _semaphore.Wait() in other methods.
                        _semaphore.Release();
                    }
                }
            }, token);
        }

        public async Task SetPageSizeAsync(PageViewModel page, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            await GuardDispose(async ct =>
            {
                if (page.IsSizeSet())
                {
                    return;
                }

                PdfPageInformation? pageInfo = await ExecuteWithLockAsync(
                    _ => _document?.GetPage<PdfPageInformation>(page.PageNumber),
                    ct);

                if (pageInfo.HasValue && !ct.IsCancellationRequested)
                {
                    page.Width = pageInfo.Value.Width * PpiScale;
                    page.Height = pageInfo.Value.Height * PpiScale;
                    page.SetSizeSet();
                }
            }, token);
        }

        public async Task SetPageTextLayerAsync(PageViewModel page, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            await GuardDispose(async ct =>
            {
                if (page.PdfTextLayer is null)
                {
                    var pageTextLayer = await ExecuteWithLockAsync(
                            _ => _document?.GetPage<PageTextLayerContent>(page.PageNumber),
                            ct)
                        .ConfigureAwait(false);

                    if (pageTextLayer is null)
                    {
                        return;
                    }

                    var textLayer = PdfTextLayerHelper.GetTextLayer(pageTextLayer, ct);

                    // This need to be done sync for SetPageTextLayerImmediate()
                    Dispatcher.UIThread.Invoke(() => page.PdfTextLayer = textLayer, DispatcherPriority.Send, ct);
                }

                if (page.PdfTextLayer is not null && !ct.IsCancellationRequested)
                {
                    // We ensure the correct selection is set now that we have the text layer
                    page.PageInteractiveLayerHandler.UpdateInteractiveLayer(page);
                }
            }, token);
        }

        public async Task SetDocumentPropertiesAsync(DocumentViewModel document, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            await GuardDispose(async ct =>
            {
                await Task.Yield();
                if (document.Properties is not null)
                {
                    return;
                }

                var info = _document?.Information;

                var others =
                    _document?.Information.DocumentInformationDictionary?.Data?
                        .Where(x => x.Value is not null)
                        .ToDictionary(x => x.Key,
                            x => x.Value.ToString()!);

                if (info is null || others is null || ct.IsCancellationRequested)
                {
                    return;
                }

                var pdfProperties = new PdfDocumentProperties()
                {
                    PdfVersion = _document?.Version.ToString(PdfVersionFormat) ?? string.Empty,
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

                Dispatcher.UIThread.Invoke(() => document.Properties = pdfProperties, DispatcherPriority.Send, ct);
            }, token);
        }

        public string? GetLogFileName()
        {
            const int length = 15;

            string? v = FileName;
            if (string.IsNullOrEmpty(v))
            {
                return v;
            }

            if (v.Length == length)
            {
                return v;
            }

            if (v.Length > length)
            {
                return v[..length];
            }

            return v + string.Concat(Enumerable.Repeat(" ", length - v.Length));
        }

        private static string? FormatPdfDate(string? rawDate)
        {
            if (string.IsNullOrEmpty(rawDate))
            {
                return rawDate;
            }

            if (rawDate.StartsWith("D:"))
            {
                rawDate = rawDate[2..];
            }

            if (UglyToad.PdfPig.Util.DateFormatHelper.TryParseDateTimeOffset(rawDate, out DateTimeOffset offset))
            {
                return offset.ToString(DateTimeFormat);
            }

            return rawDate;
        }

        public async Task SetPdfBookmark(DocumentViewModel document, CancellationToken token)
        {
            Debug.ThrowOnUiThread();
            await GuardDispose(async ct =>
            {
                Bookmarks? bookmarks = await ExecuteWithLockAsync(_ =>
                    {
                        if (_document!.TryGetBookmarks(out var b))
                        {
                            return b;
                        }

                        return null;
                    }, ct);

                if (bookmarks is null || bookmarks.Roots.Count == 0 || ct.IsCancellationRequested)
                {
                    return;
                }

                var bookmarksItems = new ObservableCollection<PdfBookmarkNode>();
                foreach (BookmarkNode node in bookmarks.Roots)
                {
                    var n = BuildPdfBookmarkNode(node, ct);
                    if (n is not null)
                    {
                        bookmarksItems.Add(n);
                    }
                }

                Dispatcher.UIThread.Invoke(() => document.Bookmarks = bookmarksItems, DispatcherPriority.Send, ct);
            }, token);
        }

        public async Task BuildIndex(DocumentViewModel document, IProgress<int> progress, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            await GuardDispose(async ct =>
            {
                await _textSearchService.BuildPdfDocumentIndex(document, progress, ct);
            }, token);
        }

        public IEnumerable<TextSearchResultViewModel> SearchText(DocumentViewModel document, string query, IReadOnlyCollection<int> pagesToSkip, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            return GuardDispose(ct => _textSearchService.Search(document, query, pagesToSkip, ct), token) ?? [];
        }

        private static PdfBookmarkNode? BuildPdfBookmarkNode(BookmarkNode node, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

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
                var n = BuildPdfBookmarkNode(child, token);
                if (n is not null)
                {
                    children.Add(n);
                }
            }

            return new PdfBookmarkNode(node.Title, pageNumber, children.Count == 0 ? null : children);
        }
        
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

                // Wait for in-flight operations (with timeout)
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    while (_activeOperations > 0 && !cts.Token.IsCancellationRequested)
                    {
                        System.Diagnostics.Debug.WriteLine($"DisposeAsync: '{FileName}' waiting for {_activeOperations} active operations to finish.");
                        await Task.Delay(50, CancellationToken.None);
                    }
                }

                AssertTokensCancelled(_thumbnailTokens);
                AssertTokensCancelled(_textLayerTokens);
                AssertTokensCancelled(_pictureTokens);

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
            }
            catch (Exception ex)
            {
                Debug.WriteExceptionToFile(ex);
                System.Diagnostics.Debug.WriteLine($"[INFO] ERROR DisposeAsync for {FileName}: {ex.Message}");
            }
        }
    }
}
