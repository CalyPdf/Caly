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
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Caly.Core.Models.Ai;
using Caly.Core.Services.Ai;
using Caly.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Caly.Core.ViewModels;

/// <summary>
/// The AI tab of one document: what to run, on which pages, and what came back.
/// </summary>
public sealed partial class AiPanelViewModel : ObservableObject, IDisposable
{
    private readonly IDocumentAiService _service;
    private readonly DocumentAiRunner _runner;
    private readonly string _baseFileName;
    private readonly Func<int> _currentPage;
    private readonly Func<int> _pageCount;
    private readonly Func<string, Task> _copy;
    private readonly Func<string, byte[], Task> _save;
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    private CancellationTokenSource? _cts;
    private bool _disposed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    private AiAction _action;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    private AiScope _scope;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    private int _rangeFrom = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    private int _rangeTo = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private AiModelStatus _status;

    [ObservableProperty] private string _outputText = string.Empty;

    [ObservableProperty] private AiResultItem? _selectedItem;

    /// <summary>
    /// Set when a run failed for a reason the service did not report through <see cref="Status"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string? _lastError;

    public ObservableCollection<AiResultItem> Items { get; } = [];

    /// <param name="currentPage">The page number the reader is on.</param>
    /// <param name="copy">Puts text on the clipboard.</param>
    /// <param name="save">Asks the user where to save a file, then writes the bytes; arguments are the suggested file name and the data.</param>
    internal AiPanelViewModel(IDocumentAiService service, IPdfDocumentService pdfService,
        Func<int> currentPage, Func<int> pageCount, Func<string, Task> copy, Func<string, byte[], Task> save)
    {
        _service = service;
        _runner = new DocumentAiRunner(service, new AiPageContentProvider(pdfService));
        _baseFileName = Path.GetFileNameWithoutExtension(pdfService.FileName) is { Length: > 0 } name ? name : "document";
        _currentPage = currentPage;
        _pageCount = pageCount;
        _copy = copy;
        _save = save;

        _status = service.Status;
        _service.StatusChanged += OnServiceStatusChanged;
    }

    public string StatusText => LastError is { } error && Status.State != AiModelState.Error
        ? error
        : Status.State switch
        {
            AiModelState.NotReady => "The model loads the first time you run an action.",
            AiModelState.Downloading => $"Downloading the model... {Status.Progress:F0}%",
            AiModelState.Loading => "Loading the model...",
            AiModelState.Ready => "Model ready.",
            AiModelState.Error => Status.Message ?? "The model could not be started.",
            _ => string.Empty
        };

    /// <summary>
    /// The scopes an action offers. All three suit every action for now; the method is where a
    /// restriction would go.
    /// </summary>
    public static IReadOnlyList<AiScope> ScopesFor(AiAction action) =>
        [AiScope.CurrentPage, AiScope.PageRange, AiScope.WholeDocument];

    private bool CanRun()
    {
        if (IsBusy)
        {
            return false;
        }

        if (Scope != AiScope.PageRange)
        {
            return _pageCount() > 0;
        }

        return RangeFrom >= 1 && RangeFrom <= RangeTo && RangeTo <= _pageCount();
    }

    private bool CanCancel() => IsBusy;

    // The ComboBoxes of the tab are bound by index.
    public int ActionIndex
    {
        get => (int)Action;
        set => Action = (AiAction)value;
    }

    public int ScopeIndex
    {
        get => (int)Scope;
        set => Scope = (AiScope)value;
    }

    public bool IsRangeScope => Scope == AiScope.PageRange;

    partial void OnScopeChanged(AiScope value)
    {
        OnPropertyChanged(nameof(ScopeIndex));
        OnPropertyChanged(nameof(IsRangeScope));
        Cancel();
    }

    partial void OnActionChanged(AiAction value)
    {
        OnPropertyChanged(nameof(ActionIndex));
        Cancel();
    }

    private void Cancel()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The run already ended.
        }
    }

    private IReadOnlyList<int> ResolvePages()
    {
        int count = _pageCount();
        switch (Scope)
        {
            case AiScope.CurrentPage:
                return [Math.Clamp(_currentPage(), 1, Math.Max(1, count))];

            case AiScope.PageRange:
                return Range(RangeFrom, RangeTo);

            default:
                return Range(1, count);
        }

        static int[] Range(int from, int to)
        {
            var pages = new int[Math.Max(0, to - from + 1)];
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i] = from + i;
            }

            return pages;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        var token = cts.Token;

        IsBusy = true;
        OutputText = string.Empty;
        Items.Clear();
        SelectedItem = null;
        LastError = null;

        try
        {
            var pages = ResolvePages();
            if (Action == AiAction.Summarise)
            {
                var text = new StringBuilder();
                await foreach (string fragment in _runner.RunTextAsync(Action, pages, token))
                {
                    text.Append(fragment);
                    OutputText = text.ToString();
                }
            }
            else
            {
                var raw = new StringBuilder();
                bool finished = false;

                // Progress<T> posts its reports, so one can arrive after the run has ended.
                var progress = new Progress<string>(fragment =>
                {
                    if (!finished && !token.IsCancellationRequested)
                    {
                        raw.Append(fragment);
                        OutputText = raw.ToString();
                    }
                });

                var items = await _runner.RunExtractionAsync(Action, pages, progress, token);
                finished = true;
                token.ThrowIfCancellationRequested();

                foreach (var item in items)
                {
                    Items.Add(item);
                }

                OutputText = AiResultParser.ToCsvFile(items);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled by the user, a scope change or the document closing: nothing to show.
        }
        catch (Exception e)
        {
            LastError = e.Message;
        }
        finally
        {
            if (ReferenceEquals(_cts, cts))
            {
                _cts = null;
            }

            cts.Dispose();
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelRun() => Cancel();

    // Named so the generated command is CancelCommand.
    public IRelayCommand CancelCommand => CancelRunCommand;

    /// <summary>
    /// Selects the item at <paramref name="index"/>; used by a click on its box on the page.
    /// </summary>
    [RelayCommand]
    private void SelectItem(int index)
    {
        if (index >= 0 && index < Items.Count)
        {
            SelectedItem = Items[index];
        }
    }

    [RelayCommand]
    private Task Copy() => string.IsNullOrEmpty(OutputText) ? Task.CompletedTask : _copy(OutputText);

    [RelayCommand]
    private Task Save()
    {
        if (string.IsNullOrEmpty(OutputText))
        {
            return Task.CompletedTask;
        }

        bool csv = Items.Count > 0;
        string suffix = Action switch
        {
            AiAction.ExtractTables => "-tables",
            AiAction.ExtractCharts => "-charts",
            _ => "-summary"
        };

        return _save($"{_baseFileName}{suffix}{(csv ? ".csv" : ".md")}", new UTF8Encoding(false).GetBytes(OutputText));
    }

    [RelayCommand]
    private void Clear()
    {
        Cancel();
        Items.Clear();
        SelectedItem = null;
        OutputText = string.Empty;
        LastError = null;
    }

    private void OnServiceStatusChanged(object? sender, EventArgs e)
    {
        if (_context is null)
        {
            ApplyStatus();
        }
        else
        {
            _context.Post(static state => ((AiPanelViewModel)state!).ApplyStatus(), this);
        }
    }

    private void ApplyStatus()
    {
        if (!_disposed)
        {
            Status = _service.Status;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _service.StatusChanged -= OnServiceStatusChanged;
        Cancel();
    }
}
