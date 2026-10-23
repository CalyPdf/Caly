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

namespace Caly.Core.Models.Ai;

public enum AiModelState : byte
{
    NotReady = 0,
    Downloading = 1,
    Loading = 2,
    Ready = 3,
    Error = 4
}

/// <param name="Progress">0..100 while <see cref="AiModelState.Downloading"/>, otherwise 0.</param>
public readonly record struct AiModelStatus(AiModelState State, double Progress = 0, string? Message = null);

/// <summary>
/// What the model is given for one page: its text and, when the action needs it, a rendered PNG.
/// </summary>
public sealed record AiPageContent(int PageNumber, string Text, byte[]? Png);

public sealed record AiRequest(string SystemPrompt, string UserPrompt, System.Collections.Generic.IReadOnlyList<AiPageContent> Pages);

public enum AiAction : byte
{
    Summarise = 0,
    ExtractTables = 1,
    ExtractCharts = 2
}

public enum AiScope : byte
{
    CurrentPage = 0,
    PageRange = 1,
    WholeDocument = 2
}
