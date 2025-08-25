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
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Media;
using Avalonia.Threading;
using Caly.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Caly.Core.ViewModels
{
    public partial class PdfDocumentViewModel
    {
        private readonly Lazy<Task> _loadBookmarksTask;
        public Task LoadBookmarksTask => _loadBookmarksTask.Value;

        [ObservableProperty] private ObservableCollection<PdfBookmarkNode>? _bookmarks;
        [ObservableProperty] private HierarchicalTreeDataGridSource<PdfBookmarkNode>? _bookmarksSource;

        [ObservableProperty] private PdfBookmarkNode? _selectedBookmark;

        private async Task LoadBookmarks()
        {
            _cts.Token.ThrowIfCancellationRequested();
            await Task.Run(() => _pdfService.SetPdfBookmark(this, _cts.Token));
            if (Bookmarks?.Count > 0)
            {
                BookmarksSource = new HierarchicalTreeDataGridSource<PdfBookmarkNode>(Bookmarks)
                {
                    Columns =
                    {
                        new HierarchicalExpanderColumn<PdfBookmarkNode>(
                            new TextColumn<PdfBookmarkNode, string>(null,
                                x => x.Title, options: new TextColumnOptions<PdfBookmarkNode>()
                                {
                                    CanUserSortColumn = false,
                                    IsTextSearchEnabled = false,
                                    TextWrapping = TextWrapping.WrapWithOverflow,
                                    TextAlignment = TextAlignment.Left,
                                    MaxWidth = new GridLength(400)
                                }), x => x.Nodes)
                    }
                };

                Dispatcher.UIThread.Invoke(() =>
                {
                    BookmarksSource.RowSelection!.SingleSelect = true;
                    BookmarksSource.RowSelection.SelectionChanged += BookmarksSelectionChanged;
                    BookmarksSource.ExpandAll();
                });
            }
        }

        private void BookmarksSelectionChanged(object? sender, Avalonia.Controls.Selection.TreeSelectionModelSelectionChangedEventArgs<PdfBookmarkNode> e)
        {
            if (e.SelectedItems.Count == 0)
            {
                return;
            }

            SelectedBookmark = e.SelectedItems[0];
        }
    }
}
