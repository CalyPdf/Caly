using Avalonia;
using Avalonia.Headless.XUnit;
using Caly.Core.Controls;
using Caly.Core.Models.Ai;
using Caly.Core.Services;
using Caly.Tests.Fakes;
using CommunityToolkit.Mvvm.Input;
using static Caly.Tests.Fakes.DocumentTestHarness;

namespace Caly.Tests;

public class AiPageBoxTests
{
    private const string TableJson = """{"items":[{"kind":"table","page":2,"box":[0.1,0.1,0.5,0.5],"csv":"a,b\n1,2"},{"kind":"chart","page":3,"csv":"x,y"}]}""";

    [Fact]
    public void Box_maps_to_the_page_size_with_top_left_origin()
    {
        var rect = new AiBox(0.1, 0.2, 0.5, 0.6).ToRect(new Size(200, 100));

        Assert.Equal(new Rect(20, 20, 80, 40), rect);
    }

    [Fact]
    public void Panel_enum_indexes_follow_the_properties()
    {
        var ai = new FakeDocumentAiService();
        var doc = new AiTestDocumentService();
        var vm = new Caly.Core.ViewModels.AiPanelViewModel(ai, doc, () => 1, () => 3, _ => Task.CompletedTask, (_, _) => Task.CompletedTask);

        vm.ActionIndex = 2;
        vm.ScopeIndex = 1;

        Assert.Equal(AiAction.ExtractCharts, vm.Action);
        Assert.Equal(AiScope.PageRange, vm.Scope);
        Assert.True(vm.IsRangeScope);

        vm.Scope = AiScope.WholeDocument;
        Assert.Equal(2, vm.ScopeIndex);
        Assert.False(vm.IsRangeScope);
    }

    [AvaloniaFact]
    public void Click_inside_a_box_runs_the_command_with_the_item_index()
    {
        var control = new PageInteractiveLayerControl();
        int? clicked = null;
        control.AiBoxCommand = new RelayCommand<int>(i => clicked = i);
        control.AiBoxes = [new AiPageBox(new AiBox(0.1, 0.1, 0.5, 0.5), 7, false)];
        control.Measure(new Size(200, 100));
        control.Arrange(new Rect(0, 0, 200, 100));

        Assert.True(control.TryActivateAiBox(new Point(50, 30)));
        Assert.Equal(7, clicked);

        clicked = null;
        Assert.False(control.TryActivateAiBox(new Point(180, 90)));
        Assert.Null(clicked);
    }

    [AvaloniaFact]
    public async Task Results_put_boxes_on_their_pages_and_clear_removes_them()
    {
        var pdfService = new RenderingPdfDocumentService();
        await using var pageService = new PdfPageService(pdfService);
        var ai = new FakeDocumentAiService();
        ai.Enqueue(TableJson);
        var document = NewLoadedDocument(pdfService, pageService, pageCount: 3, ai: ai);
        document.Ai!.Action = AiAction.ExtractTables;
        document.Ai.Scope = AiScope.WholeDocument;

        await document.Ai.RunCommand.ExecuteAsync(null);

        Assert.Equal(2, document.Ai.Items.Count);
        var box = Assert.Single(document.Pages[1].AiBoxes!);
        Assert.Equal(new AiBox(0.1, 0.1, 0.5, 0.5), box.Box);
        Assert.Equal(0, box.ItemIndex);
        Assert.False(box.IsSelected);
        Assert.Null(document.Pages[0].AiBoxes);
        Assert.Null(document.Pages[2].AiBoxes); // Page 3's item has no box: it is only listed.

        document.Ai.ClearCommand.Execute(null);

        Assert.Null(document.Pages[1].AiBoxes);
    }

    [AvaloniaFact]
    public async Task Selecting_an_item_goes_to_its_page_and_marks_its_box()
    {
        var pdfService = new RenderingPdfDocumentService();
        await using var pageService = new PdfPageService(pdfService);
        var ai = new FakeDocumentAiService();
        ai.Enqueue(TableJson);
        var document = NewLoadedDocument(pdfService, pageService, pageCount: 3, ai: ai);
        document.Ai!.Action = AiAction.ExtractTables;
        document.Ai.Scope = AiScope.WholeDocument;
        await document.Ai.RunCommand.ExecuteAsync(null);
        document.SelectedPageNumber = 1;

        document.Ai.SelectItemCommand.Execute(1);

        Assert.Equal(3, document.SelectedPageNumber);

        document.Ai.SelectItemCommand.Execute(0);

        Assert.Equal(2, document.SelectedPageNumber);
        Assert.True(Assert.Single(document.Pages[1].AiBoxes!).IsSelected);
    }
}
