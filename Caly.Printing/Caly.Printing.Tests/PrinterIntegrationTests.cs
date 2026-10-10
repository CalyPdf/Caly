namespace Caly.Printing.Tests;

public class PrinterIntegrationTests
{
    [Theory]
    // A queue was provisioned for this OS: run.
    [InlineData(true, "CalyTestPrinter", true, PrinterIntegration.Availability.Run)]
    [InlineData(true, "CalyTestPrinter", false, PrinterIntegration.Availability.Run)]
    // No queue on a developer machine: skip, never pick a printer.
    [InlineData(true, null, false, PrinterIntegration.Availability.Skip)]
    [InlineData(true, " ", false, PrinterIntegration.Availability.Skip)]
    // No queue on CI means provisioning broke: fail, never skip.
    [InlineData(true, null, true, PrinterIntegration.Availability.Fail)]
    [InlineData(true, "", true, PrinterIntegration.Availability.Fail)]
    // The other platform's print service: skip, even on CI with a queue.
    [InlineData(false, "CalyTestPrinter", true, PrinterIntegration.Availability.Skip)]
    [InlineData(false, null, true, PrinterIntegration.Availability.Skip)]
    public void Resolve_DecidesFromOsQueueAndCi(bool osMatches, string? printer, bool isCi,
        PrinterIntegration.Availability expected)
    {
        Assert.Equal(expected, PrinterIntegration.Resolve(osMatches, printer, isCi));
    }

    [Fact]
    public async Task WaitForAsync_ReturnsOnceTheProbeYieldsAValue()
    {
        int calls = 0;

        string result = await PrinterIntegration.WaitForAsync(
            () => Task.FromResult(++calls < 3 ? null : "ready"),
            "the probe",
            TimeSpan.FromSeconds(10));

        Assert.Equal("ready", result);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task WaitForAsync_NeverReady_ThrowsNamingWhatWasAwaited()
    {
        var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
            PrinterIntegration.WaitForAsync(
                () => Task.FromResult<string?>(null),
                "the spooler to write out.pdf",
                TimeSpan.FromMilliseconds(300)));

        Assert.Contains("the spooler to write out.pdf", ex.Message);
    }
}
