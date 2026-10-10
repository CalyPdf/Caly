using System.Diagnostics;

namespace Caly.Printing.Tests;

/// <summary>
/// Shared plumbing for the tests that print to a real spooler.
/// <para>
/// Those tests print only to the queue named by <see cref="PrinterVariable"/>, which the
/// Printing workflow provisions. They never pick a printer themselves, so running the test
/// project on a developer machine cannot send a job to a real printer.
/// </para>
/// </summary>
public static class PrinterIntegration
{
    /// <summary>xunit collection shared by all spooler tests: one queue, one job at a time.</summary>
    public const string Collection = "Printer";

    public const string PrinterVariable = "CALY_TEST_PRINTER";

    /// <summary>Windows only: the file the test queue's port writes each job to.</summary>
    public const string OutputVariable = "CALY_TEST_PRINTER_OUTPUT";

    public enum Availability
    {
        Run,
        Skip,
        Fail
    }

    /// <summary>
    /// Decides what a spooler test does. A missing queue is a skip on a developer machine but a
    /// failure on CI, where it means the provisioning step did not run.
    /// </summary>
    public static Availability Resolve(bool osMatches, string? printer, bool isCi)
    {
        if (!osMatches)
        {
            return Availability.Skip;
        }

        if (!string.IsNullOrWhiteSpace(printer))
        {
            return Availability.Run;
        }

        return isCi ? Availability.Fail : Availability.Skip;
    }

    /// <summary>
    /// Returns the test queue's name, or skips / fails the calling test per <see cref="Resolve"/>.
    /// </summary>
    /// <param name="osMatches">Whether the print service under test runs on this operating system.</param>
    public static string RequireQueue(bool osMatches)
    {
        string? printer = Environment.GetEnvironmentVariable(PrinterVariable);
        bool isCi = string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true",
            StringComparison.OrdinalIgnoreCase);

        switch (Resolve(osMatches, printer, isCi))
        {
            case Availability.Skip:
                Assert.Skip(osMatches
                    ? $"{PrinterVariable} is not set; spooler tests only run in the Printing workflow."
                    : "This print service does not run on this operating system.");
                break;

            case Availability.Fail:
                Assert.Fail($"{PrinterVariable} is not set on CI: the printer provisioning step did not run.");
                break;
        }

        return printer!;
    }

    /// <summary>
    /// Polls <paramref name="probe"/> until it returns a value. Spooling continues after the print
    /// call returns, so tests wait on the outcome instead of sleeping for a fixed time.
    /// </summary>
    public static async Task<T> WaitForAsync<T>(Func<Task<T?>> probe, string description, TimeSpan? timeout = null)
        where T : class
    {
        TimeSpan limit = timeout ?? TimeSpan.FromSeconds(90);
        var elapsed = Stopwatch.StartNew();

        while (true)
        {
            T? result = await probe();
            if (result is not null)
            {
                return result;
            }

            if (elapsed.Elapsed > limit)
            {
                throw new TimeoutException($"Timed out after {limit.TotalSeconds:0.#}s waiting for {description}.");
            }

            await Task.Delay(100);
        }
    }
}
