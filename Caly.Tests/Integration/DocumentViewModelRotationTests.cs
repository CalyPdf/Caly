using Avalonia.Headless.XUnit;
using Caly.Core.Models;
using Caly.Core.Services;
using Caly.Core.ViewModels;
using Caly.Tests.Integration.Mocks;

namespace Caly.Tests.Integration;

/// <summary>
/// Integration tests for DocumentViewModel rotation commands.
/// These commands iterate over Pages and call per-page rotation helpers,
/// so the document must have pages added to the Pages collection.
/// </summary>
public class DocumentViewModelRotationTests
{
    private static (DocumentViewModel vm, List<PageViewModel> pages) CreateDocumentViewModelWithPages(int pageCount = 3)
    {
        var pdfService = new MockPdfDocumentService();
        var pageService = new PdfPageService(pdfService);
        var textSearch = new MockTextSearchService();
        var settings = new MockSettingsService();
        var vm = new DocumentViewModel(pdfService, pageService, textSearch, settings);
        vm.PageCount = pageCount;

        var textSelection = new TextSelection(pageCount);
        var pages = new List<PageViewModel>();
        for (int i = 1; i <= pageCount; i++)
        {
            var page = new PageViewModel(i, textSelection, 1.0);
            pages.Add(page);
            vm.Pages.Add(page);
        }

        return (vm, pages);
    }

    // -----------------------------------------------------------------------
    // Clockwise rotation
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void RotateAllPagesClockwise_AllPagesRotate90()
    {
        var (vm, pages) = CreateDocumentViewModelWithPages(3);

        vm.RotateAllPagesClockwiseCommand.Execute(null);

        Assert.All(pages, p => Assert.Equal(90, p.Rotation));
    }

    [AvaloniaFact]
    public void RotateAllPagesClockwise_Twice_AllPagesAt180()
    {
        var (vm, pages) = CreateDocumentViewModelWithPages(3);

        vm.RotateAllPagesClockwiseCommand.Execute(null);
        vm.RotateAllPagesClockwiseCommand.Execute(null);

        Assert.All(pages, p => Assert.Equal(180, p.Rotation));
    }

    [AvaloniaFact]
    public void RotateAllPagesClockwise_FourTimes_AllPagesBackToZero()
    {
        var (vm, pages) = CreateDocumentViewModelWithPages(3);

        vm.RotateAllPagesClockwiseCommand.Execute(null);
        vm.RotateAllPagesClockwiseCommand.Execute(null);
        vm.RotateAllPagesClockwiseCommand.Execute(null);
        vm.RotateAllPagesClockwiseCommand.Execute(null);

        Assert.All(pages, p => Assert.Equal(0, p.Rotation));
    }

    // -----------------------------------------------------------------------
    // Counter-clockwise rotation
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void RotateAllPagesCounterclockwise_AllPagesRotate270()
    {
        var (vm, pages) = CreateDocumentViewModelWithPages(3);

        vm.RotateAllPagesCounterclockwiseCommand.Execute(null);

        Assert.All(pages, p => Assert.Equal(270, p.Rotation));
    }

    [AvaloniaFact]
    public void RotateAllPagesCounterclockwise_Twice_AllPagesAt180()
    {
        var (vm, pages) = CreateDocumentViewModelWithPages(3);

        vm.RotateAllPagesCounterclockwiseCommand.Execute(null);
        vm.RotateAllPagesCounterclockwiseCommand.Execute(null);

        Assert.All(pages, p => Assert.Equal(180, p.Rotation));
    }

    [AvaloniaFact]
    public void RotateAllPagesCounterclockwise_FourTimes_AllPagesBackToZero()
    {
        var (vm, pages) = CreateDocumentViewModelWithPages(3);

        vm.RotateAllPagesCounterclockwiseCommand.Execute(null);
        vm.RotateAllPagesCounterclockwiseCommand.Execute(null);
        vm.RotateAllPagesCounterclockwiseCommand.Execute(null);
        vm.RotateAllPagesCounterclockwiseCommand.Execute(null);

        Assert.All(pages, p => Assert.Equal(0, p.Rotation));
    }

    // -----------------------------------------------------------------------
    // Clockwise + counter-clockwise symmetry
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void RotateClockwiseThenCounterclockwise_ReturnsToOriginal()
    {
        var (vm, pages) = CreateDocumentViewModelWithPages(3);

        vm.RotateAllPagesClockwiseCommand.Execute(null);
        vm.RotateAllPagesCounterclockwiseCommand.Execute(null);

        Assert.All(pages, p => Assert.Equal(0, p.Rotation));
    }

    [AvaloniaFact]
    public void RotateCounterclockwiseThenClockwise_ReturnsToOriginal()
    {
        var (vm, pages) = CreateDocumentViewModelWithPages(3);

        vm.RotateAllPagesCounterclockwiseCommand.Execute(null);
        vm.RotateAllPagesClockwiseCommand.Execute(null);

        Assert.All(pages, p => Assert.Equal(0, p.Rotation));
    }

    // -----------------------------------------------------------------------
    // IsPortrait changes with rotation
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void RotateAllPagesClockwise_AllPagesBecomeLandscape()
    {
        var (vm, pages) = CreateDocumentViewModelWithPages(2);

        // Initial rotation 0 → portrait
        Assert.All(pages, p => Assert.True(p.IsPortrait));

        vm.RotateAllPagesClockwiseCommand.Execute(null); // → 90

        Assert.All(pages, p => Assert.False(p.IsPortrait));
    }

    [AvaloniaFact]
    public void RotateAllPagesTwice_AllPagesRemainPortrait()
    {
        var (vm, pages) = CreateDocumentViewModelWithPages(2);

        vm.RotateAllPagesClockwiseCommand.Execute(null);  // → 90, landscape
        vm.RotateAllPagesClockwiseCommand.Execute(null);  // → 180, portrait

        Assert.All(pages, p => Assert.True(p.IsPortrait));
    }
}
