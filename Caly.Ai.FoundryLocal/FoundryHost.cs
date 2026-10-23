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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging.Abstractions;

namespace Caly.Ai.FoundryLocal;

/// <summary>
/// The Foundry Local SDK, as used by the samples (native-chat-completions for the catalog and
/// model lifecycle, foundry-local-web-server-responses-vision for the web service).
/// </summary>
internal sealed class FoundryHost : IFoundryModelHost
{
    private IModel? _model;
    private FoundryLocalManager? _manager;
    private bool _serviceStarted;

    // The web service's address is part of the configuration the manager was created with, so a
    // retry after a partial failure has to reuse it, not pick a new port.
    private string? _url;

    public async Task<FoundryPreparedModel> PrepareAsync(string modelId, Action<double> downloadProgress, Action loading,
        CancellationToken token)
    {
        // The manager is a process-wide singleton: a retry after a failure must not create it again.
        if (_manager is null)
        {
            _url = $"http://127.0.0.1:{GetFreeLoopbackPort()}";

            var config = new Configuration
            {
                AppName = "caly",
                LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Warning,
                Web = new Configuration.WebService { Urls = _url }
            };

            try
            {
                await FoundryLocalManager.CreateAsync(config, NullLogger.Instance, token).ConfigureAwait(false);
            }
            catch (InvalidOperationException) when (FoundryLocalManager.IsInitialized)
            {
                // Created earlier in this process.
            }

            _manager = FoundryLocalManager.Instance;
        }

        // Execution providers (GPU back-ends) are downloaded once, then reported as 0..100 like the model.
        await _manager.DownloadAndRegisterEpsAsync((_, percent) => downloadProgress(percent), token).ConfigureAwait(false);

        var catalog = await _manager.GetCatalogAsync(token).ConfigureAwait(false);
        var model = await catalog.GetModelAsync(modelId, token).ConfigureAwait(false)
                    ?? await catalog.GetModelVariantAsync(modelId, token).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Model '{modelId}' is not in the Foundry Local catalog.");

        if (!await model.IsCachedAsync(token).ConfigureAwait(false))
        {
            await model.DownloadAsync(percent => downloadProgress(percent), token).ConfigureAwait(false);
        }

        loading();
        await model.LoadAsync(token).ConfigureAwait(false);
        _model = model;

        if (!_serviceStarted)
        {
            await _manager.StartWebServiceAsync(token).ConfigureAwait(false);
            _serviceStarted = true;
        }

        return new FoundryPreparedModel(new Uri(_url! + "/v1/"), model.Id);
    }

    private static int GetFreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_serviceStarted && _manager is not null)
            {
                await _manager.StopWebServiceAsync().ConfigureAwait(false);
            }

            if (_model is not null)
            {
                await _model.UnloadAsync().ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // Shutting down: nothing useful to do with a failure to tidy up.
        }
    }
}
