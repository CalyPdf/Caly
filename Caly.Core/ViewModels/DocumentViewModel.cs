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
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Models;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Caly.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Caly.Core.ViewModels;

/// <summary>
/// View model that represent a PDF document.
/// </summary>
[DebuggerDisplay("[{_pdfService?.FileName}]")]
public sealed partial class DocumentViewModel : ViewModelBase
{
    public override string ToString()
    {
        return FileName ?? "FileName NOT SET";
    }

    private readonly IPdfDocumentService _pdfDocumentService;
    private readonly PdfPageService _pdfPageService;
    private readonly ISettingsService _settingsService;

    private readonly CancellationTokenSource _cts = new();
    internal string? LocalPath { get; private set; }

    [ObservableProperty] private ObservableCollection<PageViewModel> _pages = [];

    [ObservableProperty] private bool _isDocumentPaneOpen = !CalyExtensions.IsMobilePlatform();

    [ObservableProperty] private double _paneSize;

    [ObservableProperty] private int _selectedTabIndex;
    
    [ObservableProperty] private bool _isPasswordProtected;

    [ObservableProperty] private Range? _visiblePages;

    [ObservableProperty] private Range? _realisedPages;

    [ObservableProperty] private int _pageCount;

    [ObservableProperty] private string? _fileName;

    [ObservableProperty] private string? _fileSize;
    
    /// <summary>
    /// Starts at <c>1</c>, ends at <see cref="PageCount"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoToPreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(GoToNextPageCommand))]
    private string _selectedPageIndexString = "1";

    /// <summary>
    /// Starts at <c>1</c>, ends at <see cref="PageCount"/>.
    /// </summary>
    public int? SelectedPageIndex
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            SelectedPageIndexString = value.HasValue ? value.Value.ToString("0") : string.Empty;
        }
    }

    public bool IsActive
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            _pdfDocumentService.IsActive = value;
        }
    }

    public IPageInteractiveLayerHandler? PageInteractiveLayerHandler => _pdfDocumentService.PageInteractiveLayerHandler;

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

        _pdfDocumentService = new PdfPigDocumentService(new SearchValuesTextSearchService());
        _settingsService = new JsonSettingsService(null!);
        _paneSize = 50;

        IsPasswordProtected = _pdfDocumentService.IsPasswordProtected;
        FileName = _pdfDocumentService.FileName;
        LocalPath = _pdfDocumentService.LocalPath;
        PageCount = _pdfDocumentService.NumberOfPages;
    }
