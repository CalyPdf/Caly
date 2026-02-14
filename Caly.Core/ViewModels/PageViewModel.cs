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
using Caly.Core.Events;
using Caly.Core.Models;
using Caly.Core.Utilities;
using Caly.Pdf.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UglyToad.PdfPig.Core;

namespace Caly.Core.ViewModels;

/// <summary>
/// View model that represent a PDF page.
/// </summary>
[DebuggerDisplay("Page {PageNumber}")]
public sealed partial class PageViewModel : ViewModelBase, IAsyncDisposable
{
    public override string ToString()
    {
        return $"Page {PageNumber}";
    }

    [ObservableProperty] private PdfTextLayer? _pdfTextLayer;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsPageRendering))]
    private IRef<SKPicture>? _pdfPicture;

    [ObservableProperty]
    private double _ppiScale;
    
    /// <summary>
    /// Page Width, scale by <see cref="PpiScale"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThumbnailWidth))]
    [NotifyPropertyChangedFor(nameof(DisplayWidth))]
    [NotifyPropertyChangedFor(nameof(DisplayHeight))]
    private double _width;

    /// <summary>
    /// Page Height, scale by <see cref="PpiScale"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThumbnailWidth))]
    [NotifyPropertyChangedFor(nameof(DisplayWidth))]
    [NotifyPropertyChangedFor(nameof(DisplayHeight))]
    private double _height;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsThumbnailRendering))]
    private Bitmap? _thumbnail;

    [ObservableProperty] private int _pageNumber;

    [ObservableProperty] private bool _isPagePrepared;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsPageVisible))]
    private Rect? _visibleArea;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPortrait))]
    [NotifyPropertyChangedFor(nameof(DisplayWidth))]
    [NotifyPropertyChangedFor(nameof(DisplayHeight))]
    private int _rotation;
    
    [ObservableProperty] private IReadOnlyList<PdfRectangle>? _selectedWords;

    private IReadOnlyList<Range>? _searchResultsRanges; // Needed as the text layer might not be available when we set search results
    [ObservableProperty] private IReadOnlyList<PdfRectangle>? _searchResults;

    [ObservableProperty] private bool _isPageRendering;

    public TextSelection TextSelection { get; }

    public bool IsPageVisible => VisibleArea.HasValue;

    public int ThumbnailWidth => Math.Max(1, (int)(Width / Height * ThumbnailHeight));

    public int ThumbnailHeight => 135;

    public double DisplayWidth => IsPortrait ? Width : Height;

    public double DisplayHeight => IsPortrait ? Height : Width;
    
    public bool IsThumbnailRendering => Thumbnail is null;

    public bool IsPortrait => Rotation == 0 || Rotation == 180;

    private long _isSizeSet;

    public void UpdateSearchResultsRanges(IReadOnlyList<Range>? searchResultsRanges)
    {
        if (_searchResultsRanges is null && searchResultsRanges is null)
        {
            return;
        }
        
        if (_searchResultsRanges is not null && searchResultsRanges is not null &&
            _searchResultsRanges.SequenceEqual(searchResultsRanges))
        {
            return;
        }
        
        _searchResultsRanges = searchResultsRanges;
        if (PdfTextLayer is null)
        {
            return;
        }
        
        RefreshSearchResults();
    }

    public void SetSize(double width, double height, double ppiScale)
    {
        if (Interlocked.Exchange(ref _isSizeSet, 1) == 1)
        {
            return;
        }
        
        Width = width * ppiScale;
        Height = height * ppiScale;
        PpiScale = ppiScale;
    }

    public bool IsSizeSet()
    {
        return Interlocked.Read(ref _isSizeSet) == 1;
    }

    partial void OnPdfTextLayerChanged(PdfTextLayer? value)
    {
        if (value is null)
        {
            return;
        }
        
        // We ensure the correct selection is set now that we have the text layer
        RefreshTextSelection();
        RefreshSearchResults();
    }

#if DEBUG
    /// <summary>
    /// Design mode constructor.
    /// </summary>
    public PageViewModel()
    {
        if (!Avalonia.Controls.Design.IsDesignMode)
        {
            throw new InvalidOperationException(
                $"{typeof(PageViewModel)} empty constructor should only be called in design mode");
        }

        TextSelection = null!;
    }
