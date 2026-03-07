using Avalonia.Headless.XUnit;
using Caly.Core.Models;
using Caly.Core.Services;
using Caly.Core.ViewModels;
using Caly.Pdf.Models;
using Caly.Tests.Integration.Mocks;
using UglyToad.PdfPig.Core;

namespace Caly.Tests.Integration;

/// <summary>
/// Integration tests for DocumentViewModel selection-related behaviour.
/// Corresponds to the "TextSelectionHeadlessTests" category from the original plan:
/// select state after Start/Extend, and ClearSelectionCommand.
/// </summary>
public class DocumentViewModelSelectionTests
{
    private static (DocumentViewModel vm, TextSelection sel) CreateDocumentViewModelWithSelection(int pageCount = 3)
    {
        var pdfService = new MockPdfDocumentService();
        var pageService = new PdfPageService(pdfService);
        var textSearch = new MockTextSearchService();
        var settings = new MockSettingsService();
        var vm = new DocumentViewModel(pdfService, pageService, textSearch, settings);
        vm.PageCount = pageCount;

        // Give the VM a real TextSelection (normally set by OpenDocumentCore)
        var sel = new TextSelection(pageCount);
        vm.TextSelection = sel;

        return (vm, sel);
    }

    private static PdfWord MakeWord(string value = "A")
    {
        var letters = new List<PdfLetter>
        {
            new PdfLetter(value, new PdfRectangle(0, 0, 10, 12), 12f, 0)
        };
        return new PdfWord(letters);
    }

    // -----------------------------------------------------------------------
    // TextSelection initial state inside DocumentViewModel
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void TextSelection_InitiallyNull_BeforeOpenDocument()
    {
        var pdfService = new MockPdfDocumentService();
        var pageService = new PdfPageService(pdfService);
        var textSearch = new MockTextSearchService();
        var settings = new MockSettingsService();
        var vm = new DocumentViewModel(pdfService, pageService, textSearch, settings);

        // TextSelection is null until OpenDocument is called
        Assert.Null(vm.TextSelection);
    }

    [AvaloniaFact]
    public void TextSelection_AfterAssignment_HasExpectedPageCount()
    {
        var (_, sel) = CreateDocumentViewModelWithSelection(pageCount: 5);
        Assert.Equal(5, sel.NumberOfPages);
    }

    // -----------------------------------------------------------------------
    // ClearSelectionCommand ("clear" from TextSelectionHeadlessTests plan)
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void ClearSelectionCommand_WithNullSelection_DoesNotThrow()
    {
        var pdfService = new MockPdfDocumentService();
        var pageService = new PdfPageService(pdfService);
        var textSearch = new MockTextSearchService();
        var settings = new MockSettingsService();
        var vm = new DocumentViewModel(pdfService, pageService, textSearch, settings);
        // TextSelection is null — command should be a no-op, not throw
        vm.ClearSelectionCommand.Execute(null);
    }

    [AvaloniaFact]
    public void ClearSelectionCommand_AfterExtend_ResetsSelection()
    {
        var (vm, sel) = CreateDocumentViewModelWithSelection(pageCount: 3);

        var wordA = MakeWord("A");
        var wordB = MakeWord("B");
        sel.Start(1, wordA);
        sel.Extend(1, wordB);

        Assert.True(sel.HasStarted);

        vm.ClearSelectionCommand.Execute(null);

        Assert.False(sel.HasStarted);
        Assert.False(sel.IsValid);
        Assert.Null(sel.AnchorWord);
        Assert.Null(sel.FocusWord);
    }

    [AvaloniaFact]
    public void ClearSelectionCommand_OnAlreadyResetSelection_IsIdempotent()
    {
        var (vm, sel) = CreateDocumentViewModelWithSelection(pageCount: 3);

        // Already empty — calling clear twice should be safe
        vm.ClearSelectionCommand.Execute(null);
        vm.ClearSelectionCommand.Execute(null);

        Assert.False(sel.HasStarted);
    }
}
