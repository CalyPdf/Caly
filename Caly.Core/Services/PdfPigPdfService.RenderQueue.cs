using Caly.Core.ViewModels;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Caly.Core.Services
{
    internal partial class PdfPigPdfService
    {
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _pictureTokens = new();
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _textLayerTokens = new();
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _thumbnailTokens = new();

        #region Picture

        public void EnqueueRequestPageSize(PdfPageViewModel page)
        {
            if (IsDisposed() || !IsActive)
            {
                return;
            }

            // No cancel possible
            if (!_requestsWriter.TryWrite(new RenderRequest(page, RenderRequestTypes.PageSize, CancellationToken.None)))
            {
                throw new Exception("Could not write request to channel."); // Should never happen as unbounded channel
            }
        }

        public void EnqueueRequestPicture(PdfPageViewModel page)
        {
            if (IsDisposed() || !IsActive)
            {
                return;
            }

            var pageCts = CancellationTokenSource.CreateLinkedTokenSource(_mainCts.Token);

            if (_pictureTokens.TryAdd(page.PageNumber, pageCts))
            {
                if (!_requestsWriter.TryWrite(new RenderRequest(page, RenderRequestTypes.Picture, pageCts.Token)))
                {
                    throw new Exception("Could not write request to channel."); // Should never happen as unbounded channel
                }
            }
        }

        public void EnqueueRemovePicture(PdfPageViewModel page)
        {
            var picture = page.PdfPicture;

            page.PdfPicture = null;
            if (_pictureTokens.TryRemove(page.PageNumber, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            picture?.Dispose();
        }

        #endregion

        #region Text layer

        public void EnqueueRequestTextLayer(PdfPageViewModel page)
        {
            if (IsDisposed() || !IsActive)
            {
                return;
            }

            var pageCts = CancellationTokenSource.CreateLinkedTokenSource(_mainCts.Token);

            if (_textLayerTokens.TryAdd(page.PageNumber, pageCts))
            {
                if (!_requestsWriter.TryWrite(new RenderRequest(page, RenderRequestTypes.TextLayer, pageCts.Token)))
                {
                    throw new Exception("Could not write request to channel."); // Should never happen as unbounded channel
                }
            }
        }

        public void EnqueueRemoveTextLayer(PdfPageViewModel page)
        {
            if (_textLayerTokens.TryRemove(page.PageNumber, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            page.RemovePageTextLayerImmediate();
        }

        #endregion

        #region Thumbnail

        public void EnqueueRequestThumbnail(PdfPageViewModel page)
        {
            if (IsDisposed() || !IsActive)
            {
                return;
            }

            var pageCts = CancellationTokenSource.CreateLinkedTokenSource(_mainCts.Token);

            if (_thumbnailTokens.TryAdd(page.PageNumber, pageCts))
            {
                if (!_requestsWriter.TryWrite(new RenderRequest(page, RenderRequestTypes.Thumbnail, pageCts.Token)))
                {
                    throw new Exception("Could not write request to channel."); // Should never happen as unbounded channel
                }
            }
        }

        public void EnqueueRemoveThumbnail(PdfPageViewModel page)
        {
            var thumbnail = page.Thumbnail;
            page.Thumbnail = null;

            if (_thumbnailTokens.TryRemove(page.PageNumber, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            if (_bitmaps.TryRemove(page.PageNumber, out _))
            {
                // Should always be null
                //System.Diagnostics.Debug.Assert(vm.Thumbnail is null);
            }

            thumbnail?.Dispose();
        }

        #endregion
    }
}
