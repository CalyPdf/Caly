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
using System.Threading;
using System.Threading.Tasks;
using Caly.Core.Models.Ai;

namespace Caly.Core.Services.Interfaces;

/// <summary>
/// A local language model that works on page content. Registered by the platform head that
/// supports one; when none is registered the AI tab is hidden.
/// </summary>
public interface IDocumentAiService
{
    AiModelStatus Status { get; }

    event EventHandler? StatusChanged;

    /// <summary>
    /// Downloads and loads the model if needed. Never called before the user asks for an AI action.
    /// Failure is reported through <see cref="Status"/> and rethrown; a later call retries.
    /// </summary>
    Task EnsureReadyAsync(CancellationToken token);

    /// <summary>
    /// Streams the reply, one text fragment at a time.
    /// </summary>
    IAsyncEnumerable<string> CompleteAsync(AiRequest request, CancellationToken token);
}