#endif
    
    public PageViewModel(int pageNumber, TextSelection textSelection)
    {
        ArgumentNullException.ThrowIfNull(textSelection, nameof(textSelection));
        PageNumber = pageNumber;
        TextSelection = textSelection;

        TextSelection.TextSelectionExtended += _onTextSelectionExtended;
        TextSelection.TextSelectionFocusPageChanged += _onTextSelectionFocusPageChanged;
        TextSelection.TextSelectionReset += _onTextSelectionReset;
    }

    private void _onTextSelectionReset(object? sender, EventArgs e)
    {
        if (SelectedWords is not null)
        {
            Dispatcher.UIThread.Invoke(() => SelectedWords = null);
        }
    }

    private void _onTextSelectionFocusPageChanged(object? sender, TextSelectionFocusPageChangedEventArgs e)
    {
        if (PdfTextLayer is null || PdfTextLayer.Count == 0)
        {
            return;
        }
        
        if (e.OldFocusPageIndex == -1)
        {
            return;
        }
        
        int start = Math.Min(e.OldFocusPageIndex, e.NewFocusPageIndex);
        int end = Math.Max(e.OldFocusPageIndex, e.NewFocusPageIndex);

        if (PageNumber < start || PageNumber > end)
        {
            return;
        }

        RefreshTextSelection();
    }

    private void _onTextSelectionExtended(object? sender, TextSelectionExtendedEventArgs e)
    {
        if (PdfTextLayer is null || PdfTextLayer.Count == 0)
        {
            return;
        }

        if (e.FocusPageIndex != PageNumber)
        {
            return;
        }

        RefreshTextSelection();
    }

    private void RefreshSearchResults()
    {
        if (_searchResultsRanges is null || _searchResultsRanges.Count == 0)
        {
            Dispatcher.UIThread.Invoke(() => SearchResults = null);
            return;
        }

        System.Diagnostics.Debug.Assert(PdfTextLayer is not null);
        
        var results = new List<PdfRectangle>(_searchResultsRanges.Count);
        foreach (var range in _searchResultsRanges)
        {
            var start = PdfTextLayer[range.Start];
            var end = PdfTextLayer[range.End];
            results.AddRange(PdfTextLayer.GetWords(start, end)
                .Select(x => x.BoundingBox));
        }

        if (results.Count > 0)
        {
            Dispatcher.UIThread.Invoke(() => SearchResults = results);
        }
        else
        {
            Dispatcher.UIThread.Invoke(() => SearchResults = null);
        }
    }

    private void RefreshTextSelection()
    {
        System.Diagnostics.Debug.Assert(PdfTextLayer is not null);
        
        if (!TextSelection.IsPageInSelection(PageNumber))
        {
            if (SelectedWords is not null)
            {
                Dispatcher.UIThread.Invoke(() => SelectedWords = null);
            }

            return;
        }

        var selectedWords = GetSelectedWords().ToArray();

        if (selectedWords.Length == 0)
        {
            if (SelectedWords is not null)
            {
                Dispatcher.UIThread.Invoke(() => SelectedWords = null);  // TODO - Check if we should do that here
            }
        }
        else
        {
            var selectedWordRects = TextSelection.GetPageSelectionAs(
                    selectedWords, PageNumber,
                    PdfWordHelpers.GetRectangle, PdfWordHelpers.GetRectangle)
                .ToArray();
            Dispatcher.UIThread.Invoke(() => SelectedWords = selectedWordRects);
        }
    }

    public IEnumerable<PdfWord> GetSelectedWords()
    {
        System.Diagnostics.Debug.Assert(PdfTextLayer is not null);
        return TextSelection.GetSelectedWords(PageNumber, PdfTextLayer);
    }

    internal void RotateClockwise()
    {
        Rotation = (Rotation + 90) % 360;
    }

    internal void RotateCounterclockwise()
    {
        Rotation = (Rotation + 270) % 360;
    }

    public void Clear()
    {
        PdfTextLayer = null;

        var picture = PdfPicture;
        PdfPicture = null;

        var thumbnail = Thumbnail;
        Thumbnail = null;

        if (picture is not null || thumbnail is not null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                picture?.Dispose();
                thumbnail?.Dispose();
            }, DispatcherPriority.Loaded);
        }
    }

    public ValueTask DisposeAsync()
    {
        Debug.ThrowOnUiThread();

        TextSelection.TextSelectionExtended -= _onTextSelectionExtended;
        TextSelection.TextSelectionFocusPageChanged -= _onTextSelectionFocusPageChanged;
        TextSelection.TextSelectionReset -= _onTextSelectionReset;

        Clear();

        return ValueTask.CompletedTask;
    }
}