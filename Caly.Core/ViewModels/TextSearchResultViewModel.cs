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
using System.Collections.Generic;
using System.Diagnostics;

namespace Caly.Core.ViewModels;

[DebuggerDisplay("Page {PageNumber} Word index: {WordIndex} ({WordCount}), Children {Nodes?.Count}")]
public sealed class TextSearchResultViewModel : ViewModelBase
{
    public required SearchResultItemType ItemType { get; init; }

    public required int PageNumber { get; init; }

    public int? WordIndex { get; init; }

    public int? WordCount { get; init; }

    public IReadOnlyList<TextSearchResultViewModel>? Nodes { get; init; }

    public ReadOnlyMemory<char> SampleText { get; init; }

    public override string ToString()
    {
        if (!SampleText.IsEmpty)
        {
            return $"...{SampleText}...";
        }

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