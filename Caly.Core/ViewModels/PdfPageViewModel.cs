﻿// Copyright (C) 2024 BobLd
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
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using Caly.Pdf.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;

namespace Caly.Core.ViewModels
{
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

        [ObservableProperty]
        private ITextSelectionHandler _textSelectionHandler;

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

        public PdfPageViewModel(int pageNumber, IPdfService pdfService, ITextSelectionHandler textSelectionHandler)
        {
            ArgumentNullException.ThrowIfNull(textSelectionHandler, nameof(textSelectionHandler));

            PageNumber = pageNumber;
            _pdfService = pdfService;
            TextSelectionHandler = textSelectionHandler;
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
