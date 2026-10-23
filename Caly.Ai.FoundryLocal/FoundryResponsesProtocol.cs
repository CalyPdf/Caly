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
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Caly.Core.Models.Ai;

namespace Caly.Ai.FoundryLocal;

/// <summary>
/// Foundry Local's Responses API as its vision sample uses it: a user message of
/// <c>input_text</c> and <c>input_image</c> parts (the image as <c>image_data</c> and
/// <c>media_type</c>, not OpenAI's <c>image_url</c>), answered by server-sent events.
/// </summary>
internal static class FoundryResponsesProtocol
{
    private const int MaxOutputTokens = 8192;
    private const string DataPrefix = "data: ";

    /// <summary>
    /// Instructions and each page become parts of one user message: the first part carries the
    /// system and user prompts, then every page contributes its text and, if it has one, its image.
    /// </summary>
    public static string BuildBody(string modelId, AiRequest request)
    {
        var parts = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "input_text",
                ["text"] = request.SystemPrompt + "\n\n" + request.UserPrompt
            }
        };

        foreach (var page in request.Pages)
        {
            parts.Add(new JsonObject
            {
                ["type"] = "input_text",
                ["text"] = $"Page {page.PageNumber}:\n{page.Text}"
            });

            if (page.Png is not null)
            {
                parts.Add(new JsonObject
                {
                    ["type"] = "input_image",
                    ["image_data"] = Convert.ToBase64String(page.Png),
                    ["media_type"] = "image/png"
                });
            }
        }

        var body = new JsonObject
        {
            ["model"] = modelId,
            ["input"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "message",
                    ["role"] = "user",
                    ["content"] = parts
                }
            },
            ["max_output_tokens"] = MaxOutputTokens,
            ["stream"] = true
        };

        return body.ToJsonString();
    }

    /// <summary>
    /// Reads the text fragments of a server-sent event stream, up to <c>[DONE]</c>. Lines that
    /// are not data, or not JSON, are keep-alives and skipped.
    /// </summary>
    public static async IAsyncEnumerable<string> ReadDeltasAsync(Stream stream,
        [EnumeratorCancellation] CancellationToken token)
    {
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(token).ConfigureAwait(false) is { } line)
        {
            if (!line.StartsWith(DataPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string data = line[DataPrefix.Length..];
            if (data == "[DONE]")
            {
                yield break;
            }

            string? delta = TryReadDelta(data);
            if (delta is not null)
            {
                yield return delta;
            }
        }
    }

    private static string? TryReadDelta(string data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("type", out var type) && type.GetString() == "response.output_text.delta" &&
                root.TryGetProperty("delta", out var delta))
            {
                return delta.GetString();
            }
        }
        catch (JsonException)
        {
            // Not an event we know.
        }

        return null;
    }
}
