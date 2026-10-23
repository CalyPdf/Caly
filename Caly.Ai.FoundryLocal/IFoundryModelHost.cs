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
using System.Threading;
using System.Threading.Tasks;

namespace Caly.Ai.FoundryLocal;

/// <summary>
/// A model that is loaded and served by Foundry Local's local web service.
/// </summary>
/// <param name="BaseAddress">The service's OpenAI-compatible root, ending with <c>/v1/</c>.</param>
/// <param name="ModelId">The id to put in the <c>model</c> field of a request.</param>
internal sealed record FoundryPreparedModel(Uri BaseAddress, string ModelId);

/// <summary>
/// The part of Foundry Local the service depends on, so the service's state handling can be
/// tested without the SDK or a model.
/// </summary>
internal interface IFoundryModelHost : IAsyncDisposable
{
    /// <summary>
    /// Starts Foundry Local, downloads the model if it is not cached, loads it and starts the web service.
    /// </summary>
    /// <param name="downloadProgress">Called with 0..100.</param>
    /// <param name="loading">Called when the download is over and the model starts loading.</param>
    Task<FoundryPreparedModel> PrepareAsync(string modelId, Action<double> downloadProgress, Action loading,
        CancellationToken token);
}
