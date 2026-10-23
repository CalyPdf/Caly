using System.Runtime.CompilerServices;
using Caly.Core.Models.Ai;
using Caly.Core.Services.Interfaces;

namespace Caly.Tests.Fakes;

/// <summary>
/// Records the requests it is given and replays scripted replies, each in two fragments.
/// </summary>
internal sealed class FakeDocumentAiService : IDocumentAiService
{
    private readonly Queue<string> _replies = new();

    public List<AiRequest> Requests { get; } = [];

    public int EnsureReadyCalls { get; private set; }

    /// <summary>Order of calls, "ready" or "complete".</summary>
    public List<string> Calls { get; } = [];

    /// <summary>Runs after each reply has been streamed, before the next request.</summary>
    public Action<int>? AfterReply { get; set; }

    /// <summary>When set, <see cref="EnsureReadyAsync"/> throws it.</summary>
    public Exception? ReadyFailure { get; set; }

    /// <summary>When set, a reply waits for it, honouring the token.</summary>
    public TaskCompletionSource? Gate { get; set; }

    public AiModelStatus Status { get; private set; } = new(AiModelState.NotReady);

    public event EventHandler? StatusChanged;

    public void SetStatus(AiModelStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Enqueue(params string[] replies)
    {
        foreach (string reply in replies)
        {
            _replies.Enqueue(reply);
        }
    }

    public Task EnsureReadyAsync(CancellationToken token)
    {
        EnsureReadyCalls++;
        Calls.Add("ready");
        if (ReadyFailure is not null)
        {
            SetStatus(new AiModelStatus(AiModelState.Error, 0, ReadyFailure.Message));
            return Task.FromException(ReadyFailure);
        }

        SetStatus(new AiModelStatus(AiModelState.Ready));
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<string> CompleteAsync(AiRequest request, [EnumeratorCancellation] CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Calls.Add("complete");
        Requests.Add(request);

        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(token);
        }

        string reply = _replies.Count > 0 ? _replies.Dequeue() : "ok";
        int half = reply.Length / 2;
        yield return reply[..half];
        token.ThrowIfCancellationRequested();
        yield return reply[half..];

        AfterReply?.Invoke(Requests.Count);
    }
}
