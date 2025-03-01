// Copyright (C) 2024 BobLd
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY - without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Caly.Core.ViewModels;
using Microsoft.Extensions.Logging;

namespace Caly.Core.Services
{
    internal sealed partial class PdfPigPdfService
    {
        private enum RenderRequestTypes : byte
        {
            Picture = 0,
            Thumbnail = 1,
            Information = 2,
            TextLayer = 3
        }

        private sealed record RenderRequest(PdfPageViewModel Page, RenderRequestTypes Type, CancellationToken Token)
        {
        }

        private readonly BlockingCollection<RenderRequest> _pendingHighPriorityRequests = new(new ConcurrentStack<RenderRequest>());
        private readonly BlockingCollection<RenderRequest> _pendingOtherRequests = new(new ConcurrentStack<RenderRequest>());
        private readonly BlockingCollection<RenderRequest>[] _priorityRequests;
        private readonly ChannelWriter<RenderRequest> _requestsWriter;
        private readonly ChannelReader<RenderRequest> _requestsReader;

        private readonly CancellationTokenSource _mainCts = new();

        private readonly Task _enqueuingLoopTask;
        private readonly Task _processingLoopTask;

        #region Loops
        private async Task EnqueuingLoop()
        {
            Debug.ThrowOnUiThread();

            // https://learn.microsoft.com/en-us/dotnet/standard/collections/thread-safe/blockingcollection-overview
            while (!_pendingHighPriorityRequests.IsCompleted && !_pendingOtherRequests.IsCompleted)
            {
                // Blocks if dataItems.Count == 0.
                // IOE means that Take() was called on a completed collection.
                // Some other thread can call CompleteAdding after we pass the
                // IsCompleted check but before we call Take.
                // In this example, we can simply catch the exception since the
                // loop will break on the next iteration.
                try
                {
                    if (IsDisposed())
                    {
                        return;
                    }

                    _ = BlockingCollection<RenderRequest>.TakeFromAny(_priorityRequests, out RenderRequest? renderRequest, _mainCts.Token);

                    if (renderRequest is null)
                    {
                        continue;
                    }

                    await _requestsWriter.WriteAsync(renderRequest, _mainCts.Token);
                }
                catch (OperationCanceledException) { }
                catch (InvalidOperationException) { }
            }

            // TODO - What happens if one queue is complete, and the other is not

            //while (!_pendingHighPriorityRequests.IsCompleted || !_pendingOtherRequests.IsCompleted)
            //{

            //}
        }

