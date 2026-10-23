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
using System.Text.Json;
using Caly.Core.Models.Ai;

namespace Caly.Core.Services.Ai;

/// <summary>
/// Reads what a small model answered to an extraction prompt. The model is not trusted to
/// follow the format, so anything unreadable degrades to a text item and nothing throws.
/// </summary>
internal static class AiResultParser
{
    public static IReadOnlyList<AiResultItem> ParseExtraction(string reply, int fallbackPage)
    {
        string trimmed = reply.Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        if (TryParseItems(ExtractJson(trimmed), fallbackPage, out var items))
        {
            return items;
        }

        return [new AiResultItem(AiItemKind.Text, fallbackPage, null, trimmed)];
    }

    public static string ToCsvFile(IEnumerable<AiResultItem> items)
    {
        var sb = new StringBuilder();
        foreach (var item in items)
        {
            if (sb.Length > 0)
            {
                sb.Append("\n\n");
            }

            sb.Append(item.Content.Replace("\r\n", "\n").TrimEnd('\n'));
        }

        return sb.ToString();
    }

    private static string ExtractJson(string text)
    {
        int fence = text.IndexOf("```", StringComparison.Ordinal);
        if (fence >= 0)
        {
            int start = text.IndexOf('\n', fence);
            if (start >= 0)
            {
                int end = text.IndexOf("```", start, StringComparison.Ordinal);
                text = end >= 0 ? text[(start + 1)..end] : text[(start + 1)..];
            }
        }

        int open = text.IndexOf('{');
        int close = text.LastIndexOf('}');
        return open >= 0 && close > open ? text[open..(close + 1)] : text;
    }

    private static bool TryParseItems(string json, int fallbackPage, out IReadOnlyList<AiResultItem> items)
    {
        items = [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("items", out var array) ||
                array.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var result = new List<AiResultItem>();
            foreach (var element in array.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var kind = element.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String
                    ? k.GetString()?.ToLowerInvariant() switch
                    {
                        "table" => AiItemKind.Table,
                        "chart" => AiItemKind.Chart,
                        _ => AiItemKind.Text
                    }
                    : AiItemKind.Text;

                int page = element.TryGetProperty("page", out var p) && p.TryGetInt32(out int n) && n > 0 ? n : fallbackPage;
                string content = element.TryGetProperty("csv", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? string.Empty : string.Empty;
                AiBox? box = element.TryGetProperty("box", out var b) ? ReadBox(b) : null;

                result.Add(new AiResultItem(kind, page, box, content));
            }

            items = result;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static AiBox? ReadBox(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() != 4)
        {
            return null;
        }

        Span<double> v = stackalloc double[4];
        int i = 0;
        foreach (var number in element.EnumerateArray())
        {
            if (!number.TryGetDouble(out double d) || !double.IsFinite(d))
            {
                return null;
            }

            v[i++] = Math.Clamp(d, 0, 1);
        }

        double x0 = Math.Min(v[0], v[2]), x1 = Math.Max(v[0], v[2]);
        double y0 = Math.Min(v[1], v[3]), y1 = Math.Max(v[1], v[3]);
        return x1 > x0 && y1 > y0 ? new AiBox(x0, y0, x1, y1) : null;
    }
}
