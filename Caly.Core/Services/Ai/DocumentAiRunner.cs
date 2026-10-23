// Copyright (c) BobLd
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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Caly.Core.Models.Ai;
using Caly.Core.Services.Interfaces;

namespace Caly.Core.Services.Ai;

/// <summary>
/// Runs one AI action over a set of pages: gathers the pages, splits them to fit the model's
/// context, asks the service, and puts the parts back together.
/// </summary>
internal sealed class DocumentAiRunner(IDocumentAiService service, AiPageContentProvider provider, int chunkBudgetTokens)
{
    public DocumentAiRunner(IDocumentAiService service, AiPageContentProvider provider)
        : this(service, provider, AiTokenEstimator.ContextTokens * 3 / 4)
    {
    }

    /// <summary>
    /// Streams a summary. A document too big for one request is summarised part by part, each
    /// part streamed as it comes, then once more as a whole.
    /// </summary>
    public async IAsyncEnumerable<string> RunTextAsync(AiAction action, IReadOnlyList<int> pages,
        [EnumeratorCancellation] CancellationToken token)
    {
        await service.EnsureReadyAsync(token).ConfigureAwait(false);

        var contents = await GatherAsync(action, pages, token).ConfigureAwait(false);
        var chunks = AiPageChunker.Chunk(contents, chunkBudgetTokens);

        if (chunks.Count <= 1)
        {
            await foreach (string fragment in service.CompleteAsync(AiPrompts.Build(action, contents), token).ConfigureAwait(false))
            {
                yield return fragment;
            }

            yield break;
        }

        var partSummaries = new List<string>(chunks.Count);
        for (int i = 0; i < chunks.Count; i++)
        {
            token.ThrowIfCancellationRequested();

            var chunk = chunks[i];
            yield return chunk.Count == 1
                ? $"**Page {chunk[0].PageNumber}**\n\n"
                : $"**Pages {chunk[0].PageNumber}-{chunk[^1].PageNumber}**\n\n";

            var part = new StringBuilder();
            await foreach (string fragment in service.CompleteAsync(AiPrompts.Build(action, chunk), token).ConfigureAwait(false))
            {
                part.Append(fragment);
                yield return fragment;
            }

            partSummaries.Add(part.ToString());
            yield return "\n\n";
        }

        token.ThrowIfCancellationRequested();
        yield return "**Whole document**\n\n";
        await foreach (string fragment in service.CompleteAsync(AiPrompts.BuildReduce(partSummaries), token).ConfigureAwait(false))
        {
            yield return fragment;
        }
    }

    /// <summary>
    /// Extracts tables or chart data. Reports the model's text as it streams.
    /// </summary>
    public Task<IReadOnlyList<AiResultItem>> RunExtractionAsync(AiAction action, IReadOnlyList<int> pages,
        IProgress<string>? stream, CancellationToken token)
    {
        return Task.Run(async () =>
        {
            await service.EnsureReadyAsync(token).ConfigureAwait(false);

            var contents = await GatherAsync(action, pages, token).ConfigureAwait(false);
            var items = new List<AiResultItem>();

            foreach (var chunk in AiPageChunker.Chunk(contents, chunkBudgetTokens))
            {
                token.ThrowIfCancellationRequested();

                var reply = new StringBuilder();
                await foreach (string fragment in service.CompleteAsync(AiPrompts.Build(action, chunk), token).ConfigureAwait(false))
                {
                    reply.Append(fragment);
                    stream?.Report(fragment);
                }

                items.AddRange(AiResultParser.ParseExtraction(reply.ToString(), chunk[0].PageNumber));
            }

            return (IReadOnlyList<AiResultItem>)items;
        });
    }

    private async Task<List<AiPageContent>> GatherAsync(AiAction action, IReadOnlyList<int> pages, CancellationToken token)
    {
        var contents = new List<AiPageContent>(pages.Count);
        foreach (int pageNumber in pages)
        {
            token.ThrowIfCancellationRequested();

            bool image = action != AiAction.Summarise;
            var content = await provider.GetAsync(pageNumber, image, token).ConfigureAwait(false);

            // A scanned page has no text to summarise: the model has to read the picture.
            if (!image && string.IsNullOrWhiteSpace(content.Text))
            {
                content = await provider.GetAsync(pageNumber, true, token).ConfigureAwait(false);
            }

            contents.Add(content);
        }

        return contents;
    }
}
