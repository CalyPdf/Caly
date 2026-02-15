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

using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Caly.Core.Models;
using Caly.Core.Services;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Caly.Core.ViewModels;

/// <summary>
/// View model that represent a PDF document.
/// </summary>
[DebuggerDisplay("[{_pdfService?.FileName}]")]
public sealed partial class DocumentViewModel : ViewModelBase
{
    public override string ToString()
    {
        return _pdfService?.FileName ?? "FileName NOT SET";
    }

    private readonly IPdfDocumentService _pdfService;
    private readonly PdfPageService _pdfPageService;
    private readonly ISettingsService _settingsService;

    private readonly CancellationTokenSource _mainCts = new();
    internal string? LocalPath { get; private set; }

    public bool IsActive => _pdfService.IsActive;

    [ObservableProperty] private ObservableCollection<PageViewModel> _pages = [];

    [ObservableProperty] private bool _isDocumentPaneOpen = !CalyExtensions.IsMobilePlatform();

    [ObservableProperty] private double _paneSize;

    [ObservableProperty] private int _selectedTabIndex;

    [ObservableProperty] private bool _isPasswordProtected;

    [ObservableProperty] private TextSelection? _textSelection;

    [ObservableProperty] private Range? _visiblePages;

    [ObservableProperty] private Range? _realisedPages;

    [ObservableProperty] private Range? _visibleThumbnails;

    [ObservableProperty] private Range? _realisedThumbnails;

    /// <summary>
    /// Starts at <c>1</c>, ends at <see cref="PageCount"/>.
    /// <para><c>null</c> if not selected.</para>
    /// </summary>
    public int? SelectedPageNumber
    {
        get;
        set
        {
            if (value.HasValue)
            {
                if (value.Value <= 0)
                {
                    throw new ArgumentException("Selected page should exist in the document.", nameof(SelectedPageNumber));
                }

                if (value.Value > PageCount)
                {
                    throw new ArgumentException("Selected page should exist in the document.", nameof(SelectedPageNumber));
                }
            }

            if (!SetProperty(ref field, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedPageIndex));
            GoToPreviousPageCommand.NotifyCanExecuteChanged();
            GoToNextPageCommand.NotifyCanExecuteChanged();
        }
    } = 1;

    /// <summary>
    /// Starts at <c>0</c>, ends at <see cref="PageCount"/> <c>- 1</c>.
    /// <para><c>-1</c> if not selected.</para>
    /// </summary>
    public int SelectedPageIndex
    {
        get
        {
            if (SelectedPageNumber.HasValue)
            {
                return SelectedPageNumber.Value - 1;
            }

            return -1;
        }
        set
        {
            if (value != -1)
            {
                SelectedPageNumber = value + 1;
                return;
            }

            SelectedPageNumber = null;
        }
    }

    [ObservableProperty] private int _pageCount;

    [ObservableProperty] private string? _fileName;

    [ObservableProperty] private string? _fileSize;

    private readonly Lazy<Task> _loadPagesTask;
    public Task LoadPagesTask => _loadPagesTask.Value;

    /// <summary>
    /// The task that opens the document. Can be awaited to make sure the document is done opening.
    /// </summary>
    public Task<int>? WaitOpenAsync { get; private set; }

    partial void OnPaneSizeChanged(double oldValue, double newValue)
    {
        _settingsService.SetProperty(CalySettings.CalySettingsProperty.PaneSize, newValue);
    }

    private readonly IDisposable _searchResultsDisposable;

    private readonly ITextSearchService _textSearchService;

#if DEBUG
    public DocumentViewModel()
    {
        if (!Design.IsDesignMode)
        {
            throw new InvalidOperationException("Should only be called in Design mode.");
        }

        _loadPagesTask = null!;
        _searchResultsDisposable = null!;
        _loadPropertiesTask = null!;
        _loadBookmarksTask = null!;
        _buildSearchIndex = null!;
        _searchResultsSource = null!;

        _pdfService = new PdfPigDocumentService();
        _settingsService = new JsonSettingsService(null!);
        _paneSize = 50;

        IsPasswordProtected = _pdfService.IsPasswordProtected;
        FileName = _pdfService.FileName;
        LocalPath = _pdfService.LocalPath;
        PageCount = _pdfService.NumberOfPages;
        TextSelection = new TextSelection(PageCount);
    }
#endif

