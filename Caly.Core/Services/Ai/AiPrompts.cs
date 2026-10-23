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
using System.Text;
using Caly.Core.Models.Ai;

namespace Caly.Core.Services.Ai;

/// <summary>
/// The model is given each page as its text followed by its image; the service lays that out
/// (see <see cref="Interfaces.IDocumentAiService"/>), so prompts only say what to do with it.
/// </summary>
internal static class AiPrompts
{
    private const string ExtractionFormat =
        "Reply with JSON only, no other text, in this shape: " +
        "{\"items\":[{\"kind\":\"{KIND}\",\"page\":<page number>,\"box\":[x0,y0,x1,y1],\"csv\":\"<CSV text>\"}]}. " +
        "box is the position of the {KIND} on the page as fractions of the page width and height " +
        "(0 to 1, origin at the top left). csv holds the data as CSV, one header row first, " +
        "cells containing commas or quotes in double quotes. Use {\"items\":[]} when there is none.";

    public static AiRequest Build(AiAction action, IReadOnlyList<AiPageContent> pages)
    {
        return action switch
        {
            AiAction.Summarise => new AiRequest(
                "You summarise documents accurately and concisely. Reply in markdown.",
                "Summarise and analyse the following pages: main topic, key points, figures worth noting.",
                pages),

            AiAction.ExtractTables => new AiRequest(
                "You extract tables from document pages.",
                "Extract every table from the following pages. " + ExtractionFormat.Replace("{KIND}", "table"),
                pages),

            AiAction.ExtractCharts => new AiRequest(
                "You read data points off charts and graphs in document pages.",
                "Extract the data points of every chart or graph in the following pages: one row per point, " +
                "with the series name, the x value and the y value. " + ExtractionFormat.Replace("{KIND}", "chart"),
                pages),

            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };
    }

    /// <summary>
    /// Combines the summaries of consecutive parts of a document that did not fit in one request.
    /// </summary>
    public static AiRequest BuildReduce(IReadOnlyList<string> partSummaries)
    {
        var sb = new StringBuilder("The following are summaries of consecutive parts of one document. " +
                                   "Write one summary of the whole document.\n");
        for (int i = 0; i < partSummaries.Count; i++)
        {
            sb.Append("\n--- Part ").Append(i + 1).Append(" ---\n").Append(partSummaries[i]).Append('\n');
        }

        return new AiRequest("You summarise documents accurately and concisely. Reply in markdown.", sb.ToString(), []);
    }
}
