using System.Text;
using Caly.Core.Models.Ai;
using Caly.Core.ViewModels;
using Caly.Tests.Fakes;

namespace Caly.Tests
{
    public class AiPanelViewModelTests
    {
        private const string TableJson = """{"items":[{"kind":"table","page":2,"box":[0.1,0.1,0.5,0.5],"csv":"a,b\n1,2"}]}""";

        private sealed class Sink
        {
            public List<string> Copied { get; } = [];
            public List<(string Name, byte[] Data)> Saved { get; } = [];
        }

        private static (AiPanelViewModel Vm, FakeDocumentAiService Ai, Sink Sink, Func<int> Page) Create(int pageCount = 5)
        {
            var ai = new FakeDocumentAiService();
            var doc = new AiTestDocumentService();
            for (int p = 1; p <= pageCount; p++)
            {
                doc.SetText(p, "Hello World");
            }

            var sink = new Sink();
            int current = 3;
            var vm = new AiPanelViewModel(ai, doc, () => current, () => pageCount,
                text => { sink.Copied.Add(text); return Task.CompletedTask; },
                (name, data) => { sink.Saved.Add((name, data)); return Task.CompletedTask; });
            return (vm, ai, sink, () => current);
        }

        [Fact]
        public async Task Run_streams_text_into_OutputText()
        {
            var (vm, ai, _, _) = Create();
            ai.Enqueue("the summary");
            vm.Action = AiAction.Summarise;

            await vm.RunCommand.ExecuteAsync(null);

            Assert.Equal("the summary", vm.OutputText);
            Assert.False(vm.IsBusy);
        }

        [Fact]
        public async Task Run_extraction_fills_Items()
        {
            var (vm, ai, _, _) = Create();
            ai.Enqueue(TableJson);
            vm.Action = AiAction.ExtractTables;

            await vm.RunCommand.ExecuteAsync(null);

            var item = Assert.Single(vm.Items);
            Assert.Equal(AiItemKind.Table, item.Kind);
            Assert.Equal("a,b\n1,2", vm.OutputText);
        }

        [Fact]
        public void Run_disabled_for_invalid_range()
        {
            var (vm, _, _, _) = Create(pageCount: 5);
            vm.Scope = AiScope.PageRange;

            vm.RangeFrom = 4;
            vm.RangeTo = 2;
            Assert.False(vm.RunCommand.CanExecute(null));

            vm.RangeFrom = 2;
            vm.RangeTo = 9;
            Assert.False(vm.RunCommand.CanExecute(null));

            vm.RangeTo = 4;
            Assert.True(vm.RunCommand.CanExecute(null));
        }

        [Fact]
        public async Task Changing_scope_cancels_running_generation()
        {
            var (vm, ai, _, _) = Create();
            ai.Gate = new TaskCompletionSource();
            ai.Enqueue(TableJson);
            vm.Action = AiAction.ExtractTables;

            var run = vm.RunCommand.ExecuteAsync(null);
            vm.Scope = AiScope.WholeDocument;
            await run;

            Assert.Empty(vm.Items);
            Assert.False(vm.IsBusy);
        }

        [Fact]
        public async Task Cancel_stops_generation_and_keeps_Run_available()
        {
            var (vm, ai, _, _) = Create();
            ai.Gate = new TaskCompletionSource();

            var run = vm.RunCommand.ExecuteAsync(null);
            vm.CancelCommand.Execute(null);
            await run;

            Assert.False(vm.IsBusy);
            Assert.True(vm.RunCommand.CanExecute(null));
        }

        [Fact]
        public async Task Dispose_cancels()
        {
            var (vm, ai, _, _) = Create();
            ai.Gate = new TaskCompletionSource();
            ai.Enqueue(TableJson);
            vm.Action = AiAction.ExtractTables;

            var run = vm.RunCommand.ExecuteAsync(null);
            vm.Dispose();
            await run;

            Assert.Empty(vm.Items);
        }

        [Fact]
        public async Task Service_error_sets_Error_status_and_keeps_Run_enabled()
        {
            var (vm, ai, _, _) = Create();
            ai.ReadyFailure = new InvalidOperationException("no foundry");

            await vm.RunCommand.ExecuteAsync(null);

            Assert.Equal(AiModelState.Error, vm.Status.State);
            Assert.Contains("no foundry", vm.StatusText);
            Assert.False(vm.IsBusy);
            Assert.True(vm.RunCommand.CanExecute(null));
        }

        [Fact]
        public async Task Copy_sends_output_text_to_clipboard()
        {
            var (vm, ai, sink, _) = Create();
            ai.Enqueue("the summary");
            await vm.RunCommand.ExecuteAsync(null);

            await vm.CopyCommand.ExecuteAsync(null);

            Assert.Equal(["the summary"], sink.Copied);
        }

        [Fact]
        public async Task Save_writes_csv_bytes_for_extraction()
        {
            var (vm, ai, sink, _) = Create();
            ai.Enqueue(TableJson);
            vm.Action = AiAction.ExtractTables;
            await vm.RunCommand.ExecuteAsync(null);

            await vm.SaveCommand.ExecuteAsync(null);

            var saved = Assert.Single(sink.Saved);
            Assert.EndsWith(".csv", saved.Name);
            Assert.Equal("a,b\n1,2", Encoding.UTF8.GetString(saved.Data));
        }

        [Fact]
        public async Task Save_writes_markdown_for_summary()
        {
            var (vm, ai, sink, _) = Create();
            ai.Enqueue("the summary");
            await vm.RunCommand.ExecuteAsync(null);

            await vm.SaveCommand.ExecuteAsync(null);

            Assert.EndsWith(".md", Assert.Single(sink.Saved).Name);
        }

        [Fact]
        public async Task Current_page_scope_uses_selected_page()
        {
            var (vm, ai, _, _) = Create();
            vm.Scope = AiScope.CurrentPage;

            await vm.RunCommand.ExecuteAsync(null);

            Assert.Equal([3], Assert.Single(ai.Requests).Pages.Select(p => p.PageNumber));
        }

        [Fact]
        public async Task Whole_document_scope_uses_every_page()
        {
            var (vm, ai, _, _) = Create(pageCount: 4);
            vm.Scope = AiScope.WholeDocument;

            await vm.RunCommand.ExecuteAsync(null);

            Assert.Equal([1, 2, 3, 4], Assert.Single(ai.Requests).Pages.Select(p => p.PageNumber));
        }

        [Fact]
        public async Task Clear_empties_results()
        {
            var (vm, ai, _, _) = Create();
            ai.Enqueue(TableJson);
            vm.Action = AiAction.ExtractTables;
            await vm.RunCommand.ExecuteAsync(null);

            vm.ClearCommand.Execute(null);

            Assert.Empty(vm.Items);
            Assert.Equal(string.Empty, vm.OutputText);
        }

        [Fact]
        public void Status_follows_service()
        {
            var (vm, ai, _, _) = Create();

            ai.SetStatus(new AiModelStatus(AiModelState.Downloading, 42));

            Assert.Equal(AiModelState.Downloading, vm.Status.State);
            Assert.Contains("42", vm.StatusText);
        }
    }
}
