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
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using Lifti;
using Lifti.Querying;
using Lifti.Tokenization;

namespace Caly.Core.Services
{
    internal sealed class LiftiTextSearchService : ITextSearchService
    {
        private readonly FullTextIndex<int> _index;

        /// <summary>
        /// The Caly <see cref="IndexTokenizer"/> that does not split tokens.
        /// </summary>
        private sealed class CalyIndexTokenizer : IndexTokenizer
        {
            public CalyIndexTokenizer(TokenizationOptions tokenizationOptions)
                : base(tokenizationOptions)
            {
            }

            public override bool IsSplitCharacter(char character)
            {
                return false;
            }
        }

        public LiftiTextSearchService()
        {
            _index = new FullTextIndexBuilder<int>()
                //.WithIndexModificationAction(OnIndexChange)
                .WithObjectTokenization<PdfPageViewModel>(
                    options => options
                        .WithKey(p => p.PageNumber)
                        .WithField("w", async (p, ct) =>
                        {
                            return await Task.Run(async () =>
                            {
                                var textLayer = p.PdfTextLayer;
                                if (textLayer is null)
                                {
                                    await p.SetPageTextLayer(ct);
                                    textLayer = p.PdfTextLayer;
                                }

                                if (textLayer is null)
                                {
                                    ct.ThrowIfCancellationRequested();
                                    throw new NullReferenceException("Cannot index search on a null PdfTextLayer.");
                                }

                                return textLayer.Select(w => w.Value);
                            }, ct);
                        }, tokenizationOptions: builder => builder.WithFactory(o => new CalyIndexTokenizer(o)))
                        .WithField("a", async (p, ct) =>
                        {
                            return await Task.Run(async () =>
                            {
                                var textLayer = p.PdfTextLayer;
                                if (textLayer is null)
                                {
                                    await p.SetPageTextLayer(ct);
                                    textLayer = p.PdfTextLayer;
                                }

                                if (textLayer is null)
                                {
                                    ct.ThrowIfCancellationRequested();
                                    throw new NullReferenceException("Cannot index search on a null PdfTextLayer.");
                                }

                                if (textLayer.Annotations.Count == 0)
                                {
                                    return Array.Empty<string>();
                                }
                                
                                // TODO - Index is not usable as is... need to find a way to find the annot back

                                return textLayer.Annotations
                                    .Where(a => !string.IsNullOrEmpty(a.Content))
                                    .Select(a => a.Content!);
                            }, ct);
                        })
                ).Build();
        }

        public async Task BuildPdfDocumentIndex(PdfDocumentViewModel pdfDocument, IProgress<int> progress, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            for (int i = 0; i < pdfDocument.PageCount; ++i)
            {
                if (i % 10 == 0)
                {
                    token.ThrowIfCancellationRequested();
                    if (i > 0)
                    {
                        await _index.CommitBatchChangeAsync(token);
                    }
                    _index.BeginBatchChange();
                }

                await _index.AddAsync(pdfDocument.Pages[i], token);
                progress.Report(i + 1);

                System.Diagnostics.Debug.Assert(pdfDocument.Pages[i].PdfTextLayer is not null);
            }

            token.ThrowIfCancellationRequested();

            await _index.CommitBatchChangeAsync(token);
            progress.Report(pdfDocument.PageCount);
        }

        public async Task<IEnumerable<TextSearchResultViewModel>> Search(PdfDocumentViewModel pdfDocument, string text, CancellationToken token)
        {
            Debug.ThrowOnUiThread();
            
            token.ThrowIfCancellationRequested();
            
            if (string.IsNullOrEmpty(text))
            {
                return [];
            }

            IQuery query = _index.QueryParser.Parse(_index.FieldLookup, text, _index);

            return await Task.Run(() =>
            {
                ISearchResults<int> results = _index.Search(query);

                token.ThrowIfCancellationRequested();

                return results.Select(r => ToViewModel(pdfDocument, r, token));
            }, token);
        }

        private static SearchResultItemType GetSearchResultItemType(string fieldName)
        {
            return fieldName switch
            {
                "w" => SearchResultItemType.Word,
                "a" => SearchResultItemType.Annotation,
                "Unspecified" => SearchResultItemType.Unspecified,
                _ => throw new NotImplementedException($"Unknown field name '{fieldName}'.")
            };
        }

        private static TextSearchResultViewModel ToViewModel(PdfDocumentViewModel pdfDocument, SearchResult<int> result, CancellationToken token)
        {
            var children = new ObservableCollection<TextSearchResultViewModel>();

            foreach (var m in result.FieldMatches)
            {
                token.ThrowIfCancellationRequested();

                var itemType = GetSearchResultItemType(m.FoundIn);

                foreach (TokenLocation l in m.Locations)
                {
                    token.ThrowIfCancellationRequested();

                    var vm = new TextSearchResultViewModel()
                    {
                        PageNumber = result.Key,
                        ItemType = itemType,
                        WordIndex = l.TokenIndex,
                        Score = m.Score,
                        Word = itemType == SearchResultItemType.Word
                            ? pdfDocument.Pages[result.Key - 1].PdfTextLayer?[l.TokenIndex]
                            : null
                    };
                    children.Add(vm);
                }
            }

            return new TextSearchResultViewModel()
            {
                PageNumber = result.Key,
                Nodes = children
            };
        }

        public void Dispose()
        {
            _index.Dispose();
            System.Diagnostics.Debug.WriteLine("Text search service disposed");
        }
    }
}