#endif

    public DocumentViewModel(IPdfDocumentService pdfDocumentService, PdfPageService pdfPageService, ISettingsService settingsService)
    {
        ArgumentNullException.ThrowIfNull(pdfDocumentService, nameof(pdfDocumentService));
        ArgumentNullException.ThrowIfNull(pdfPageService, nameof(pdfPageService));
        ArgumentNullException.ThrowIfNull(settingsService, nameof(settingsService));

        System.Diagnostics.Debug.Assert(pdfDocumentService.NumberOfPages == 0);

        _pdfDocumentService = pdfDocumentService;
        _pdfPageService = pdfPageService;
        _settingsService = settingsService;

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
                    if (PageInteractiveLayerHandler is null)
                    {
                        throw new NullReferenceException(
                            "The PageInteractiveLayerHandler is null, cannot process search results.");
                    }

                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Reset:
                            PageInteractiveLayerHandler.ClearTextSearchResults(this);
                            break;

                        case NotifyCollectionChangedAction.Add:
                            if (e.NewItems?.Count > 0)
                            {
                                var searchResult = e.NewItems.OfType<TextSearchResultViewModel>().ToArray();
                                var first = searchResult.FirstOrDefault();

                                if (first is null || first.PageNumber <= 0)
                                {
                                    PageInteractiveLayerHandler.ClearTextSearchResults(this);
                                }
                                else
                                {
                                    PageInteractiveLayerHandler.AddTextSearchResults(this, searchResult);
                                }
                            }

                            if (e.OldItems?.Count > 0)
                            {
                                throw new NotImplementedException($"SearchResults Action '{e.Action}' with OldItems.");
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

        SearchResultsSource = new HierarchicalTreeDataGridSource<TextSearchResultViewModel>(SearchResults)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<TextSearchResultViewModel>(
                    new TextColumn<TextSearchResultViewModel, string>(null, x => x.ToString()),
                    x => x.Nodes)
            }
        };

        Dispatcher.UIThread.Invoke(() =>
        {
            SearchResultsSource.RowSelection!.SingleSelect = true;
            SearchResultsSource.RowSelection.SelectionChanged += TextSearchSelectionChanged;
        });
    }

    /// <summary>
    /// Open the pdf document.
    /// </summary>
    /// <returns>The number of pages in the opened document. <c>0</c> if the document was not opened.</returns>
    public Task<int> OpenDocument(IStorageFile? storageFile, string? password, CancellationToken token)
    {
        var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, token);

        WaitOpenAsync = Task.Run(async () =>
        {
            int pageCount = await _pdfDocumentService.OpenDocument(storageFile, password, combinedCts.Token);

            IsPasswordProtected = _pdfDocumentService.IsPasswordProtected;
            FileName = _pdfDocumentService.FileName;
            LocalPath = _pdfDocumentService.LocalPath;

            if (pageCount == 0)
            {
                return pageCount;
            }

            PageCount = _pdfDocumentService.NumberOfPages;

            if (_pdfDocumentService.FileSize.HasValue)
            {
                FileSize = Helpers.FormatSizeBytes(_pdfDocumentService.FileSize.Value);
            }

            return pageCount;
        }, combinedCts.Token)
        .ContinueWith(s =>
        {
            combinedCts.Dispose();
            return s.Result;
        }, TaskContinuationOptions.ExecuteSynchronously);

        return WaitOpenAsync;
    }

    public void ClearAllThumbnails()
    {
        _pdfDocumentService.ClearAllThumbnail();
    }

    internal async ValueTask CancelAsync()
    {
        await _cts.CancelAsync();
    }

    private async Task LoadPages()
    {
        if (PageCount == 0)
        {
            if (IsPasswordProtected)
            {
                throw new Exception("Could not open password protected document.");
            }

            throw new Exception("Cannot load pages because document has 0 pages.");
        }

        await Task.Run((Func<Task?>)(async () =>
        {
            // Use 1st page size as default page size
            var firstPage = new PageViewModel(1, _pdfDocumentService);
            await firstPage.LoadPageSizeImmediate(_cts.Token);

            App.Messenger.Send(new LoadPageMessage(firstPage)); // Enqueue first page full loading

            double defaultWidth = firstPage.Width * _pdfDocumentService.PpiScale;
            double defaultHeight = firstPage.Height * _pdfDocumentService.PpiScale;

            Pages.Add(firstPage);

            for (int p = 2; p <= PageCount; p++)
            {
                _cts.Token.ThrowIfCancellationRequested();
                var newPage = new PageViewModel(p, _pdfDocumentService)
                {
                    Height = defaultHeight,
                    Width = defaultWidth
                };

                App.Messenger.Send(new LoadPageSizeMessage(newPage));
                Pages.Add(newPage);
            }
        }), _cts.Token);
    }

    [RelayCommand]
    private async Task RefreshPages()
    {
        //_pdfPageService
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private void GoToPreviousPage()
    {
        if (!SelectedPageIndex.HasValue)
        {
            return;
        }

        SelectedPageIndex = Math.Max(1, SelectedPageIndex.Value - 1);
    }

    private bool CanGoToPreviousPage()
    {
        if (!SelectedPageIndex.HasValue)
        {
            return false;
        }

        return SelectedPageIndex.Value > 1;
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private void GoToNextPage()
    {
        if (!SelectedPageIndex.HasValue)
        {
            return;
        }

        SelectedPageIndex = Math.Min(PageCount, SelectedPageIndex.Value + 1);
    }

    private bool CanGoToNextPage()
    {
        if (!SelectedPageIndex.HasValue)
        {
            return false;
        }

        return SelectedPageIndex.Value < PageCount;
    }
    
    [RelayCommand]
    private async Task CloseDocument(CancellationToken token)
    {
        var pdfDocumentsService = App.Current?.Services?.GetRequiredService<IPdfDocumentsManagerService>()!;
        await Task.Run(() => pdfDocumentsService.CloseUnloadDocument(this), token);
    }
}