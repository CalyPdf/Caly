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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Data.Core.Plugins;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Caly.Core.Services;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using Caly.Core.Views;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Caly.Core
{
    public partial class App : Application
    {
        public static readonly IMessenger Messenger = StrongReferenceMessenger.Default;

        private readonly FilePipeStream _pipeServer = new();
        private readonly CancellationTokenSource _listeningToFilesCts = new();
        private Task? _listeningToFiles;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private IPdfDocumentsService _pdfDocumentsService;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public static new App? Current => Application.Current as App;

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> instance to resolve application services.
        /// </summary>
        public IServiceProvider? Services { get; private set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // Line below is needed to remove Avalonia data validation.
            // Without this line you will get duplicate validations from both Avalonia and CT
            BindingPlugins.DataValidators.RemoveAt(0);

            // Initialise dependencies
            var services = new ServiceCollection();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel()
                };

                services.AddSingleton(_ => (Visual)desktop.MainWindow);
                services.AddSingleton<IStorageProvider>(_ => desktop.MainWindow.StorageProvider);
                services.AddSingleton<IClipboard>(_ => desktop.MainWindow.Clipboard);

                desktop.Startup += Desktop_Startup;
                desktop.Exit += Desktop_Exit;

#if DEBUG
                desktop.MainWindow.RendererDiagnostics.DebugOverlays = Avalonia.Rendering.RendererDebugOverlays.RenderTimeGraph;
#endif
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = new MainView
                {
                    DataContext = new MainViewModel()
                };

                services.AddSingleton(_ => (Visual)singleViewPlatform.MainView);
                services.AddSingleton<IStorageProvider>(_ => TopLevel.GetTopLevel(singleViewPlatform.MainView)?.StorageProvider);
                services.AddSingleton<IClipboard>(_ => TopLevel.GetTopLevel(singleViewPlatform.MainView)?.Clipboard);
            }
            else if (ApplicationLifetime is null)
            {
                // Design mode or tests
                var mainView = new MainView { DataContext = new MainViewModel() };
                services.AddSingleton(_ => (Visual)mainView);
            }

            services.AddSingleton<ISettingsService, JsonSettingsService>();
            services.AddSingleton<IFilesService, FilesService>();
            services.AddSingleton<IClipboardService, ClipboardService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IPdfDocumentsService, PdfDocumentsService>();

            services.AddScoped<IPdfService, PdfPigPdfService>();
            services.AddScoped<ITextSearchService, LiftiTextSearchService>();
            services.AddScoped<PdfDocumentViewModel>();

            OverrideRegisteredServices(services);

            Services = services.BuildServiceProvider();

            // Load settings
            Services.GetRequiredService<ISettingsService>().Load();

            // We need to make sure IPdfDocumentsService singleton is initiated in UI thread
            _pdfDocumentsService = Services.GetRequiredService<IPdfDocumentsService>();

            // TODO - Check https://github.com/AvaloniaUI/Avalonia/commit/0e014f9cb627d99fb4e1afa389b4c073c836e9b6

            base.OnFrameworkInitializationCompleted();
        }

        public virtual void OverrideRegisteredServices(ServiceCollection services)
        {
            // Mainly for test purpose
        }

        public bool TryBringToFront()
        {
            if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return false;
            }

            if (desktop.MainWindow is null)
            {
                return false;
            }

            try
            {
                desktop.MainWindow.Activate(); // Bring window to front

                Dispatcher.UIThread.Invoke(() =>
                {
                    // Popup from taskbar
                    if (desktop.MainWindow.WindowState == WindowState.Minimized)
                    {
                        desktop.MainWindow.WindowState = WindowState.Normal;
                    }
                });

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async void Desktop_Startup(object? sender, ControlledApplicationLifetimeStartupEventArgs e)
        {
            try
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Startup -= Desktop_Startup;
                }

                _listeningToFiles = Task.Run(ListenToIncomingFiles); // Start listening

                if (e.Args.Length == 0)
                {
                    return;
                }

                await Task.Run(() => OpenDoc(e.Args[0], CancellationToken.None));
            }
            catch (Exception ex)
            {
                ShowExceptionWindowSafely(ex);
                Debug.WriteExceptionToFile(ex);
            }
        }

        private void Desktop_Exit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
        {
            _listeningToFilesCts.Cancel();
            GC.KeepAlive(_listeningToFiles); // TODO - await instead

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Exit -= Desktop_Exit;
            }
        }

        private async Task ListenToIncomingFiles()
        {
            Debug.ThrowOnUiThread();

            try
            {
                await Parallel.ForEachAsync(_pipeServer.ReceivePathAsync(_listeningToFilesCts.Token),
                    _listeningToFilesCts.Token, async (path, ct) =>
                    {
                        if (string.IsNullOrEmpty(path))
                        {
                            return;
                        }

                        await OpenDoc(path, ct);
                    });
            }
            catch (OperationCanceledException)
            {
                // No op
            }
            catch (Exception ex)
            {
                // Critical error...
                ShowExceptionWindowSafely(ex);
                Debug.WriteExceptionToFile(ex);
                throw;
            }
            finally
            {
                await _pipeServer.DisposeAsync();
            }
        }

        private async Task OpenDoc(string? path, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    var dialogService = Services?.GetRequiredService<IDialogService>();
                    dialogService?.ShowNotification("Cannot open file",
                        "The file does not exist or the path is invalid.",
                        NotificationType.Error);

                    return;
                }

                await _pdfDocumentsService.OpenLoadDocument(path, token);
            }
            catch (Exception ex)
            {
                ShowExceptionNotificationSafely(ex);
                Debug.WriteExceptionToFile(ex);
            }
        }

        private void ShowExceptionNotificationSafely(Exception? ex)
        {
            try
            {
                if (ex is null) return;

                var dialogService = Services?.GetRequiredService<IDialogService>();
                dialogService?.ShowNotification("Error", ex.Message, NotificationType.Error);
            }
            catch
            {
                // No op
            }
        }

        private void ShowExceptionWindowSafely(Exception? ex)
        {
            try
            {
                if (ex is null) return;

                var dialogService = Services?.GetRequiredService<IDialogService>();
                dialogService?.ShowExceptionWindow(ex);
            }
            catch
            {
                // No op
            }
        }
    }
}
