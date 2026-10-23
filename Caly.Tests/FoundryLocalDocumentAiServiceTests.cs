using System.Net;
using System.Text;
using System.Text.Json;
using Caly.Ai.FoundryLocal;
using Caly.Core.Models.Ai;

namespace Caly.Tests;

public class FoundryLocalDocumentAiServiceTests
{
    private sealed class FakeHost : IFoundryModelHost
    {
        public int PrepareCalls;
        public Exception? FailWith;
        public TaskCompletionSource? Gate;
        public List<double> Downloads { get; } = [];

        public async Task<FoundryPreparedModel> PrepareAsync(string modelId, Action<double> downloadProgress,
            Action loading, CancellationToken token)
        {
            Interlocked.Increment(ref PrepareCalls);
            if (Gate is { } gate)
            {
                await gate.Task.WaitAsync(token);
            }

            if (FailWith is { } failure)
            {
                throw failure;
            }

            downloadProgress(50);
            loading();
            return new FoundryPreparedModel(new Uri("http://127.0.0.1:5555/v1/"), "model-variant-id");
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }
        public Uri? RequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/event-stream") };
        }
    }

    private const string Sse =
        ": keepalive\n\n" +
        "data: {\"type\":\"response.created\"}\n\n" +
        "data: {\"type\":\"response.output_text.delta\",\"delta\":\"Hel\"}\n\n" +
        "data: not json\n\n" +
        "data: {\"type\":\"response.output_text.delta\",\"delta\":\"lo\"}\n\n" +
        "data: [DONE]\n\n" +
        "data: {\"type\":\"response.output_text.delta\",\"delta\":\"IGNORED\"}\n\n";

    [Fact]
    public async Task Failing_init_sets_Error_and_retry_succeeds()
    {
        var host = new FakeHost { FailWith = new InvalidOperationException("boom") };
        var service = new FoundryLocalDocumentAiService(host, "qwen", new HttpClient(new StubHandler(Sse)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnsureReadyAsync(CancellationToken.None));

        Assert.Equal(AiModelState.Error, service.Status.State);
        Assert.Contains("boom", service.Status.Message);
        Assert.Contains("foundry", service.Status.Message, StringComparison.OrdinalIgnoreCase);

        host.FailWith = null;
        await service.EnsureReadyAsync(CancellationToken.None);

        Assert.Equal(AiModelState.Ready, service.Status.State);
        Assert.Equal(2, host.PrepareCalls);
    }

    [Fact]
    public async Task Concurrent_EnsureReady_initialises_once()
    {
        var host = new FakeHost { Gate = new TaskCompletionSource() };
        var service = new FoundryLocalDocumentAiService(host, "qwen", new HttpClient(new StubHandler(Sse)));

        var first = service.EnsureReadyAsync(CancellationToken.None);
        var second = service.EnsureReadyAsync(CancellationToken.None);
        host.Gate.SetResult();
        await Task.WhenAll(first, second);
        await service.EnsureReadyAsync(CancellationToken.None);

        Assert.Equal(1, host.PrepareCalls);
    }

    [Fact]
    public async Task Status_changes_raise_event_in_order()
    {
        var host = new FakeHost();
        var service = new FoundryLocalDocumentAiService(host, "qwen", new HttpClient(new StubHandler(Sse)));
        var seen = new List<AiModelStatus>();
        service.StatusChanged += (_, _) => seen.Add(service.Status);

        await service.EnsureReadyAsync(CancellationToken.None);

        Assert.Contains(seen, s => s.State == AiModelState.Downloading && s.Progress == 50);
        Assert.Equal(AiModelState.Loading, seen[^2].State);
        Assert.Equal(AiModelState.Ready, seen[^1].State);
    }

    [Fact]
    public async Task Cancelled_init_goes_back_to_NotReady()
    {
        var host = new FakeHost { Gate = new TaskCompletionSource() };
        var service = new FoundryLocalDocumentAiService(host, "qwen", new HttpClient(new StubHandler(Sse)));
        using var cts = new CancellationTokenSource();

        var task = service.EnsureReadyAsync(cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(AiModelState.NotReady, service.Status.State);
    }

    [Fact]
    public void Request_body_has_text_and_image_parts()
    {
        var request = new AiRequest("Be brief.", "Summarise.",
        [
            new AiPageContent(1, "Hello", [1, 2, 3]),
            new AiPageContent(2, "No image", null)
        ]);

        using var doc = JsonDocument.Parse(FoundryResponsesProtocol.BuildBody("variant-id", request));
        var root = doc.RootElement;

        Assert.Equal("variant-id", root.GetProperty("model").GetString());
        Assert.True(root.GetProperty("stream").GetBoolean());

        var message = root.GetProperty("input")[0];
        Assert.Equal("user", message.GetProperty("role").GetString());
        var parts = message.GetProperty("content").EnumerateArray().ToArray();

        Assert.Equal("input_text", parts[0].GetProperty("type").GetString());
        Assert.StartsWith("Be brief.", parts[0].GetProperty("text").GetString());
        Assert.Contains("Summarise.", parts[0].GetProperty("text").GetString());
        Assert.Equal("Page 1:\nHello", parts[1].GetProperty("text").GetString());
        Assert.Equal("input_image", parts[2].GetProperty("type").GetString());
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), parts[2].GetProperty("image_data").GetString());
        Assert.Equal("image/png", parts[2].GetProperty("media_type").GetString());
        Assert.Equal("Page 2:\nNo image", parts[3].GetProperty("text").GetString());
        Assert.Equal(4, parts.Length);
    }

    [Fact]
    public async Task Deltas_are_read_until_done()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Sse));
        var deltas = new List<string>();

        await foreach (string delta in FoundryResponsesProtocol.ReadDeltasAsync(stream, CancellationToken.None))
        {
            deltas.Add(delta);
        }

        Assert.Equal(["Hel", "lo"], deltas);
    }

    [Fact]
    public async Task CompleteAsync_posts_to_the_prepared_server_and_streams_deltas()
    {
        var handler = new StubHandler(Sse);
        var service = new FoundryLocalDocumentAiService(new FakeHost(), "qwen", new HttpClient(handler));
        await service.EnsureReadyAsync(CancellationToken.None);

        var text = new StringBuilder();
        await foreach (string delta in service.CompleteAsync(new AiRequest("s", "u", []), CancellationToken.None))
        {
            text.Append(delta);
        }

        Assert.Equal("Hello", text.ToString());
        Assert.Equal("http://127.0.0.1:5555/v1/responses", handler.RequestUri!.ToString());
        Assert.Contains("model-variant-id", handler.RequestBody);
    }

    [Fact]
    public async Task Model_run_on_the_real_service()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("CALY_TEST_FOUNDRY") == "1",
            "Set CALY_TEST_FOUNDRY=1 to download and run the real model.");

        await using var service = new FoundryLocalDocumentAiService("qwen3.5-0.8b-generic-gpu:4");
        var text = new StringBuilder();
        await foreach (string delta in service.CompleteAsync(new AiRequest("Reply briefly.", "Say hello.", []), CancellationToken.None))
        {
            text.Append(delta);
        }

        Assert.NotEmpty(text.ToString());
    }
}
