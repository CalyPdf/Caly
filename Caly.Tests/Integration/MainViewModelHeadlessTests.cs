using Avalonia.Headless.XUnit;
using Caly.Core.ViewModels;

namespace Caly.Tests.Integration;

/// <summary>
/// Integration tests for MainViewModel.
/// These correspond to the "MainWindowHeadlessTests" category from the original plan:
/// window starts with no open tabs and basic multi-document navigation helpers behave correctly.
/// [AvaloniaFact] is used because MainViewModel internally subscribes to Reactive extensions
/// and the headless dispatcher provides a stable threading environment.
/// </summary>
public class MainViewModelHeadlessTests
{
    // -----------------------------------------------------------------------
    // Initial state ("no tabs initially")
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void PdfDocuments_InitiallyEmpty()
    {
        var vm = new MainViewModel();
        Assert.Empty(vm.PdfDocuments);
    }

    [AvaloniaFact]
    public void SelectedDocumentIndex_Default_IsZero()
    {
        var vm = new MainViewModel();
        Assert.Equal(0, vm.SelectedDocumentIndex);
    }

    // -----------------------------------------------------------------------
    // ActivateNextDocument / ActivatePreviousDocument
    // -----------------------------------------------------------------------

    [AvaloniaFact]
    public void ActivateNextDocument_WithNoDocuments_DoesNotThrowOrChange()
    {
        var vm = new MainViewModel();
        // Should be a no-op when PdfDocuments is empty (lastIndex <= 0)
        var before = vm.SelectedDocumentIndex;
        vm.ActivateNextDocumentCommand.Execute(null);
        Assert.Equal(before, vm.SelectedDocumentIndex);
    }

    [AvaloniaFact]
    public void ActivatePreviousDocument_WithNoDocuments_DoesNotThrowOrChange()
    {
        var vm = new MainViewModel();
        var before = vm.SelectedDocumentIndex;
        vm.ActivatePreviousDocumentCommand.Execute(null);
        Assert.Equal(before, vm.SelectedDocumentIndex);
    }

    [AvaloniaFact]
    public void IsSettingsPaneOpen_Default_IsFalse()
    {
        var vm = new MainViewModel();
        Assert.False(vm.IsSettingsPaneOpen);
    }

    [AvaloniaFact]
    public void Version_IsNotNullOrEmpty()
    {
        var vm = new MainViewModel();
        Assert.False(string.IsNullOrWhiteSpace(vm.Version));
    }
}
