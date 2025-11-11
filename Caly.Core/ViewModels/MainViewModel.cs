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

using Avalonia.Collections;
using Caly.Core.Services;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Tabalonia.Controls;

namespace Caly.Core.ViewModels
{
    public sealed partial class MainViewModel : ViewModelBase
    {
        private readonly IDisposable _documentCollectionDisposable;

        public ObservableCollection<PdfDocumentViewModel> PdfDocuments { get; } = new();

        [ObservableProperty] private int _selectedDocumentIndex;

        [ObservableProperty] private bool _isSettingsPaneOpen;

        public string Version => CalyExtensions.CalyVersion;

        partial void OnSelectedDocumentIndexChanged(int oldValue, int newValue)
        {
            System.Diagnostics.Debug.WriteLine($"Selected Document Index changed from {oldValue} to {newValue}.");
            var currentDoc = GetCurrentPdfDocument();
            if (currentDoc is null)
            {
                return;
            }

            App.Messenger.Send(new SelectedDocumentChangedMessage(currentDoc));
        }

        public MainViewModel()
        {
            // TODO - Dispose to unsubscribe
            _documentCollectionDisposable = PdfDocuments
                .GetWeakCollectionChangedObservable()
                .ObserveOn(Scheduler.Default)
                .Subscribe(async e =>
                {
                    Debug.ThrowOnUiThread();

                    // NB: Tabalonia uses a Remove + Add when moving tabs
                    try
                    {
                        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems?.Count > 0)
                        {
                            foreach (var newDoc in e.NewItems.OfType<PdfDocumentViewModel>())
                            {
                                if (newDoc.WaitOpenAsync is null)
                                {
                                    throw new Exception("WaitOpenAsync is null");
                                }

                                await newDoc.WaitOpenAsync; // Make sure the doc is open before proceeding
                                await Task.WhenAll(newDoc.LoadPagesTask, newDoc.LoadBookmarksTask, newDoc.LoadPropertiesTask);
                            }
                        }
                        else if (e.Action == NotifyCollectionChangedAction.Remove)
                        {
                            if (PdfDocuments.Count == 0)
                            {
                                // We want to clear any possible reference to the last PdfDocumentViewModel.
                                // The collection keeps a reference of the last document in e.OldItems
                                // We trigger a NotifyCollectionChangedAction.Reset to flush
                                PdfDocuments.Clear();
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // No op
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteExceptionToFile(ex);
                        Dispatcher.UIThread.Post(() => Exception = new ExceptionViewModel(ex));
                    }
                });
        }

        private PdfDocumentViewModel? GetCurrentPdfDocument()
        {
            try
            {
                return (SelectedDocumentIndex < 0 || PdfDocuments.Count == 0) ? null : PdfDocuments[SelectedDocumentIndex];
            }
            catch (Exception e)
            {
                Debug.WriteExceptionToFile(e);
                return null;
            }
        }

        [RelayCommand]
        private async Task OpenFile(CancellationToken token)
        {
            try
            {
                var pdfDocumentsService = App.Current?.Services?.GetRequiredService<IPdfDocumentsService>();
                if (pdfDocumentsService is null)
                {
                    throw new NullReferenceException($"Missing {nameof(IPdfDocumentsService)} instance.");
                }

                await pdfDocumentsService.OpenLoadDocument(token);
            }
            catch (OperationCanceledException)
            {
                // No op
            }
            catch (Exception ex)
            {
                Debug.WriteExceptionToFile(ex);
                Dispatcher.UIThread.Post(() => Exception = new ExceptionViewModel(ex));
            }
        }
        
        [RelayCommand]
        private async Task CloseTab(object tabItem)
        {
            // TODO - Finish proper dispose / unload of document on close 
            if (((DragTabItem)tabItem)?.DataContext is PdfDocumentViewModel vm)
            {
                await CloseDocumentInternal(vm);
            }
        }

        [RelayCommand]
        private async Task CloseDocument(CancellationToken token)
        {
            PdfDocumentViewModel? vm = GetCurrentPdfDocument();
            if (vm is null)
            {
                return;
            }
            await CloseDocumentInternal(vm);
        }

        private static async Task CloseDocumentInternal(PdfDocumentViewModel vm)
        {
            IPdfDocumentsService pdfDocumentsService = App.Current?.Services?.GetRequiredService<IPdfDocumentsService>()
                ?? throw new NullReferenceException($"Missing {nameof(IPdfDocumentsService)} instance.");

            await Task.Run(() => pdfDocumentsService.CloseUnloadDocument(vm));
        }

        [RelayCommand]
        private void ActivateSearchTextTab()
        {
            GetCurrentPdfDocument()?.ActivateSearchTextTabCommand.Execute(null);
        }

        [RelayCommand]
        private Task CopyText(CancellationToken token)
        {
            PdfDocumentViewModel? vm = GetCurrentPdfDocument();
            return vm is null ? Task.CompletedTask : vm.CopyTextCommand.ExecuteAsync(null);
        }

        [RelayCommand]
        private void ActivateNextDocument()
        {
            int lastIndex = PdfDocuments.Count - 1;

            if (lastIndex <= 0)
            {
                return;
            }

            int newIndex = SelectedDocumentIndex + 1;

            if (newIndex > lastIndex)
            {
                newIndex = 0;
            }
            SelectedDocumentIndex = newIndex;
        }

        [RelayCommand]
        private void ActivatePreviousDocument()
        {
            int lastIndex = PdfDocuments.Count - 1;

            if (lastIndex <= 0)
            {
                return;
            }

            int newIndex = SelectedDocumentIndex - 1;

            if (newIndex < 0)
            {
                newIndex = lastIndex;
            }
            SelectedDocumentIndex = newIndex;
        }
    }
}