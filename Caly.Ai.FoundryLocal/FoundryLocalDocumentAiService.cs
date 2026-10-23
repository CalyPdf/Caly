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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Caly.Core.Models.Ai;
using Caly.Core.Services.Interfaces;

namespace Caly.Ai.FoundryLocal;

/// <summary>
/// <see cref="IDocumentAiService"/> backed by Foundry Local. Nothing is downloaded or started
/// until <see cref="EnsureReadyAsync"/> is first called.
/// </summary>
public sealed class FoundryLocalDocumentAiService : IDocumentAiService, IAsyncDisposable
{
    private readonly IFoundryModelHost _host;
    private readonly string _modelId;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private FoundryPreparedModel? _prepared;

    public FoundryLocalDocumentAiService(string modelId)
        : this(new FoundryHost(), modelId, new HttpClient { Timeout = Timeout.InfiniteTimeSpan })
    {
    }

    internal FoundryLocalDocumentAiService(IFoundryModelHost host, string modelId, HttpClient http)
    {
        _host = host;
        _modelId = modelId;
        _http = http;
    }

    public AiModelStatus Status { get; private set; } = new(AiModelState.NotReady);

    public event EventHandler? StatusChanged;

    private void SetStatus(AiModelStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task EnsureReadyAsync(CancellationToken token)
    {
        if (_prepared is not null)
        {
            return;
        }

        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_prepared is not null)
            {
                return;
            }

            try
            {
                _prepared = await _host.PrepareAsync(_modelId,
                    percent => SetStatus(new AiModelStatus(AiModelState.Downloading, percent)),
                    () => SetStatus(new AiModelStatus(AiModelState.Loading)),
                    token).ConfigureAwait(false);

                SetStatus(new AiModelStatus(AiModelState.Ready));
            }
            catch (OperationCanceledException)
            {
                SetStatus(new AiModelStatus(AiModelState.NotReady));
                throw;
            }
            catch (Exception e)
            {
                SetStatus(new AiModelStatus(AiModelState.Error, 0,
                    $"Foundry Local could not start the model: {e.Message} " +
                    $"Check that Foundry Local is installed, for example by running: foundry model run {_modelId}"));
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async IAsyncEnumerable<string> CompleteAsync(AiRequest request, [EnumeratorCancellation] CancellationToken token)
    {
        await EnsureReadyAsync(token).ConfigureAwait(false);
        var prepared = _prepared!;

        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(prepared.BaseAddress, "responses"))
        {
            Content = new StringContent(FoundryResponsesProtocol.BuildBody(prepared.ModelId, request), Encoding.UTF8, "application/json")
        };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "notneeded");

        using var response = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await foreach (string delta in FoundryResponsesProtocol.ReadDeltasAsync(stream, token).ConfigureAwait(false))
        {
            yield return delta;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _host.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
