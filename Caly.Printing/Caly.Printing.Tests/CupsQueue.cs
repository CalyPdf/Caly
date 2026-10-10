using SharpIpp;
using SharpIpp.Models.Requests;
using SharpIpp.Protocol.Models;

namespace Caly.Printing.Tests;

/// <summary>
/// Reads the job record CUPS keeps for the test queue, over IPP. The queue discards its output,
/// so the job record is what the Unix tests assert on.
/// </summary>
internal sealed class CupsQueue(string name) : IDisposable
{
    private static readonly string[] s_jobAttributes =
    [
        "job-id", "job-state", "job-state-reasons", "job-name", "job-originating-user-name",
        "number-of-documents", "orientation-requested", "print-color-mode", "number-up", "print-scaling"
    ];

    private readonly SharpIppClient _client = new();

    public string Name { get; } = name;

    public Uri PrinterUri { get; } = new($"ipp://localhost:631/printers/{Uri.EscapeDataString(name)}");

    /// <summary>The highest job id on the queue, whatever its state; 0 when it has no jobs.</summary>
    public async Task<int> LastJobIdAsync()
    {
        var response = await _client.GetJobsAsync(new GetJobsRequest
        {
            OperationAttributes = new GetJobsOperationAttributes
            {
                PrinterUri = PrinterUri,
                RequestingUserName = Environment.UserName,
                WhichJobs = WhichJobs.All,
                RequestedAttributes = new[] { "job-id" }
            }
        });

        int last = 0;
        foreach (var job in response.JobsAttributes ?? [])
        {
            last = Math.Max(last, job.JobId?.GetValueOrDefault() ?? 0);
        }

        return last;
    }

    /// <summary>
    /// Waits for a job newer than <paramref name="previousJobId"/> to be canceled, aborted or
    /// completed, and returns its attributes by IPP name.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object>> WaitForFinishedJobAfterAsync(int previousJobId)
    {
        return PrinterIntegration.WaitForAsync<IReadOnlyDictionary<string, object>>(async () =>
        {
            int jobId = await LastJobIdAsync();
            if (jobId <= previousJobId)
            {
                return null;
            }

            var job = await GetJobAttributesAsync(jobId);
            return Int(job, "job-state") >= (int)JobState.Canceled ? job : null;
        }, $"a job newer than {previousJobId} on '{Name}' to finish");
    }

    private async Task<IReadOnlyDictionary<string, object>> GetJobAttributesAsync(int jobId)
    {
        var request = _client.CreateRawRequest(new GetJobAttributesRequest
        {
            OperationAttributes = new GetJobAttributesOperationAttributes
            {
                PrinterUri = PrinterUri,
                JobId = jobId,
                RequestingUserName = Environment.UserName,
                RequestedAttributes = s_jobAttributes
            }
        });

        var response = await _client.SendAsync(PrinterUri, request);

        var job = new Dictionary<string, object>();
        foreach (var attribute in response.JobAttributes.SelectMany(group => group))
        {
            // First value wins: every attribute read here is single-valued.
            job.TryAdd(attribute.Name, attribute.Value);
        }

        return job;
    }

    public static int Int(IReadOnlyDictionary<string, object> job, string name)
    {
        Assert.True(job.ContainsKey(name), $"CUPS did not return '{name}'. Returned: {string.Join(", ", job.Keys)}");
        return Convert.ToInt32(job[name]);
    }

    public static string Text(IReadOnlyDictionary<string, object> job, string name)
    {
        Assert.True(job.ContainsKey(name), $"CUPS did not return '{name}'. Returned: {string.Join(", ", job.Keys)}");
        return job[name].ToString()!;
    }

    public void Dispose() => _client.Dispose();
}
