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
using Avalonia.Threading;
using Caly.Core.ViewModels;
using Caly.Pdf.Models;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace Caly.Core.Handlers
{
    public sealed partial class PageInteractiveLayerHandler
    {
        private static readonly Color _searchColor = Color.FromArgb(0xa9, 255, 0, 0);

        private readonly IReadOnlyList<PdfWord>?[] _searchWordsResults;
        private readonly IReadOnlyList<Range>?[] _searchIndexResults;

        public void ClearTextSearchResults(PdfDocumentViewModel documentViewModel)
        {
            Debug.ThrowOnUiThread();

            for (var p = 0; p < _searchWordsResults.Length; ++p)
            {
                if (_searchIndexResults[p] is null)
                {
                    continue;
                }

                _searchIndexResults[p] = null;

                if (_searchWordsResults[p] is not null)
                {
                    _searchWordsResults[p] = null;
                    Dispatcher.UIThread.Invoke(documentViewModel.Pages[p].FlagInteractiveLayerChanged);
                }
            }
        }

        public void AddTextSearchResults(PdfDocumentViewModel documentViewModel, IReadOnlyCollection<TextSearchResultViewModel> searchResults)
        {
            if (searchResults.Count > 0)
            {
                foreach (var result in searchResults)
                {
                    System.Diagnostics.Debug.Assert(result.Nodes is not null);

                    _searchIndexResults[result.PageNumber - 1] = result.Nodes
                        .Where(x => x is { ItemType: SearchResultItemType.Word, WordIndex: not null })
                        .Select(x => new Range(new Index(x.WordIndex!.Value), new Index(x.WordIndex.Value + x.WordCount!.Value - 1)))
                        .ToArray();

                    var page = documentViewModel.Pages[result.PageNumber - 1];
                    if (page.PdfTextLayer is not null)
                    {
                        page.PageInteractiveLayerHandler.UpdateInteractiveLayer(page);
                    }
                }
            }
        }
    }
}