    public DocumentViewModel(IPdfDocumentService pdfService, PdfPageService pdfPageService, ITextSearchService textSearchService, ISettingsService settingsService)
    {
        ArgumentNullException.ThrowIfNull(pdfService, nameof(pdfService));
        ArgumentNullException.ThrowIfNull(settingsService, nameof(settingsService));

        System.Diagnostics.Debug.Assert(pdfService.NumberOfPages == 0);

        _pdfService = pdfService;
        _pdfPageService = pdfPageService;
        _settingsService = settingsService;
        _textSearchService = textSearchService;

        _paneSize = _settingsService.GetSettings().PaneSize;

        _loadPagesTask = new Lazy<Task>(LoadPages);
        _loadBookmarksTask = new Lazy<Task>(LoadBookmarks);
        _loadPropertiesTask = new Lazy<Task>(LoadProperties);
        _buildSearchIndex = new Lazy<Task>(BuildSearchIndex);

        _searchResultsDisposable = SearchResults
            .GetWeakCollectionChangedObservable()
            .ObserveOn(Scheduler.Default)
            .Subscribe(e =>
            {
                Debug.ThrowOnUiThread();

                try
                {
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Reset:
                            // Clear selection highlights
                            foreach (var page in Pages)
                            {
                                page.UpdateSearchResultsRanges(null);
                            }
                            break;

                        case NotifyCollectionChangedAction.Add:
                            if (e.NewItems?.Count > 0)
                            {
                                var searchResults = e.NewItems.OfType<TextSearchResult>().ToArray();

                                if (searchResults.Length == 0 || searchResults[0].PageNumber <= 0)
                                {
                                    // Clear selection highlights
                                    foreach (var page in Pages)
                                    {
                                        if (page.SearchResults is not null)
                                        {
                                            page.UpdateSearchResultsRanges(null);
                                        }
                                    }
                                }
                                else
                                {
                                    foreach (var result in searchResults)
                                    {
                                        System.Diagnostics.Debug.Assert(result.Nodes is not null);

                                        var searchRange = result.Nodes
                                            .Where(x => x is
                                                { ItemType: SearchResultItemType.Word, WordIndex: not null })
                                            .Select(x => new Range(new Index(x.WordIndex!.Value),
                                                new Index(x.WordIndex.Value + x.WordCount!.Value - 1))).ToArray();

                                        var page = Pages[result.PageNumber - 1];
                                        page.UpdateSearchResultsRanges(searchRange);
                                    }
                                }
                            }
                            break;

                        case NotifyCollectionChangedAction.Remove:
                        case NotifyCollectionChangedAction.Replace:
                        case NotifyCollectionChangedAction.Move:
                            throw new NotImplementedException($"SearchResults Action '{e.Action}'.");
                    }
                }
                catch (OperationCanceledException)
                {
                    // No op
                }
                catch (Exception ex)
                {
                    Debug.WriteExceptionToFile(ex);
                    Dispatcher.UIThread.Post(() => Exception = new ExceptionViewModel(ex));
                }
            });

