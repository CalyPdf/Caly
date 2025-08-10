// Copyright (c) 2025 BobLd
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Caly.Core.Services
{
    internal sealed class PdfDocumentsService : IPdfDocumentsService, IDisposable
    {
        private sealed class PdfDocumentRecord
        {
            public required AsyncServiceScope Scope { get; init; }

            public required PdfDocumentViewModel ViewModel { get; init; }
        }

        private readonly Visual _target;
        private readonly MainViewModel _mainViewModel;
        private readonly IFilesService _filesService;
        private readonly IDialogService _dialogService;

        private readonly Channel<IStorageFile?> _fileChannel;
        private readonly ChannelWriter<IStorageFile?> _channelWriter;
        private readonly ChannelReader<IStorageFile?> _channelReader;

        private readonly ConcurrentDictionary<string, PdfDocumentRecord> _openedFiles = new();

        private async Task ProcessDocumentsQueue(CancellationToken token)
        {
            try
            {
                Debug.ThrowOnUiThread();

                await Parallel.ForEachAsync(_channelReader.ReadAllAsync(token), token, async (d, ct) =>
                {
                    try
                    {
                        if (d is not null)
                        {
                            await OpenLoadDocumentInternal(d, null, ct);
                        }
                    }
                    catch (Exception e)
                    {
                        await _dialogService.ShowExceptionWindowAsync(e);
                    }
                });
            }
            catch (Exception e)
            {
                // Critical error - can't open document anymore
                System.Diagnostics.Debug.WriteLine($"ERROR in WorkerProc {e}");
                Debug.WriteExceptionToFile(e);
                await _dialogService.ShowExceptionWindowAsync(e);
                throw;
            }
        }

        public PdfDocumentsService(Visual target, IFilesService filesService, IDialogService dialogService)
        {
            Debug.ThrowNotOnUiThread();

            _target = target;

            if (_target.DataContext is not MainViewModel mvm)
            {
                throw new ArgumentException("Could not get a valid DataContext for the main window.");
            }

            _mainViewModel = mvm;

            _filesService = filesService ?? throw new NullReferenceException("Missing File Service instance.");
            _dialogService = dialogService ?? throw new NullReferenceException("Missing Dialog Service instance.");

            _fileChannel = Channel.CreateUnbounded<IStorageFile?>(new UnboundedChannelOptions()
            {
                AllowSynchronousContinuations = false, SingleReader = false, SingleWriter = false
            });
            _channelWriter = _fileChannel.Writer;
            _channelReader = _fileChannel.Reader;

            StrongReferenceMessenger.Default.Register<SelectedDocumentChangedMessage>(this, HandleSelectedDocumentChangedMessage);
            StrongReferenceMessenger.Default.Register<LoadPageSizeMessage>(this, HandleLoadPageSizeMessage);
            StrongReferenceMessenger.Default.Register<LoadPageMessage>(this, HandleLoadPageMessage);
            StrongReferenceMessenger.Default.Register<UnloadPageMessage>(this, HandleUnloadPageMessage);
            StrongReferenceMessenger.Default.Register<LoadThumbnailMessage>(this, HandleLoadThumbnailMessage);
            StrongReferenceMessenger.Default.Register<UnloadThumbnailMessage>(this, HandleUnloadThumbnailMessage);
            
            _ = Task.Run(() => ProcessDocumentsQueue(CancellationToken.None));
        }

        private void HandleSelectedDocumentChangedMessage(object r, SelectedDocumentChangedMessage m)
        {
            foreach (var openedFile in _openedFiles)
            {
                if (openedFile.Value.ViewModel.Equals(m.Value))
                {
                    openedFile.Value.ViewModel.SetActive();
                    continue;
                }

                openedFile.Value.ViewModel.SetInactive();
            }
        }

        private static void HandleLoadPageSizeMessage(object r, LoadPageSizeMessage m)
        {
            m.Value.PdfService.EnqueueRequestPageSize(m.Value);
        }
        
        private static void HandleLoadPageMessage(object r, LoadPageMessage m)
        {
            m.Value.PdfService.EnqueueRequestPicture(m.Value);
            m.Value.PdfService.EnqueueRequestTextLayer(m.Value);
        }

        private static void HandleUnloadPageMessage(object r, UnloadPageMessage m)
        {
            m.Value.PdfService.EnqueueRemovePicture(m.Value);
            m.Value.PdfService.EnqueueRemoveTextLayer(m.Value);
        }

        private static void HandleLoadThumbnailMessage(object r, LoadThumbnailMessage m)
        {
            m.Value.PdfService.EnqueueRequestThumbnail(m.Value);
        }

        private static void HandleUnloadThumbnailMessage(object r, UnloadThumbnailMessage m)
        {
            m.Value.PdfService.EnqueueRemoveThumbnail(m.Value);
        }

        public async Task OpenLoadDocument(CancellationToken cancellationToken)
        {
            Debug.ThrowNotOnUiThread();

            IStorageFile? file = await _filesService.OpenPdfFileAsync();

            await Task.Run(() => OpenLoadDocument(file, cancellationToken), cancellationToken);
        }

        public async Task OpenLoadDocument(string? path, CancellationToken cancellationToken)
        {
            Debug.ThrowOnUiThread();

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                // TODO - Log
                return;
            }

            var file = await _filesService.TryGetFileFromPathAsync(path);

            await OpenLoadDocument(file, cancellationToken);
        }

        public async Task OpenLoadDocument(IStorageFile? storageFile, CancellationToken cancellationToken)
        {
            Debug.ThrowOnUiThread();

            await _channelWriter.WriteAsync(storageFile, cancellationToken);
        }

        public async Task OpenLoadDocuments(IEnumerable<IStorageItem?> storageFiles, CancellationToken cancellationToken)
        {
            Debug.ThrowOnUiThread();

            foreach (IStorageItem? item in storageFiles)
            {
                if (item is not IStorageFile file)
                {
                    continue;
                }

                await OpenLoadDocument(file, cancellationToken);
            }
        }

        public async Task CloseUnloadDocument(PdfDocumentViewModel? document)
        {
            Debug.ThrowOnUiThread();

            if (document is null)
            {
                return;
            }

            if (string.IsNullOrEmpty(document.LocalPath))
            {
                throw new Exception($"Invalid {nameof(document.LocalPath)} value for view model.");
            }

            await document.CancelAsync();

            _mainViewModel.PdfDocuments.RemoveSafely(document);

            if (_openedFiles.TryRemove(document.LocalPath, out var docRecord))
            {
                await docRecord.Scope.DisposeAsync();
            }
            else
            {
                // TODO - Log error
            }

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        }

        private async Task OpenLoadDocumentInternal(IStorageFile? storageFile, string? password, CancellationToken cancellationToken)
        {
            Debug.ThrowOnUiThread();

            if (storageFile is null)
            {
                // TODO - Log
                return;
            }

            // TODO - Look into Avalonia bookmark
            // string? id = await storageFile.SaveBookmarkAsync();

            // Check if file is already open
            if (_openedFiles.TryGetValue(storageFile.Path.LocalPath, out var doc))
            {
                // Already open - Activate tab
                // We need a lock to avoid issues with tabs when opening documents in parallel (this might not be needed here though).
                int index = _mainViewModel.PdfDocuments.IndexOfSafely(doc.ViewModel);
                if (index != -1 && _mainViewModel.SelectedDocumentIndex != index)
                {
                    _mainViewModel.SelectedDocumentIndex = index;
                }

                return;
            }

            // We use a named mutex to ensure a single file with the same path is only opened once
            using (new Mutex(true, GetMutexName(storageFile.Path.LocalPath), out bool created))
            {
                if (!created)
                {
                    // Already processing
                    return;
                }

                var scope = App.Current!.Services!.CreateAsyncScope();

                var documentViewModel = scope.ServiceProvider.GetRequiredService<PdfDocumentViewModel>();
                documentViewModel.FileName = $"Opening '{Path.GetFileNameWithoutExtension(storageFile.Path.LocalPath)}'...";
                
                var docRecord = new PdfDocumentRecord()
                {
                    Scope = scope,
                    ViewModel = documentViewModel
                };

                if (_openedFiles.TryAdd(storageFile.Path.LocalPath, docRecord))
                {
                    // Do not await just yet - We need the WaitOpenAsync() to be created but we also
                    // want to add the document to PdfDocuments before opening it.
                    Task<int> openDocTask = documentViewModel.OpenDocument(storageFile, password, cancellationToken);

                    // We need a lock to avoid issues with tabs when opening documents in parallel
                    _mainViewModel.PdfDocuments.AddSafely(documentViewModel);

                    _mainViewModel.SelectedDocumentIndex = Math.Max(0, _mainViewModel.PdfDocuments.Count - 1);

                    int pageCount = 0;
                    try
                    {
                        pageCount = await openDocTask;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteExceptionToFile(ex);
                        Dispatcher.UIThread.Post(() => _mainViewModel.PdfDocuments.RemoveSafely(documentViewModel));
                        _openedFiles.TryRemove(storageFile.Path.LocalPath, out _);
                        throw;
                    }

                    if (pageCount > 0)
                    {
                        // Document opened successfully
                        return;
                    }

                    // Document is not valid
                    Dispatcher.UIThread.Post(() => _mainViewModel.PdfDocuments.RemoveSafely(documentViewModel));
                    _openedFiles.TryRemove(storageFile.Path.LocalPath, out _);
                }

                // TODO - Log error
                await Task.Run(scope.DisposeAsync, CancellationToken.None);
            }
        }

        private static string GetMutexName(string path)
        {
            // The backslash character (\) is reserved for mutex names
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(path)).Replace('\\', '#');
        }

        public void Dispose()
        {
            // https://formatexception.com/2024/03/using-messenger-in-the-communitytoolkit-mvvm/
            StrongReferenceMessenger.Default.UnregisterAll(this);
        }
    }
}
