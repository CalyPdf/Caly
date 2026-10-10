using System.Runtime.Versioning;
using Caly.Core.Services.Interfaces;
using Caly.Printing.Windows;
using UglyToad.PdfPig;

namespace Caly.Printing.Tests;

/// <summary>
/// Prints through the real Windows spooler to the queue the Printing workflow provisions: the
/// "Microsoft Print To PDF" driver on a port that writes each job to a known file. The tests
/// read that file back to see what the driver was asked to print.
/// </summary>
[Collection(PrinterIntegration.Collection)]
[SupportedOSPlatform("windows5.0")]
public class WindowsPrintServiceIntegrationTests
{
    private static (PrinterInfo Printer, string OutputPath) RequirePrinter()
    {
        string name = PrinterIntegration.RequireQueue(OperatingSystem.IsWindows());
        string? output = Environment.GetEnvironmentVariable(PrinterIntegration.OutputVariable);
        Assert.False(string.IsNullOrWhiteSpace(output),
            $"{PrinterIntegration.OutputVariable} must name the file the test queue writes to.");

        return (new PrinterInfo(name, null), output!);
    }

    /// <summary>
    /// Prints every page of <paramref name="document"/> and returns the (width, height) of each
    /// page of the PDF the spooler wrote.
    /// </summary>
    private static async Task<IReadOnlyList<(double Width, double Height)>> PrintAsync(
        PrintTestDocument document, PrintSettings settings, IProgress<int>? progress = null)
    {
        var (printer, outputPath) = RequirePrinter();

        // The queue always writes to the same file: remove the previous test's job first.
        File.Delete(outputPath);

        await new WindowsPrintService().PrintDocumentAsync(
            printer, document, document.AllPages, settings, progress, CancellationToken.None);

        return await PrinterIntegration.WaitForAsync(
            () => Task.FromResult(TryReadPageSizes(outputPath)),
            $"the spooler to write {outputPath}");
    }

    /// <summary>
    /// Returns the page sizes of the PDF at <paramref name="path"/>, or <c>null</c> while the
    /// spooler has not created it or still holds it open.
    /// </summary>
    private static IReadOnlyList<(double Width, double Height)>? TryReadPageSizes(string path)
    {
        try
        {
            // FileShare.None fails while the spooler is still writing, so a half-written PDF is never parsed.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            if (stream.Length == 0)
            {
                return null;
            }

            using var pdf = PdfDocument.Open(stream);
            return pdf.GetPages().Select(p => (p.Width, p.Height)).ToArray();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    [Fact]
    public async Task GetAvailablePrintersAsync_ListsTheTestQueue()
    {
        var (printer, _) = RequirePrinter();

        var printers = await new WindowsPrintService().GetAvailablePrintersAsync(CancellationToken.None);

        Assert.Contains(printers, p => p.Name == printer.Name);
    }

    [Fact]
    public async Task GetPrinterCapabilitiesAsync_ReportsLandscapeAndAppSideNumberUp()
    {
        var (printer, _) = RequirePrinter();

        var capabilities = await new WindowsPrintService().GetPrinterCapabilitiesAsync(printer, CancellationToken.None);

        Assert.True(capabilities.SupportsLandscape);
        Assert.Equal(new[] { 1, 2, 4 }, capabilities.SupportedNumberUp);
    }

    [Fact]
    public async Task PrintDocumentAsync_PortraitSetting_WritesOnePortraitSheetPerPage()
    {
        await using var document = PrintTestDocument.Create("caly-portrait.pdf", true, true);

        var sheets = await PrintAsync(document, new PrintSettings(Orientation: PrintOrientation.Portrait));

        Assert.Equal(2, sheets.Count);
        Assert.All(sheets, s => Assert.True(s.Height > s.Width, $"expected portrait, got {s.Width}x{s.Height}"));
    }

    [Fact]
    public async Task PrintDocumentAsync_LandscapeSetting_WritesLandscapeSheets()
    {
        await using var document = PrintTestDocument.Create("caly-landscape.pdf", true, true);

        var sheets = await PrintAsync(document, new PrintSettings(Orientation: PrintOrientation.Landscape));

        Assert.Equal(2, sheets.Count);
        Assert.All(sheets, s => Assert.True(s.Width > s.Height, $"expected landscape, got {s.Width}x{s.Height}"));
    }

    [Fact]
    public async Task PrintDocumentAsync_AutoOrientation_FollowsEachPage()
    {
        await using var document = PrintTestDocument.Create("caly-auto.pdf", true, false);

        var sheets = await PrintAsync(document, new PrintSettings(Orientation: PrintOrientation.Auto));

        Assert.Equal(2, sheets.Count);
        Assert.True(sheets[0].Height > sheets[0].Width, $"page 1: expected portrait, got {sheets[0].Width}x{sheets[0].Height}");
        Assert.True(sheets[1].Width > sheets[1].Height, $"page 2: expected landscape, got {sheets[1].Width}x{sheets[1].Height}");
    }

    [Fact]
    public async Task PrintDocumentAsync_TwoPagesPerSheet_WritesHalfAsManySheetsRoundedUp()
    {
        await using var document = PrintTestDocument.Create("caly-2up.pdf", true, true, true);

        var sheets = await PrintAsync(document,
            new PrintSettings(Orientation: PrintOrientation.Portrait, PagesPerSheet: 2));

        Assert.Equal(2, sheets.Count);
    }

    [Fact]
    public async Task PrintDocumentAsync_ReportsProgressUpToThePageCount()
    {
        await using var document = PrintTestDocument.Create("caly-progress.pdf", true, true);
        var progress = new RecordingProgress();

        await PrintAsync(document, new PrintSettings(Orientation: PrintOrientation.Portrait), progress);

        Assert.Equal(new[] { 1, 2 }, progress.Values);
    }

    [Fact]
    public async Task PrintDocumentAsync_UnknownPrinter_Throws()
    {
        RequirePrinter();
        await using var document = PrintTestDocument.Create("caly-unknown.pdf", true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new WindowsPrintService().PrintDocumentAsync(
                new PrinterInfo("Caly No Such Printer", null), document, document.AllPages,
                new PrintSettings(), null, CancellationToken.None));
    }
}
