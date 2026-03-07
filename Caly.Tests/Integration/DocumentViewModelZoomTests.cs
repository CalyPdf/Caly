using Avalonia.Headless.XUnit;
using Caly.Core.Models;
using Caly.Core.Services;
using Caly.Core.ViewModels;
using Caly.Tests.Integration.Mocks;

namespace Caly.Tests.Integration;

/// <summary>
/// Integration tests for DocumentViewModel zoom behaviour.
/// These tests require Avalonia's UI thread because DocumentViewModel's constructor
/// calls Dispatcher.UIThread.Invoke() to set up the search results tree selection.
/// </summary>
public class DocumentViewModelZoomTests
{
    private static DocumentViewModel CreateDocumentViewModel()
    {
        var pdfService = new MockPdfDocumentService();
        var pageService = new PdfPageService(pdfService);
        var textSearch = new MockTextSearchService();
        var settings = new MockSettingsService();
        return new DocumentViewModel(pdfService, pageService, textSearch, settings);
    }

    // -----------------------------------------------------------------------
    // Default state
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void ZoomLevel_Default_IsOne()
    {
        var vm = CreateDocumentViewModel();
        Assert.Equal(1.0, vm.ZoomLevel);
    }

    [AvaloniaFact]
    public void ZoomInCommand_CanExecute_TrueAtDefault()
    {
        var vm = CreateDocumentViewModel();
        Assert.True(vm.ZoomInCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void ZoomOutCommand_CanExecute_TrueAtDefault()
    {
        var vm = CreateDocumentViewModel();
        Assert.True(vm.ZoomOutCommand.CanExecute(null));
    }

    // -----------------------------------------------------------------------
    // Zoom in
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void ZoomIn_FromDefault_MovesToNextDiscreteLevel()
    {
        var vm = CreateDocumentViewModel();
        // ZoomLevels: ..., 0.75, 1.0, 1.25, ...
        // ZoomLevel starts at 1.0, next step is 1.25
        vm.ZoomInCommand.Execute(null);
        Assert.Equal(1.25, vm.ZoomLevel);
    }

    [AvaloniaFact]
    public void ZoomIn_TwiceFromDefault_MovesToTwoLevelsUp()
    {
        var vm = CreateDocumentViewModel();
        vm.ZoomInCommand.Execute(null); // 1.0 → 1.25
        vm.ZoomInCommand.Execute(null); // 1.25 → 1.5
        Assert.Equal(1.5, vm.ZoomLevel);
    }

    [AvaloniaFact]
    public void ZoomIn_AtMaxLevel_ZoomLevelUnchanged()
    {
        var vm = CreateDocumentViewModel();
        vm.ZoomLevel = vm.MaxZoomLevel; // 64
        var before = vm.ZoomLevel;

        vm.ZoomInCommand.Execute(null);

        Assert.Equal(before, vm.ZoomLevel);
    }

    [AvaloniaFact]
    public void ZoomInCommand_CanExecute_FalseAtMaxLevel()
    {
        var vm = CreateDocumentViewModel();
        vm.ZoomLevel = vm.MaxZoomLevel;
        Assert.False(vm.ZoomInCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void ZoomIn_FromNonDiscreteValue_SnapsToNextDiscreteLevel()
    {
        var vm = CreateDocumentViewModel();
        vm.ZoomLevel = 1.1; // between 1.0 and 1.25 in ZoomLevelsDiscrete
        vm.ZoomInCommand.Execute(null);
        Assert.Equal(1.25, vm.ZoomLevel);
    }

    // -----------------------------------------------------------------------
    // Zoom out
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void ZoomOut_FromDefault_MovesToPreviousDiscreteLevel()
    {
        var vm = CreateDocumentViewModel();
        // ZoomLevels: ..., 0.75, 1.0, 1.25, ...
        // ZoomLevel starts at 1.0, previous step is 0.75
        vm.ZoomOutCommand.Execute(null);
        Assert.Equal(0.75, vm.ZoomLevel);
    }

    [AvaloniaFact]
    public void ZoomOut_TwiceFromDefault_MovesToTwoLevelsDown()
    {
        var vm = CreateDocumentViewModel();
        vm.ZoomOutCommand.Execute(null); // 1.0 → 0.75
        vm.ZoomOutCommand.Execute(null); // 0.75 → 0.67
        Assert.Equal(0.67, vm.ZoomLevel);
    }

    [AvaloniaFact]
    public void ZoomOut_AtMinLevel_ZoomLevelUnchanged()
    {
        var vm = CreateDocumentViewModel();
        vm.ZoomLevel = vm.MinZoomLevel; // 0.08
        var before = vm.ZoomLevel;

        vm.ZoomOutCommand.Execute(null);

        Assert.Equal(before, vm.ZoomLevel);
    }

    [AvaloniaFact]
    public void ZoomOutCommand_CanExecute_FalseAtMinLevel()
    {
        var vm = CreateDocumentViewModel();
        vm.ZoomLevel = vm.MinZoomLevel;
        Assert.False(vm.ZoomOutCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void ZoomOut_FromNonDiscreteValue_SnapsToPreviousDiscreteLevel()
    {
        var vm = CreateDocumentViewModel();
        vm.ZoomLevel = 1.1; // between 1.0 and 1.25
        vm.ZoomOutCommand.Execute(null);
        Assert.Equal(1.0, vm.ZoomLevel);
    }

    // -----------------------------------------------------------------------
    // Zoom in / out symmetry
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void ZoomInThenZoomOut_ReturnsToOriginalLevel()
    {
        var vm = CreateDocumentViewModel();
        var original = vm.ZoomLevel;

        vm.ZoomInCommand.Execute(null);
        vm.ZoomOutCommand.Execute(null);

        Assert.Equal(original, vm.ZoomLevel);
    }

    [AvaloniaFact]
    public void ZoomOutThenZoomIn_ReturnsToOriginalLevel()
    {
        var vm = CreateDocumentViewModel();
        var original = vm.ZoomLevel;

        vm.ZoomOutCommand.Execute(null);
        vm.ZoomInCommand.Execute(null);

        Assert.Equal(original, vm.ZoomLevel);
    }
}
