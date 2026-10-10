using Caly.Core.Services.Interfaces;
using Caly.Printing.Unix;
using SharpIpp.Exceptions;
using SharpIpp.Protocol.Models;
using CalyPrintColorMode = Caly.Core.Services.Interfaces.PrintColorMode;

namespace Caly.Printing.Tests;

/// <summary>
/// Prints through a real cupsd to the queue the Printing workflow provisions: a Generic
/// PostScript queue whose output goes to /dev/null. CUPS filters every document Caly sends, so
/// a job only completes if CUPS could process it; the tests assert on CUPS' record of the job.
/// </summary>
[Collection(PrinterIntegration.Collection)]
public class UnixPrintServiceIntegrationTests
{
    private static CupsQueue RequireQueue() => new(PrinterIntegration.RequireQueue(!OperatingSystem.IsWindows()));

    /// <summary>
    /// Prints every page of <paramref name="document"/> and returns the attributes of the job
    /// CUPS created for it, once that job has finished.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, object>> PrintAsync(
        CupsQueue queue, PrintTestDocument document, PrintSettings settings, IProgress<int>? progress = null)
    {
        int previousJobId = await queue.LastJobIdAsync();

        using var service = new UnixPrintService();
        await service.PrintDocumentAsync(
            new PrinterInfo(queue.Name, queue.PrinterUri), document, document.AllPages, settings, progress,
            CancellationToken.None);

        return await queue.WaitForFinishedJobAfterAsync(previousJobId);
    }

    [Fact]
    public async Task GetAvailablePrintersAsync_ListsTheTestQueueWithAnIppUri()
    {
        using var queue = RequireQueue();
        using var service = new UnixPrintService();

        var printers = await service.GetAvailablePrintersAsync(CancellationToken.None);

        var printer = Assert.Single(printers, p => p.Name == queue.Name);
        Assert.NotNull(printer.IppUri);
    }

    [Fact]
    public async Task PrintDocumentAsync_TwoPages_CompletesAJobWithOneDocumentPerPage()
    {
        using var queue = RequireQueue();
        await using var document = PrintTestDocument.Create("caly-two-pages.pdf", true, true);

        var job = await PrintAsync(queue, document, new PrintSettings());

        Assert.Equal((int)JobState.Completed, CupsQueue.Int(job, "job-state"));
        Assert.Equal(2, CupsQueue.Int(job, "number-of-documents"));
        Assert.Equal("caly-two-pages.pdf", CupsQueue.Text(job, "job-name"));
        Assert.Equal(Environment.UserName, CupsQueue.Text(job, "job-originating-user-name"));
    }

    [Fact]
    public async Task PrintDocumentAsync_Settings_ReachTheJobAsIppAttributes()
    {
        using var queue = RequireQueue();
        await using var document = PrintTestDocument.Create("caly-settings.pdf", true, true);
        var settings = new PrintSettings(
            Orientation: PrintOrientation.Landscape,
            FitMode: PrintFitMode.ActualSize,
            PagesPerSheet: 2,
            ColorMode: CalyPrintColorMode.Monochrome);

        var job = await PrintAsync(queue, document, settings);

        Assert.Equal((int)JobState.Completed, CupsQueue.Int(job, "job-state"));
        Assert.Equal((int)Orientation.Landscape, CupsQueue.Int(job, "orientation-requested"));
        Assert.Equal(2, CupsQueue.Int(job, "number-up"));
        Assert.Equal("none", CupsQueue.Text(job, "print-scaling"));
        Assert.Equal("monochrome", CupsQueue.Text(job, "print-color-mode"));
    }

    [Fact]
    public async Task PrintDocumentAsync_ReportsProgressUpToThePageCount()
    {
        using var queue = RequireQueue();
        await using var document = PrintTestDocument.Create("caly-progress.pdf", true, true);
        var progress = new RecordingProgress();

        await PrintAsync(queue, document, new PrintSettings(), progress);

        Assert.Equal(new[] { 1, 2 }, progress.Values);
    }

    [Fact]
    public async Task PrintDocumentAsync_UnknownQueue_Throws()
    {
        using var queue = RequireQueue();
        await using var document = PrintTestDocument.Create("caly-unknown.pdf", true);
        using var service = new UnixPrintService();
        var missing = new PrinterInfo("CalyNoSuchQueue", new Uri("ipp://localhost:631/printers/CalyNoSuchQueue"));

        var exception = await Record.ExceptionAsync(() =>
            service.PrintDocumentAsync(missing, document, document.AllPages, new PrintSettings(), null,
                CancellationToken.None));

        // CUPS answers either with an IPP error status or with a plain HTTP error for the unknown path.
        Assert.True(exception is IppResponseException or HttpRequestException,
            $"expected an IPP or HTTP error, got {exception?.GetType().Name ?? "no exception"}: {exception?.Message}");
    }

    [Fact]
    public async Task PrintDocumentAsync_CanceledAfterTheFirstPage_DoesNotLeaveTheJobOpen()
    {
        using var queue = RequireQueue();
        await using var document = PrintTestDocument.Create("caly-canceled.pdf", true, true, true);
        using var cts = new CancellationTokenSource();
        var progress = new RecordingProgress(_ => cts.Cancel());
        int previousJobId = await queue.LastJobIdAsync();

        using var service = new UnixPrintService();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.PrintDocumentAsync(
                new PrinterInfo(queue.Name, queue.PrinterUri), document, document.AllPages, new PrintSettings(),
                progress, cts.Token));

        // A job still waiting for documents never reaches a finished state, and this would time out.
        var job = await queue.WaitForFinishedJobAfterAsync(previousJobId);

        Assert.Equal(new[] { 1 }, progress.Values);
        Assert.Equal("caly-canceled.pdf", CupsQueue.Text(job, "job-name"));
    }
}
