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

using System.Collections.Generic;
using Caly.Core.Models.Ai;

namespace Caly.Core.Services.Ai;

internal static class AiPageChunker
{
    /// <summary>
    /// Packs consecutive pages into chunks of at most <paramref name="budgetTokens"/>; a page
    /// bigger than the budget is never dropped, it gets a chunk of its own.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<AiPageContent>> Chunk(IReadOnlyList<AiPageContent> pages, int budgetTokens)
    {
        var chunks = new List<IReadOnlyList<AiPageContent>>();
        var current = new List<AiPageContent>();
        int used = 0;

        foreach (var page in pages)
        {
            int cost = AiTokenEstimator.Estimate(page);
            if (current.Count > 0 && used + cost > budgetTokens)
            {
                chunks.Add(current);
                current = [];
                used = 0;
            }

            current.Add(page);
            used += cost;
        }

        if (current.Count > 0)
        {
            chunks.Add(current);
        }

        return chunks;
    }
}
