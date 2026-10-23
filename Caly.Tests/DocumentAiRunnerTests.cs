using Caly.Core.Models.Ai;
using Caly.Core.Services.Ai;
using Caly.Tests.Fakes;

namespace Caly.Tests
{
    public class DocumentAiRunnerTests
    {
        private const string TableJson = """{"items":[{"kind":"table","page":1,"box":[0.1,0.1,0.5,0.5],"csv":"a,b"}]}""";

        private static (DocumentAiRunner Runner, FakeDocumentAiService Ai, AiTestDocumentService Doc) Create(int? budget = null)
        {
            var ai = new FakeDocumentAiService();
            var doc = new AiTestDocumentService();
            var runner = new DocumentAiRunner(ai, new AiPageContentProvider(doc), budget ?? AiTokenEstimator.ContextTokens * 3 / 4);
            return (runner, ai, doc);
        }

        private static async Task<string> Collect(IAsyncEnumerable<string> stream)
        {
            var sb = new System.Text.StringBuilder();
            await foreach (string s in stream)
            {
                sb.Append(s);
            }

            return sb.ToString();
        }

        [Fact]
        public async Task Summarise_single_chunk_makes_one_request()
        {
            var (runner, ai, doc) = Create();
            doc.SetText(1, "Hello World");
            doc.SetText(2, "Hello World");
            ai.Enqueue("the summary");

            string text = await Collect(runner.RunTextAsync(AiAction.Summarise, [1, 2], CancellationToken.None));

            Assert.Equal("the summary", text);
            var request = Assert.Single(ai.Requests);
            Assert.Equal([1, 2], request.Pages.Select(p => p.PageNumber));
        }

        [Fact]
        public async Task Summarise_scanned_page_includes_image()
        {
            var (runner, ai, doc) = Create();
            doc.SetNoTextLayer(1);

            await Collect(runner.RunTextAsync(AiAction.Summarise, [1], CancellationToken.None));

            Assert.NotNull(Assert.Single(ai.Requests).Pages[0].Png);
        }

        [Fact]
        public async Task Summarise_text_page_has_no_image()
        {
            var (runner, ai, doc) = Create();
            doc.SetText(1, "Hello World");

            await Collect(runner.RunTextAsync(AiAction.Summarise, [1], CancellationToken.None));

            Assert.Null(Assert.Single(ai.Requests).Pages[0].Png);
        }

        [Fact]
        public async Task Extraction_always_includes_images()
        {
            var (runner, ai, doc) = Create();
            doc.SetText(1, "Hello World");
            ai.Enqueue(TableJson);

            var items = await runner.RunExtractionAsync(AiAction.ExtractTables, [1], null, CancellationToken.None);

            Assert.NotNull(Assert.Single(ai.Requests).Pages[0].Png);
            Assert.Equal(AiItemKind.Table, Assert.Single(items).Kind);
        }

        [Fact]
        public async Task Summarise_multi_chunk_makes_reduce_request()
        {
            var (runner, ai, doc) = Create(budget: 4);
            doc.SetText(1, "Hello World");
            doc.SetText(2, "Hello World");
            ai.Enqueue("first part", "second part", "overall");

            string text = await Collect(runner.RunTextAsync(AiAction.Summarise, [1, 2], CancellationToken.None));

            Assert.Equal(3, ai.Requests.Count);
            Assert.Empty(ai.Requests[2].Pages);
            Assert.Contains("first part", ai.Requests[2].UserPrompt);
            Assert.Contains("second part", ai.Requests[2].UserPrompt);
            Assert.EndsWith("overall", text);
        }

        [Fact]
        public async Task Extraction_merges_items_from_all_chunks()
        {
            var (runner, ai, doc) = Create(budget: 1500);
            doc.SetText(1, "Hello World");
            doc.SetText(2, "Hello World");
            ai.Enqueue(TableJson, TableJson.Replace("\"page\":1", "\"page\":2"));

            var items = await runner.RunExtractionAsync(AiAction.ExtractTables, [1, 2], null, CancellationToken.None);

            Assert.Equal(2, ai.Requests.Count);
            Assert.Equal([1, 2], items.Select(i => i.PageNumber));
        }

        [Fact]
        public async Task Extraction_reports_streamed_text()
        {
            var (runner, ai, doc) = Create();
            doc.SetText(1, "Hello World");
            ai.Enqueue(TableJson);
            var progress = new List<string>();

            await runner.RunExtractionAsync(AiAction.ExtractTables, [1], new SyncProgress(progress.Add), CancellationToken.None);

            Assert.Equal(TableJson, string.Concat(progress));
        }

        [Fact]
        public async Task Cancellation_stops_before_next_chunk()
        {
            var (runner, ai, doc) = Create(budget: 4);
            doc.SetText(1, "Hello World");
            doc.SetText(2, "Hello World");
            using var cts = new CancellationTokenSource();
            ai.AfterReply = _ => cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => Collect(runner.RunTextAsync(AiAction.Summarise, [1, 2], cts.Token)));

            Assert.Single(ai.Requests);
        }

        [Fact]
        public async Task EnsureReady_called_once_first()
        {
            var (runner, ai, doc) = Create(budget: 4);
            doc.SetText(1, "Hello World");
            doc.SetText(2, "Hello World");

            await Collect(runner.RunTextAsync(AiAction.Summarise, [1, 2], CancellationToken.None));

            Assert.Equal(1, ai.EnsureReadyCalls);
            Assert.Equal("ready", ai.Calls[0]);
        }

        [Fact]
        public async Task Failing_EnsureReady_surfaces_and_makes_no_request()
        {
            var (runner, ai, doc) = Create();
            doc.SetText(1, "Hello World");
            ai.ReadyFailure = new InvalidOperationException("no foundry");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => runner.RunExtractionAsync(AiAction.ExtractTables, [1], null, CancellationToken.None));

            Assert.Empty(ai.Requests);
        }

        private sealed class SyncProgress(Action<string> handler) : IProgress<string>
        {
            public void Report(string value) => handler(value);
        }
    }
}
