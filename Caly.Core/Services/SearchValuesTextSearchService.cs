using Caly.Core.Services.Interfaces;
using Caly.Core.ViewModels;
using Caly.Pdf.Models;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Caly.Core.Services
{
    internal class SearchValuesTextSearchService : ITextSearchService
    {
        private const char WordSeparator = ' ';
        
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

                    _index[p.PageNumber] = string.Join(WordSeparator, textLayer.Select(w => w.Value));
                    progress.Report(Interlocked.Add(ref done, 1));
                }, ct);
            });
        }

        private static string CleanText(string text, out int count)
        {
            List<int> separators = new List<int>();
            for (int i = 0; i < text.Length; ++i)
            {
                if (char.IsPunctuation(text[i]))
                {
                    separators.Add(i);
                }
            }

            if (separators.Count > 0)
            {
                int start = 0;
                StringBuilder sb = new StringBuilder();

                foreach (var i in separators)
                {
                    if (i != 0)
                    {
                        sb.Append(text, start, i - start);
                        sb.Append(WordSeparator);
                    }

                    sb.Append(text, i, 1);

                    if (i == text.Length - 1)
                    {
                        start = i + 1;
                        break;
                    }

                    sb.Append(WordSeparator);
                    start = i + 1;
                }

                sb.Append(text, start, text.Length - start);
                text = sb.ToString();
            }

            count = text.AsSpan().Count(WordSeparator) + 1;

            return text;
        }

        public async Task<IEnumerable<TextSearchResultViewModel>> Search(PdfDocumentViewModel pdfDocument, string text, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            token.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(text))
            {
                return [];
            }

            return await Task.Run(() =>
            {
                // TODO - Move the below out of here as it reruns while indexing
                text = CleanText(text, out int count);
                // END TODO
                
                var results = new List<TextSearchResultViewModel>();
                var searchValue = SearchValues.Create([text], StringComparison.OrdinalIgnoreCase);
                
                foreach (var pageKvp in _index)
                {
                    token.ThrowIfCancellationRequested();

                    var pageResults = new List<TextSearchResultViewModel>();
                    PdfTextLayer? textLayer = pdfDocument.Pages[pageKvp.Key - 1].PdfTextLayer;
                    if (textLayer is null)
                    {
                        throw new NullReferenceException($"Text layer for page {pageKvp.Key} is null.");
                    }

                    string index = _index[pageKvp.Key];
                    int lastWordFound = 0;

                    while (lastWordFound < index.Length)
                    {
                        token.ThrowIfCancellationRequested();
                        
                        int current = index.AsSpan(lastWordFound).IndexOfAny(searchValue);
                        if (current == -1)
                        {
                            break;
                        }

                        lastWordFound += current;

                        var wordIndex = index.AsSpan(0, lastWordFound).Count(WordSeparator);

                        PdfWord[] words = new PdfWord[count];
                        var firstWord = textLayer[wordIndex];
                        words[0] = firstWord;

                        for (int i = 1; i < words.Length; ++i)
                        {
                            words[i] = textLayer[wordIndex + i];
                        }

                        pageResults.Add(new TextSearchResultViewModel()
                        {
                            PageNumber = pageKvp.Key,
                            ItemType = SearchResultItemType.Word,
                            Word = words,
                            WordIndex = wordIndex
                        });

                        lastWordFound += firstWord.Value.Length;
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
