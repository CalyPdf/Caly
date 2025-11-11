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

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Services;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using Caly.Pdf.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Caly.Core.ViewModels
{
    [DebuggerDisplay("[{PdfService?.FileName}] Page {_pageNumber}")]
    public sealed partial class PdfPageViewModel : ViewModelBase, IAsyncDisposable
    {
        public override string ToString()
        {
            return $"[{PdfService.FileName}] Page {PageNumber}";
        }

        internal readonly IPdfService PdfService;
        
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

        public IPageInteractiveLayerHandler PageInteractiveLayerHandler => PdfService.PageInteractiveLayerHandler!;

        public bool IsPageVisible => VisibleArea.HasValue;

        public int ThumbnailWidth => Math.Max(1, (int)(Width / Height * ThumbnailHeight));

        public int ThumbnailHeight => 135;

        public double DisplayWidth => IsPortrait ? Width : Height;

        public double DisplayHeight => IsPortrait ? Height : Width;

        public bool IsPageRendering => PdfPicture?.Item is null; // TODO - refactor might not be optimal

        public bool IsThumbnailRendering => Thumbnail is null;

        public bool IsPortrait => Rotation == 0 || Rotation == 180;

        private long _isSizeSet;
        public bool IsSizeSet()
        {
            return Interlocked.Read(ref _isSizeSet) == 1;
        }

        public void SetSizeSet()
        {
            Interlocked.Exchange(ref _isSizeSet, 1);
        }

#if DEBUG
        /// <summary>
        /// Design mode constructor.
        /// </summary>
        public PdfPageViewModel()
        {
            if (!Avalonia.Controls.Design.IsDesignMode)
            {
                throw new InvalidOperationException($"{typeof(PdfPageViewModel)} empty constructor should only be called in design mode");
            }

            PdfService = null!;
        }
#endif

        public PdfPageViewModel(int pageNumber, IPdfService pdfService)
        {
            ArgumentNullException.ThrowIfNull(pdfService?.PageInteractiveLayerHandler, nameof(pdfService.PageInteractiveLayerHandler));
            PageNumber = pageNumber;
            PdfService = pdfService;
        }

        public async Task LoadPageSizeImmediate(CancellationToken cancellationToken)
        {
            await PdfService.SetPageSizeAsync(this, cancellationToken);
        }

        public async Task SetPageTextLayerImmediate(CancellationToken token)
        {
            await PdfService.SetPageTextLayerAsync(this, token);
        }

        public void RemovePageTextLayerImmediate()
        {
            Dispatcher.UIThread.Invoke(() => PdfTextLayer = null);
        }

        public void FlagInteractiveLayerChanged()
        {
            Debug.ThrowNotOnUiThread();
            SelectionChangedFlag = !SelectionChangedFlag;
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
            App.Messenger.Send(new UnloadThumbnailMessage(this));
            return ValueTask.CompletedTask;
        }
    }
}
