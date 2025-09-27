using Caly.Core.Services.Interfaces;
using Caly.Core.ViewModels;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static SkiaSharp.HarfBuzz.SKShaper;

namespace Caly.Core.Services
{
    internal class SearchValuesTextSearchService : ITextSearchService
    {
        private readonly ConcurrentDictionary<int, string> _index = new ConcurrentDictionary<int, string>();
        
        public void Dispose()
        { }

        public async Task BuildPdfDocumentIndex(PdfDocumentViewModel pdfDocument, IProgress<int> progress, CancellationToken token)
        {
            int done = 0;
            await Parallel.ForEachAsync(pdfDocument.Pages, token, async (p, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                
                await Task.Run(async () =>
                {
                    var textLayer = p.PdfTextLayer;
                    if (textLayer is null)
                    {
                        await p.SetPageTextLayerImmediate(ct);
                        textLayer = p.PdfTextLayer;
                    }

                    if (textLayer is null)
                    {
                        ct.ThrowIfCancellationRequested();
                        throw new NullReferenceException("Cannot index search on a null PdfTextLayer.");
                    }

                    _index[p.PageNumber] = string.Join(' ', textLayer.Select(w => w.Value));
                    progress.Report(Interlocked.Add(ref done, 1));
                }, ct);
            });
        }

        public async Task<IEnumerable<TextSearchResultViewModel>> Search(PdfDocumentViewModel pdfDocument, string text, CancellationToken token)
        {
            return await Task.Run(() =>
            {
                List<TextSearchResultViewModel> results = new List<TextSearchResultViewModel>();
                var searchValue = SearchValues.Create(text);

                foreach (var pageKvp in _index)
                {
                    token.ThrowIfCancellationRequested();
                    List<TextSearchResultViewModel> pageResults = new List<TextSearchResultViewModel>();
                    var textLayer = pdfDocument.Pages[pageKvp.Key - 1].PdfTextLayer;
                    if (textLayer is null)
                    {
                        throw new Exception();
                    }

                    var index = _index[pageKvp.Key];
                    int lastWordFound = 0;

                    while (true)
                    {
                        token.ThrowIfCancellationRequested();
                        var range = index.AsSpan(lastWordFound);
                        if (range.ContainsAny(searchValue))
                        {
                            int current = range.IndexOf(text);
                            if (current == -1)
                            {
                                break;
                            }

                            lastWordFound += current;

                            var wordIndex = index.AsSpan(0, lastWordFound).Count(' ');
                            var w = textLayer[wordIndex];

                            pageResults.Add(new TextSearchResultViewModel()
                            {
                                PageNumber = pageKvp.Key,
                                ItemType = SearchResultItemType.Word,
                                Word = w,
                                WordIndex = wordIndex
                            });

                            lastWordFound += w.Value.Length;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (pageResults.Count > 0)
                    {
                        results.Add(new TextSearchResultViewModel()
                        {
                            PageNumber = pageKvp.Key,
                            Nodes = new ObservableCollection<TextSearchResultViewModel>(pageResults)
                        });
                    }
                }

                return results;
            }, token);
        }
    }
}