        SearchResultsSource = new HierarchicalTreeDataGridSource<TextSearchResult>(SearchResults)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<TextSearchResult>(
                    new TextColumn<TextSearchResult, string>(null, x => x.ToString()),
                    x => x.Nodes)
            }
        };

        Dispatcher.UIThread.Invoke(() =>
        {
            SearchResultsSource.RowSelection!.SingleSelect = true;
            SearchResultsSource.RowSelection.SelectionChanged += TextSearchSelectionChanged;
        }, DispatcherPriority.Send, _mainCts.Token);
    }

    public void SetActive()
    {
        _pdfService.IsActive = true;
    }

    public void SetInactive()
    {
        _pdfService.IsActive = false;
    }

    /// <summary>
    /// Open the pdf document.
    /// </summary>
    /// <returns>The number of pages in the opened document. <c>0</c> if the document was not opened.</returns>
    public Task<int> OpenDocument(IStorageFile? storageFile, string? password, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(storageFile, nameof(storageFile));
        LocalPath = storageFile.Path.LocalPath;

        WaitOpenAsync = Task.Run(async () =>
        {
            using (var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(_mainCts.Token, token))
            {
                int pageCount = await _pdfService.OpenDocument(storageFile, password, combinedCts.Token);

                IsPasswordProtected = _pdfService.IsPasswordProtected;
                FileName = _pdfService.FileName;
                System.Diagnostics.Debug.Assert(_pdfService.LocalPath == LocalPath);

                if (pageCount == 0)
                {
                    return pageCount;
                }

                PageCount = _pdfService.NumberOfPages;
                TextSelection = new TextSelection(PageCount);

                if (_pdfService.FileSize.HasValue)
                {
                    FileSize = Helpers.FormatSizeBytes(_pdfService.FileSize.Value);
                }

                _pdfPageService.Initialise();

                return pageCount;
            }
        }, token);

        return WaitOpenAsync;
    }

    internal async ValueTask CancelAsync()
    {
        await _mainCts.CancelAsync();
    }

    private async Task LoadPages()
    {
        System.Diagnostics.Debug.Assert(TextSelection is not null);
        
        if (PageCount == 0)
        {
            if (IsPasswordProtected)
            {
                throw new Exception("Could not open password protected document.");
            }
            throw new Exception("Cannot load pages because document has 0 pages.");
        }

        await Task.Run(async () =>
        {
            // Use 1st page size as default page size
            var firstPage = new PageViewModel(1, TextSelection);
            var pageInfo = await _pdfPageService.GetPageSize(1, _mainCts.Token);
            if (pageInfo.HasValue)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    firstPage.SetSize(pageInfo.Value.Width, pageInfo.Value.Height,
                        _pdfService.PpiScale);
                });
            }

            double defaultWidth = firstPage.Width * _pdfService.PpiScale;
            double defaultHeight = firstPage.Height * _pdfService.PpiScale;

            Pages.Add(firstPage);

            for (int p = 2; p <= PageCount; ++p)
            {
                _mainCts.Token.ThrowIfCancellationRequested();
                var newPage = new PageViewModel(p, TextSelection)
                {
                    Height = defaultHeight,
                    Width = defaultWidth
                };
                _pdfPageService.RequestPageSize(newPage);
                Pages.Add(newPage);
            }
        }, _mainCts.Token);
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private void GoToPreviousPage()
    {
        if (!SelectedPageNumber.HasValue)
        {
            return;
        }

        SelectedPageNumber = Math.Max(1, SelectedPageNumber.Value - 1);
    }

    private bool CanGoToPreviousPage()
    {
        if (!SelectedPageNumber.HasValue)
        {
            return false;
        }

        return SelectedPageNumber.Value > 1;
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private void GoToNextPage()
    {
        if (!SelectedPageNumber.HasValue)
        {
            return;
        }

        SelectedPageNumber = Math.Min(PageCount, SelectedPageNumber.Value + 1);
    }

    private bool CanGoToNextPage()
    {
        if (!SelectedPageNumber.HasValue)
        {
            return false;
        }

        return SelectedPageNumber.Value < PageCount;
    }
    
    [RelayCommand]
    private async Task CloseDocument(CancellationToken token)
    {
        var pdfDocumentsService = App.Current?.Services?.GetRequiredService<IPdfDocumentsManagerService>()!;
        await Task.Run(() => pdfDocumentsService.CloseUnloadDocument(this), token);
    }

    [RelayCommand]
    private async Task RefreshPages()
    {
        try
        {
            await _pdfPageService.RefreshPages(new RefreshPagesRequestMessage()
            {
                Document = this,
                VisiblePages = VisiblePages,
                RealisedPages = RealisedPages,
                VisibleThumbnails = VisibleThumbnails,
                RealisedThumbnails = RealisedThumbnails
            });
        }
        catch (OperationCanceledException)
        { }
        catch (Exception ex)
        {
            Debug.WriteExceptionToFile(ex);
        }
    }

    [RelayCommand]
    private async Task RefreshThumbnails()
    {
        try
        {
            await _pdfPageService.RefreshThumbnails(new RefreshPagesRequestMessage()
            {
                Document = this,
                VisiblePages = VisiblePages,
                RealisedPages = RealisedPages,
                VisibleThumbnails = VisibleThumbnails,
                RealisedThumbnails = RealisedThumbnails
            });
        }
        catch (OperationCanceledException)
        { }
        catch (Exception ex)
        {
            Debug.WriteExceptionToFile(ex);
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        Debug.ThrowNotOnUiThread();

        if (TextSelection is null)
        {
            return;
        }

        System.Diagnostics.Debug.Assert(TextSelection.GetStartPageIndex() <= TextSelection.GetEndPageIndex());

        TextSelection.ResetSelection();
    }

    [RelayCommand]
    private async Task Clear()
    {
        await Task.Run(() =>
        {
            foreach (var page in Pages)
            {
                page.Clear();
            }
        });

        await _pdfPageService.CancelAndClear();

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false);
    }

    [RelayCommand]
    private void Activated()
    {
        App.Messenger.Send(new SelectedDocumentChangedMessage(this));
    }
}