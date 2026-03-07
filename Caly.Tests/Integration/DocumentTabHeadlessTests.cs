using System.Reflection;
using Avalonia.Headless.XUnit;
using Caly.Core.Models;
using Caly.Core.Services;
using Caly.Core.ViewModels;
using Caly.Tests.Integration.Mocks;

namespace Caly.Tests.Integration;

/// <summary>
/// Integration tests for document tab management — open/close tabs and multi-document navigation.
///
/// Architecture note
/// -----------------
/// The real <c>PdfDocumentsManagerService</c> depends on <c>App.Current!.Services</c> (a full DI
/// scope), <c>App.Messenger</c>, and a running Avalonia window to obtain the <c>MainViewModel</c>
/// from the DataContext.  None of those are available in a minimal headless test.
///
/// Instead these tests exercise the same <em>observable state</em> that the service manipulates:
/// <c>MainViewModel.PdfDocuments</c> and <c>MainViewModel.SelectedDocumentIndex</c>.
/// A <c>DocumentViewModel</c> is constructed with mock services, and its <c>WaitOpenAsync</c>
/// property (private set, normally written by <c>OpenDocument()</c>) is seeded via reflection so
/// the <c>MainViewModel</c> Rx subscription accepts the document without crashing.
///
/// Behaviours that require the full service pipeline (mutex-based deduplication, DI scope
/// disposal, etc.) are explicitly called out as out-of-scope.
/// </summary>
public class DocumentTabHeadlessTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static DocumentViewModel CreateDocumentViewModel()
    {
        var pdfService = new MockPdfDocumentService();
        var pageService = new PdfPageService(pdfService);
        var textSearch = new MockTextSearchService();
        var settings = new MockSettingsService();
        var vm = new DocumentViewModel(pdfService, pageService, textSearch, settings);

        // WaitOpenAsync has a private setter; it is normally written by OpenDocument().
        // We seed it here so MainViewModel's Rx subscription does not reject the document.
        typeof(DocumentViewModel)
            .GetProperty(nameof(DocumentViewModel.WaitOpenAsync))!
            .SetValue(vm, Task.FromResult(0));

        return vm;
    }

    // -----------------------------------------------------------------------
    // Opening documents ("open doc adds tab")
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void OpenDocument_AddedToCollection_CountIsOne()
    {
        var mainVm = new MainViewModel();
        var docVm = CreateDocumentViewModel();

        mainVm.PdfDocuments.Add(docVm);

        Assert.Single(mainVm.PdfDocuments);
    }

    [AvaloniaFact]
    public void OpenDocument_AddedToCollection_SameInstanceIsStored()
    {
        var mainVm = new MainViewModel();
        var docVm = CreateDocumentViewModel();

        mainVm.PdfDocuments.Add(docVm);

        Assert.Same(docVm, mainVm.PdfDocuments[0]);
    }

    [AvaloniaFact]
    public void OpenTwoDocuments_CollectionHasTwoTabs()
    {
        var mainVm = new MainViewModel();
        var doc1 = CreateDocumentViewModel();
        var doc2 = CreateDocumentViewModel();

        mainVm.PdfDocuments.Add(doc1);
        mainVm.PdfDocuments.Add(doc2);

        Assert.Equal(2, mainVm.PdfDocuments.Count);
    }

    [AvaloniaFact]
    public void OpenThreeDocuments_CollectionHasThreeTabs()
    {
        var mainVm = new MainViewModel();

        mainVm.PdfDocuments.Add(CreateDocumentViewModel());
        mainVm.PdfDocuments.Add(CreateDocumentViewModel());
        mainVm.PdfDocuments.Add(CreateDocumentViewModel());

        Assert.Equal(3, mainVm.PdfDocuments.Count);
    }

    [AvaloniaFact]
    public void OpenTwoDocuments_BothAccessibleByIndex()
    {
        var mainVm = new MainViewModel();
        var doc1 = CreateDocumentViewModel();
        var doc2 = CreateDocumentViewModel();

        mainVm.PdfDocuments.Add(doc1);
        mainVm.PdfDocuments.Add(doc2);

        Assert.Same(doc1, mainVm.PdfDocuments[0]);
        Assert.Same(doc2, mainVm.PdfDocuments[1]);
    }

    // -----------------------------------------------------------------------
    // Closing documents ("close removes tab")
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void CloseDocument_RemovesFromCollection_CountIsZero()
    {
        var mainVm = new MainViewModel();
        var docVm = CreateDocumentViewModel();
        mainVm.PdfDocuments.Add(docVm);

        mainVm.PdfDocuments.Remove(docVm);

        Assert.Empty(mainVm.PdfDocuments);
    }

    [AvaloniaFact]
    public void CloseFirstOfTwoDocuments_RemainingDocumentIsStillAccessible()
    {
        var mainVm = new MainViewModel();
        var doc1 = CreateDocumentViewModel();
        var doc2 = CreateDocumentViewModel();
        mainVm.PdfDocuments.Add(doc1);
        mainVm.PdfDocuments.Add(doc2);

        mainVm.PdfDocuments.Remove(doc1);

        Assert.Single(mainVm.PdfDocuments);
        Assert.Same(doc2, mainVm.PdfDocuments[0]);
    }

    [AvaloniaFact]
    public void CloseSecondOfTwoDocuments_RemainingDocumentIsStillAccessible()
    {
        var mainVm = new MainViewModel();
        var doc1 = CreateDocumentViewModel();
        var doc2 = CreateDocumentViewModel();
        mainVm.PdfDocuments.Add(doc1);
        mainVm.PdfDocuments.Add(doc2);

        mainVm.PdfDocuments.Remove(doc2);

        Assert.Single(mainVm.PdfDocuments);
        Assert.Same(doc1, mainVm.PdfDocuments[0]);
    }

    [AvaloniaFact]
    public void CloseAllDocuments_CollectionIsEmpty()
    {
        var mainVm = new MainViewModel();
        var doc1 = CreateDocumentViewModel();
        var doc2 = CreateDocumentViewModel();
        mainVm.PdfDocuments.Add(doc1);
        mainVm.PdfDocuments.Add(doc2);

        mainVm.PdfDocuments.Remove(doc1);
        mainVm.PdfDocuments.Remove(doc2);

        Assert.Empty(mainVm.PdfDocuments);
    }

    // -----------------------------------------------------------------------
    // SelectedDocumentIndex tracking
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void SelectedDocumentIndex_CanBeSetToPointAtSecondDocument()
    {
        var mainVm = new MainViewModel();
        mainVm.PdfDocuments.Add(CreateDocumentViewModel());
        mainVm.PdfDocuments.Add(CreateDocumentViewModel());

        mainVm.SelectedDocumentIndex = 1;

        Assert.Equal(1, mainVm.SelectedDocumentIndex);
    }

    // -----------------------------------------------------------------------
    // Multi-document navigation (ActivateNext / ActivatePrevious)
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void ActivateNextDocument_TwoDocuments_AdvancesIndex()
    {
        var mainVm = new MainViewModel();
        mainVm.PdfDocuments.Add(CreateDocumentViewModel());
        mainVm.PdfDocuments.Add(CreateDocumentViewModel());
        mainVm.SelectedDocumentIndex = 0;

        mainVm.ActivateNextDocumentCommand.Execute(null);

        Assert.Equal(1, mainVm.SelectedDocumentIndex);
    }

    [AvaloniaFact]
    public void ActivateNextDocument_AtLastDocument_WrapsToFirst()
    {
        var mainVm = new MainViewModel();
        mainVm.PdfDocuments.Add(CreateDocumentViewModel());
        mainVm.PdfDocuments.Add(CreateDocumentViewModel());
        mainVm.SelectedDocumentIndex = 1; // at last document

        mainVm.ActivateNextDocumentCommand.Execute(null);

        Assert.Equal(0, mainVm.SelectedDocumentIndex);
    }

    [AvaloniaFact]
    public void ActivatePreviousDocument_TwoDocuments_DecreasesIndex()
    {
        var mainVm = new MainViewModel();
        mainVm.PdfDocuments.Add(CreateDocumentViewModel());
        mainVm.PdfDocuments.Add(CreateDocumentViewModel());
        mainVm.SelectedDocumentIndex = 1;

        mainVm.ActivatePreviousDocumentCommand.Execute(null);

        Assert.Equal(0, mainVm.SelectedDocumentIndex);
    }

    [AvaloniaFact]
    public void ActivatePreviousDocument_AtFirstDocument_WrapsToLast()
    {
        var mainVm = new MainViewModel();
        mainVm.PdfDocuments.Add(CreateDocumentViewModel());
        mainVm.PdfDocuments.Add(CreateDocumentViewModel());
        mainVm.SelectedDocumentIndex = 0; // at first document

        mainVm.ActivatePreviousDocumentCommand.Execute(null);

        Assert.Equal(1, mainVm.SelectedDocumentIndex);
    }

    [AvaloniaFact]
    public void ActivateNextDocument_ThreeDocuments_CyclesThroughAll()
    {
        var mainVm = new MainViewModel();
        mainVm.PdfDocuments.Add(CreateDocumentViewModel());
        mainVm.PdfDocuments.Add(CreateDocumentViewModel());
        mainVm.PdfDocuments.Add(CreateDocumentViewModel());
        mainVm.SelectedDocumentIndex = 0;

        mainVm.ActivateNextDocumentCommand.Execute(null); // → 1
        mainVm.ActivateNextDocumentCommand.Execute(null); // → 2
        mainVm.ActivateNextDocumentCommand.Execute(null); // → 0 (wrap)

        Assert.Equal(0, mainVm.SelectedDocumentIndex);
    }
}
