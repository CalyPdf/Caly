using Avalonia.Headless.XUnit;
using Caly.Core.Models;
using Caly.Core.Services;
using Caly.Core.ViewModels;
using Caly.Tests.Fakes;

namespace Caly.Tests;

public class DocumentViewModelAiTests
{
    private static DocumentViewModel NewDocument(RenderingPdfDocumentService pdfService, PdfPageService pageService,
        FakeDocumentAiService? ai, int pageCount = 3)
    {
        var document = new DocumentViewModel(pdfService, pageService, new NoopTextSearchService(), ai);
        pdfService.Publish(pageCount);
        document.PageCount = pageCount;
        document.SelectedPageNumber = 2;
        return document;
    }

    [AvaloniaFact]
    public async Task Document_without_ai_service_has_no_panel()
    {
        var pdfService = new RenderingPdfDocumentService();
        await using var pageService = new PdfPageService(pdfService);

        var document = NewDocument(pdfService, pageService, ai: null);

        Assert.Null(document.Ai);
    }

    [AvaloniaFact]
    public async Task Document_with_ai_service_has_panel_on_the_selected_page()
    {
        var pdfService = new RenderingPdfDocumentService();
        await using var pageService = new PdfPageService(pdfService);
        var ai = new FakeDocumentAiService();
        var document = NewDocument(pdfService, pageService, ai);

        Assert.NotNull(document.Ai);
        document.Ai.Scope = Caly.Core.Models.Ai.AiScope.CurrentPage;
        await document.Ai.RunCommand.ExecuteAsync(null);

        Assert.Equal([2], Assert.Single(ai.Requests).Pages.Select(p => p.PageNumber));
    }

    [AvaloniaFact]
    public async Task Disposing_the_document_cancels_a_running_generation()
    {
        var pdfService = new RenderingPdfDocumentService();
        await using var pageService = new PdfPageService(pdfService);
        var ai = new FakeDocumentAiService { Gate = new TaskCompletionSource() };
        var document = NewDocument(pdfService, pageService, ai);

        var run = document.Ai!.RunCommand.ExecuteAsync(null);
        document.Ai.Dispose();
        await run;

        Assert.False(document.Ai.IsBusy);
    }
}
