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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Caly.Pdf.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Caly.Core.ViewModels
{
    [DebuggerDisplay("Page {PageNumber} Word index: {WordIndex} ({WordCount}), Children {Nodes?.Count}")]
    public sealed partial class TextSearchResultViewModel : ViewModelBase
    {
        [ObservableProperty] private SearchResultItemType _itemType;
        [ObservableProperty] private int _pageNumber;
        [ObservableProperty] private int? _wordIndex;
        [ObservableProperty] private int? _wordCount;
        [ObservableProperty] private IReadOnlyList<PdfWord>? _word;
        [ObservableProperty] private ObservableCollection<TextSearchResultViewModel>? _nodes;

        public override string ToString()
        {
            if (Nodes is null)
            {
                return $"{WordIndex} [{ItemType}]";
            }
            return $"{PageNumber} ({Nodes.Count})";
        }
    }

    public enum SearchResultItemType : byte
    {
        Unspecified = 0,
        Word = 1,
        Annotation = 2,
    }
}