        private async Task ProcessingLoop()
        {
            Debug.ThrowOnUiThread();

            try
            {
                var asyncFeed = _requestsReader.ReadAllAsync(_mainCts.Token);

                await Parallel.ForEachAsync(asyncFeed, _mainCts.Token, async (r, c) =>
                {
                    try
                    {
                        switch (r.Type)
                        {
                            case RenderRequestTypes.Picture:
                                await ProcessPictureRequest(r);
                                break;

                            case RenderRequestTypes.Thumbnail:
                                await ProcessThumbnailRequest(r);
                                break;

                            case RenderRequestTypes.TextLayer:
                                await ProcessTextLayerRequest(r);
                                break;

                            default:
                                throw new NotImplementedException(r.Type.ToString());
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("CANCELED: Page {pageNumber}, type {type}.", r.Page.PageNumber, r.Type);
                    }
                    catch (Exception e)
                    {
                        // We just ignore for the moment
                        Debug.WriteExceptionToFile(e);
                        _logger.LogError(e, "Page {pageNumber}, type {type}.", r.Page.PageNumber, r.Type);
                    }
                });
            }
            catch (OperationCanceledException) { }
        }
        #endregion

        #region Picture
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _pictureTokens = new();

        private async Task ProcessPictureRequest(RenderRequest renderRequest)
        {
            _logger.LogInformation("[RENDER] [PICTURE] Start process {pageNumber}.", renderRequest.Page.PageNumber);

            try
            {
                renderRequest.Token.ThrowIfCancellationRequested();

                if (IsDisposed())
                {
                    return;
                }

                if (renderRequest.Page.PdfPicture is not null)
                {
                    _logger.LogInformation("[RENDER] [PICTURE] No need process {pageNumber}.", renderRequest.Page.PageNumber);
                    return;
                }

                var picture = await GetRenderPageAsync(renderRequest.Page.PageNumber, renderRequest.Token);

                renderRequest.Page.PdfPicture = picture;

                if (renderRequest.Page.PdfPicture?.Item is not null)
                {
                    renderRequest.Page.Width = renderRequest.Page.PdfPicture.Item.CullRect.Width;
                    renderRequest.Page.Height = renderRequest.Page.PdfPicture.Item.CullRect.Height;
                }
            }
            finally
            {
                if (_pictureTokens.TryRemove(renderRequest.Page.PageNumber, out var cts))
                {
                    cts.Dispose();
                }
            }

            _logger.LogInformation("[RENDER] [PICTURE] End process {pageNumber}.", renderRequest.Page.PageNumber);
        }

        public void AskPagePicture(PdfPageViewModel page, CancellationToken token)
        {
            _logger.LogInformation("[RENDER] AskPagePicture {pageNumber}.", page.PageNumber);
            if (IsDisposed())
            {
                return;
            }

            var pageCts = CancellationTokenSource.CreateLinkedTokenSource(token, _mainCts.Token);

            if (_pictureTokens.TryAdd(page.PageNumber, pageCts))
            {
                _pendingHighPriorityRequests.Add(new RenderRequest(page, RenderRequestTypes.Picture, pageCts.Token), CancellationToken.None);
            }
        }

        public void AskRemovePagePicture(PdfPageViewModel page)
        {
            _logger.LogInformation("[RENDER] AskRemovePagePicture {pageNumber}.", page.PageNumber);

            var picture = page.PdfPicture;

            page.PdfPicture = null;
            if (_pictureTokens.TryRemove(page.PageNumber, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            picture?.Dispose();

            System.Diagnostics.Debug.Assert((picture?.RefCount ?? 0) == 0);
        }
        #endregion

        #region Text layer
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _textLayerTokens = new();

        private async Task ProcessTextLayerRequest(RenderRequest renderRequest)
        {
            _logger.LogInformation("[RENDER] [TEXT] Start process {pageNumber}.", renderRequest.Page.PageNumber);

            try
            {
                renderRequest.Token.ThrowIfCancellationRequested();

                if (IsDisposed())
                {
                    return;
                }

                if (renderRequest.Page.PdfTextLayer is not null)
                {
                    _logger.LogInformation("[RENDER] [TEXT] No need process {pageNumber}.", renderRequest.Page.PageNumber);
                    return;
                }

                await SetPageTextLayer(renderRequest.Page, renderRequest.Token);
            }
            finally
            {
                if (_textLayerTokens.TryRemove(renderRequest.Page.PageNumber, out var cts))
                {
                    cts.Dispose();
                }
            }

            _logger.LogInformation("[RENDER] [TEXT] End process {pageNumber}.", renderRequest.Page.PageNumber);
        }
        
        public void AskPageTextLayer(PdfPageViewModel page, CancellationToken token)
        {
            _logger.LogInformation("[RENDER] AskPageTextLayer {pageNumber}.", page.PageNumber);

            if (IsDisposed())
            {
                return;
            }

            var pageCts = CancellationTokenSource.CreateLinkedTokenSource(token, _mainCts.Token);

            if (_textLayerTokens.TryAdd(page.PageNumber, pageCts))
            {
                _pendingOtherRequests.Add(new RenderRequest(page, RenderRequestTypes.TextLayer, pageCts.Token), CancellationToken.None);
            }
        }

        public void AskRemovePageTextLayer(PdfPageViewModel page)
        {
            _logger.LogInformation("[RENDER] AskRemovePageTextLayer {pageNumber}.", page.PageNumber);

            if (_textLayerTokens.TryRemove(page.PageNumber, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }
        #endregion

        #region Thumbnail
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _thumbnailTokens = new();

        private async Task ProcessThumbnailRequest(RenderRequest renderRequest)
        {
            _logger.LogInformation("[RENDER] [THUMBNAIL] Start process {pageNumber}.", renderRequest.Page.PageNumber);

            try
            {
                renderRequest.Token.ThrowIfCancellationRequested();

                if (IsDisposed())
                {
                    return;
                }

                if (renderRequest.Page.Thumbnail is not null)
                {
                    _logger.LogInformation("[RENDER] [THUMBNAIL] No need process {pageNumber}.", renderRequest.Page.PageNumber);
                    return;
                }

                var picture = renderRequest.Page.PdfPicture?.Clone();
                if (picture is not null)
                {
                    await SetThumbnail(renderRequest.Page, picture.Item, renderRequest.Token);
                    picture.Dispose();
                    return;
                }

                // Need to get picture first
                using (picture = await GetRenderPageAsync(renderRequest.Page.PageNumber, renderRequest.Token))
                {
                    if (picture is not null)
                    {
                        // This is the first we load the page, width and height are not set yet
                        renderRequest.Page.Width = picture.Item.CullRect.Width;
                        renderRequest.Page.Height = picture.Item.CullRect.Height;

                        await SetThumbnail(renderRequest.Page, picture.Item, renderRequest.Token);
                    }
                }
            }
            finally
            {
                if (_thumbnailTokens.TryRemove(renderRequest.Page.PageNumber, out var cts))
                {
                    cts.Dispose();
                }
            }

            _logger.LogInformation("[RENDER] [THUMBNAIL] End process {pageNumber}.", renderRequest.Page.PageNumber);
        }

        public void AskPageThumbnail(PdfPageViewModel page, CancellationToken token)
        {
            _logger.LogInformation("[RENDER] AskPageThumbnail {pageNumber}.", page.PageNumber);

            if (IsDisposed())
            {
                return;
            }

            var pageCts = CancellationTokenSource.CreateLinkedTokenSource(token, _mainCts.Token);
            
            //pageCts.Cancel();

            if (_thumbnailTokens.TryAdd(page.PageNumber, pageCts))
            {
                _pendingOtherRequests.Add(new RenderRequest(page, RenderRequestTypes.Thumbnail, pageCts.Token), CancellationToken.None);
            }

            _logger.LogInformation("[RENDER] Thumbnail Count {count}.", _bitmaps.Count);
        }

        public void AskRemoveThumbnail(PdfPageViewModel page)
        {
            _logger.LogInformation("[RENDER] AskRemoveThumbnail {pageNumber}.", page.PageNumber);

            var thumbnail = page.Thumbnail;
            page.Thumbnail = null;

            if (_thumbnailTokens.TryRemove(page.PageNumber, out var cts))
            {
                _logger.LogInformation("[RENDER] AskRemoveThumbnail removed {pageNumber}.", page.PageNumber);
                cts.Cancel();
                cts.Dispose();
            }

            if (_bitmaps.TryRemove(page.PageNumber, out var vm))
            {
                // Should always be null
                //System.Diagnostics.Debug.Assert(vm.Thumbnail is null);
            }

            thumbnail?.Dispose();

            _logger.LogInformation("[RENDER] Thumbnail Count {count}.", _bitmaps.Count);
        }
        #endregion
    }
}
