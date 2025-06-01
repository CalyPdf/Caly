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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using Caly.Pdf.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;

namespace Caly.Core.ViewModels
{
    [DebuggerDisplay("Page {_pageNumber}")]
    public sealed partial class PdfPageViewModel : ViewModelBase, IAsyncDisposable
    {
        private readonly IPdfService _pdfService;

        private CancellationTokenSource? _cts = new();

        [ObservableProperty]
        private PdfTextLayer? _pdfTextLayer;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPageRendering))]
        private IRef<SKPicture>? _pdfPicture;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ThumbnailWidth))]
        [NotifyPropertyChangedFor(nameof(DisplayWidth))]
        [NotifyPropertyChangedFor(nameof(DisplayHeight))]
        private double _width;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ThumbnailWidth))]
        [NotifyPropertyChangedFor(nameof(DisplayWidth))]
        [NotifyPropertyChangedFor(nameof(DisplayHeight))]
        private double _height;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsThumbnailRendering))]
        private Bitmap? _thumbnail;

        [ObservableProperty]
        private int _pageNumber;

        [ObservableProperty]
        private bool _isPagePrepared;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPageVisible))]
        private Rect? _visibleArea;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPortrait))]
        [NotifyPropertyChangedFor(nameof(DisplayWidth))]
        [NotifyPropertyChangedFor(nameof(DisplayHeight))]
        private int _rotation;

        [ObservableProperty]
        private bool _selectionChangedFlag;

        public ITextSelectionHandler TextSelectionHandler => _pdfService.TextSelectionHandler!;

        public bool IsPageVisible => VisibleArea.HasValue;

        public int ThumbnailWidth => Math.Max(1, (int)(Width / Height * ThumbnailHeight));

        public int ThumbnailHeight => 135;

        public double DisplayWidth => IsPortrait ? Width : Height;

        public double DisplayHeight => IsPortrait ? Height : Width;

        public bool IsPageRendering => PdfPicture is null || PdfPicture.Item is null; // TODO - refactor might not be optimal

        public bool IsThumbnailRendering => Thumbnail is null;

        public bool IsPortrait => Rotation == 0 || Rotation == 180;

#if DEBUG
        /// <summary>
        /// Design mode constructor.
        /// </summary>
        public PdfPageViewModel()
        {
            if (Avalonia.Controls.Design.IsDesignMode)
            {
                //_pdfService = DummyPdfPageService.Instance; // TODO
            }
            else
            {
                throw new InvalidOperationException($"{typeof(PdfPageViewModel)} empty constructor should only be called in design mode");
            }
        }
#endif

        public PdfPageViewModel(int pageNumber, IPdfService pdfService)
        {
            ArgumentNullException.ThrowIfNull(pdfService.TextSelectionHandler, nameof(pdfService.TextSelectionHandler));
            PageNumber = pageNumber;
            _pdfService = pdfService;
        }

        public async Task LoadPageSize(CancellationToken cancellationToken)
        {
            await _pdfService.SetPageSizeAsync(this, cancellationToken);
        }

        public void FlagSelectionChanged()
        {
            Debug.ThrowNotOnUiThread();
            SelectionChangedFlag = !SelectionChangedFlag;
        }

        public void LoadPage()
        {
            if (_cts is null)
            {
                return;
            }

            try
            {
                LoadPagePicture();
                LoadInteractiveLayer(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                UnloadPagePicture();
            }
            catch (Exception e)
            {
                UnloadPagePicture();
                Exception = new ExceptionViewModel(e);
            }
        }

        private void LoadPagePicture()
        {
            if (PdfPicture?.Item is not null)
            {
                return;
            }
            _pdfService.AskPagePicture(this, _cts.Token);
        }

        public void UnloadPage()
        {
            UnloadPagePicture();
            CancelLoadInteractiveLayer();
        }

        private void UnloadPagePicture()
        {
            _pdfService.AskRemovePagePicture(this);
        }

        public void LoadThumbnail()
        {
            if (_cts is null)
            {
                return;
            }

            _pdfService.AskPageThumbnail(this, _cts.Token);
        }

        public void UnloadThumbnail()
        {
            _pdfService.AskRemoveThumbnail(this);
        }

        public void LoadInteractiveLayer(CancellationToken cancellationToken)
        {
            _pdfService.AskPageTextLayer(this, cancellationToken);
        }

        private void CancelLoadInteractiveLayer()
        {
            _pdfService.AskRemovePageTextLayer(this);
        }

        public async Task SetPageTextLayer(CancellationToken token)
        {
            await _pdfService.SetPageTextLayer(this, token);
        }

        internal void RotateClockwise()
        {
            Rotation = (Rotation + 90) % 360;
        }

        internal void RotateCounterclockwise()
        {
            Rotation = (Rotation + 270) % 360;
        }

        public ValueTask DisposeAsync()
        {
            UnloadThumbnail();
            UnloadPage();
            _cts?.Dispose();
            _cts = null;
            return ValueTask.CompletedTask;
        }
    }
}
