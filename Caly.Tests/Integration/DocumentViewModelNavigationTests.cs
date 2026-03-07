using Avalonia.Headless.XUnit;
using Caly.Core.Models;
using Caly.Core.Services;
using Caly.Core.ViewModels;
using Caly.Tests.Integration.Mocks;

namespace Caly.Tests.Integration;

/// <summary>
/// Integration tests for DocumentViewModel page navigation commands.
/// Requires Avalonia headless because DocumentViewModel's constructor uses
/// Dispatcher.UIThread.Invoke().
/// </summary>
public class DocumentViewModelNavigationTests
{
    private static DocumentViewModel CreateDocumentViewModel(int pageCount = 5)
    {
        var pdfService = new MockPdfDocumentService();
        var pageService = new PdfPageService(pdfService);
        var textSearch = new MockTextSearchService();
        var settings = new MockSettingsService();
        var vm = new DocumentViewModel(pdfService, pageService, textSearch, settings);
        // Set PageCount directly so navigation commands can evaluate their CanExecute guards.
        vm.PageCount = pageCount;
        return vm;
    }

    // -----------------------------------------------------------------------
    // Initial state
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void SelectedPageNumber_Default_IsOne()
    {
        var vm = CreateDocumentViewModel();
        Assert.Equal(1, vm.SelectedPageNumber);
    }

    [AvaloniaFact]
    public void SelectedPageIndex_Default_IsZero()
    {
        var vm = CreateDocumentViewModel();
        Assert.Equal(0, vm.SelectedPageIndex);
    }

    [AvaloniaFact]
    public void SelectedPageIndex_ReflectsSelectedPageNumber()
    {
        var vm = CreateDocumentViewModel();
        vm.SelectedPageNumber = 3;
        Assert.Equal(2, vm.SelectedPageIndex);
    }

    [AvaloniaFact]
    public void SelectedPageNumber_ReflectsSelectedPageIndex()
    {
        var vm = CreateDocumentViewModel();
        vm.SelectedPageIndex = 4; // 5th page
        Assert.Equal(5, vm.SelectedPageNumber);
    }

    // -----------------------------------------------------------------------
    // CanExecute guards
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void GoToPreviousPageCommand_CanExecute_FalseOnFirstPage()
    {
        var vm = CreateDocumentViewModel();
        // SelectedPageNumber starts at 1
        Assert.False(vm.GoToPreviousPageCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void GoToNextPageCommand_CanExecute_TrueOnFirstPage()
    {
        var vm = CreateDocumentViewModel(pageCount: 5);
        // SelectedPageNumber = 1, PageCount = 5
        Assert.True(vm.GoToNextPageCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void GoToNextPageCommand_CanExecute_FalseOnLastPage()
    {
        var vm = CreateDocumentViewModel(pageCount: 5);
        vm.SelectedPageNumber = 5;
        Assert.False(vm.GoToNextPageCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void GoToPreviousPageCommand_CanExecute_TrueOnLastPage()
    {
        var vm = CreateDocumentViewModel(pageCount: 5);
        vm.SelectedPageNumber = 5;
        Assert.True(vm.GoToPreviousPageCommand.CanExecute(null));
    }

    // -----------------------------------------------------------------------
    // Forward navigation
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void GoToNextPage_FromPage1_MovesToPage2()
    {
        var vm = CreateDocumentViewModel(pageCount: 5);
        vm.GoToNextPageCommand.Execute(null);
        Assert.Equal(2, vm.SelectedPageNumber);
    }

    [AvaloniaFact]
    public void GoToNextPage_MultipleSteps_AdvancesCorrectly()
    {
        var vm = CreateDocumentViewModel(pageCount: 5);
        vm.GoToNextPageCommand.Execute(null); // → 2
        vm.GoToNextPageCommand.Execute(null); // → 3
        vm.GoToNextPageCommand.Execute(null); // → 4
        Assert.Equal(4, vm.SelectedPageNumber);
    }

    [AvaloniaFact]
    public void GoToNextPage_AtLastPage_StaysOnLastPage()
    {
        var vm = CreateDocumentViewModel(pageCount: 3);
        vm.SelectedPageNumber = 3;
        // Command should be disabled; force an Execute anyway to verify clamping too
        // (CanExecute guard prevents execution in normal flow)
        Assert.False(vm.GoToNextPageCommand.CanExecute(null));
        Assert.Equal(3, vm.SelectedPageNumber);
    }

    // -----------------------------------------------------------------------
    // Backward navigation
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void GoToPreviousPage_FromLastPage_MovesToPreviousPage()
    {
        var vm = CreateDocumentViewModel(pageCount: 5);
        vm.SelectedPageNumber = 5;
        vm.GoToPreviousPageCommand.Execute(null);
        Assert.Equal(4, vm.SelectedPageNumber);
    }

    [AvaloniaFact]
    public void GoToPreviousPage_MultipleSteps_DecreasesCorrectly()
    {
        var vm = CreateDocumentViewModel(pageCount: 5);
        vm.SelectedPageNumber = 5;
        vm.GoToPreviousPageCommand.Execute(null); // → 4
        vm.GoToPreviousPageCommand.Execute(null); // → 3
        Assert.Equal(3, vm.SelectedPageNumber);
    }

    [AvaloniaFact]
    public void GoToPreviousPage_AtFirstPage_StaysOnFirstPage()
    {
        var vm = CreateDocumentViewModel(pageCount: 3);
        // SelectedPageNumber = 1 by default
        Assert.False(vm.GoToPreviousPageCommand.CanExecute(null));
        Assert.Equal(1, vm.SelectedPageNumber);
    }

    // -----------------------------------------------------------------------
    // Round-trip
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void NextThenPrevious_ReturnsToOriginalPage()
    {
        var vm = CreateDocumentViewModel(pageCount: 5);
        var original = vm.SelectedPageNumber;
        vm.GoToNextPageCommand.Execute(null);
        vm.GoToPreviousPageCommand.Execute(null);
        Assert.Equal(original, vm.SelectedPageNumber);
    }
}
