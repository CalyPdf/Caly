using Caly.Core.Services.Interfaces;
using Caly.Core.ViewModels;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        {
            _index.Clear();
        }

        public async Task BuildPdfDocumentIndex(PdfDocumentViewModel pdfDocument, IProgress<int> progress, CancellationToken token)
        {
            int done = 0;

            var options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = token
            };

            await Parallel.ForEachAsync(pdfDocument.Pages, options, async (p, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                
                await Task.Run(async () =>
                {
                    var textLayer = p.PdfTextLayer;
                    if (textLayer is null)
                    {
                        await p.SetPageTextLayerImmediate(ct);
                        textLayer = p.PdfTextLayer;
                        p.RemovePageTextLayerImmediate();
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

            var span = text.AsSpan();
            count = span.Count(WordSeparator) + 1;

            if (span.StartsWith(WordSeparator))
            {
                count--;
            }

            if (span.EndsWith(WordSeparator))
            {
                count--;
            }

            return text;
        }

        private static ReadOnlyMemory<char> GetSampleText(string pageText, int startIndex, int length)
        {
            int sampleStart = Math.Max(0, startIndex - 10);
            int sampleLength = Math.Min(length + 20, pageText.Length - sampleStart);
            return pageText.AsMemory(sampleStart, sampleLength);
        }

        public IEnumerable<TextSearchResultViewModel> Search(PdfDocumentViewModel pdfDocument, string text, IReadOnlyCollection<int> pagesToSkip, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            token.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            // TODO - Move the below out of here as it reruns while indexing
            text = CleanText(text, out int count);
            // END TODO

            var searchValue = SearchValues.Create([text], StringComparison.OrdinalIgnoreCase);

            foreach (var pageKvp in _index)
            {
                token.ThrowIfCancellationRequested();

                if (pagesToSkip.Contains(pageKvp.Key))
                {
                    continue;
                }

                string pageText = _index[pageKvp.Key];
                int lastSpanIndex = 0;

                var pageResults = new List<TextSearchResultViewModel>();

                /*
                 * TODO - If the page text start with the word but the word starts with a space.
                 * The search won't be pick up
                 */

                while (lastSpanIndex < pageText.Length)
                {
                    token.ThrowIfCancellationRequested();

                    int currentSpanIndex = pageText.AsSpan(lastSpanIndex).IndexOfAny(searchValue);
                    if (currentSpanIndex == -1)
                    {
                        break;
                    }

                    lastSpanIndex += currentSpanIndex;
                    
                    var wordIndex = pageText.AsSpan(0, lastSpanIndex).Count(WordSeparator);

                    pageResults.Add(new TextSearchResultViewModel()
                    {
                        PageNumber = pageKvp.Key,
                        ItemType = SearchResultItemType.Word,
                        WordIndex = wordIndex,
                        WordCount = count,
                        SampleText = GetSampleText(pageText, lastSpanIndex, 20)
                    });

                    lastSpanIndex += text.Length;
                }

                if (pageResults.Count > 0)
                {
                    yield return new TextSearchResultViewModel()
                    {
                        ItemType = SearchResultItemType.Unspecified,
                        PageNumber = pageKvp.Key,
                        Nodes = pageResults
                    };
                }
            }
        }
    }
}
