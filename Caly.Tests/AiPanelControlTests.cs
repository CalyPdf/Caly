using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Caly.Core.Controls;
using Caly.Core.Services;
using Caly.Tests.Fakes;
using static Caly.Tests.Fakes.DocumentTestHarness;

namespace Caly.Tests;

public class AiPanelControlTests
{
    private static Styles LoadIcons()
    {
        // DocumentTabView references icon geometries via StaticResource, which the test app
        // does not load by default.
        var icons = (Styles)AvaloniaXamlLoader.Load(new Uri("avares://Caly.Core/Assets/Icons.axaml"));
        Application.Current!.Styles.Add(icons);
        return icons;
    }

    private static TabItem AiTab(DocumentTabView view)
    {
        var tabControl = view.FindControl<TabControl>("PART_TabControlNavigation")!;
        return tabControl.Items.Cast<TabItem>().Single(t => ToolTip.GetTip((Control)t.Header!) as string == "AI");
    }

    [AvaloniaFact]
    public async Task Tab_is_visible_when_the_document_has_an_ai_panel()
    {
        var icons = LoadIcons();
        try
        {
            var pdfService = new RenderingPdfDocumentService();
            await using var pageService = new PdfPageService(pdfService);
            var document = NewLoadedDocument(pdfService, pageService, ai: new FakeDocumentAiService());

            var view = new DocumentTabView { DataContext = document };

            Assert.True(AiTab(view).IsVisible);
        }
        finally
        {
            Application.Current!.Styles.Remove(icons);
        }
    }

    [AvaloniaFact]
    public async Task Tab_is_hidden_without_an_ai_service()
    {
        var icons = LoadIcons();
        try
        {
            var pdfService = new RenderingPdfDocumentService();
            await using var pageService = new PdfPageService(pdfService);
            var document = NewLoadedDocument(pdfService, pageService);

            var view = new DocumentTabView { DataContext = document };

            Assert.False(AiTab(view).IsVisible);
        }
        finally
        {
            Application.Current!.Styles.Remove(icons);
        }
    }

    [AvaloniaFact]
    public async Task Panel_binds_to_its_view_model()
    {
        var pdfService = new RenderingPdfDocumentService();
        await using var pageService = new PdfPageService(pdfService);
        var document = NewLoadedDocument(pdfService, pageService, ai: new FakeDocumentAiService());

        var panel = new AiPanelControl { DataContext = document.Ai };
        var window = new Window { Content = panel };
        window.Show();

        var buttons = panel.GetVisualDescendants().OfType<Button>().Select(b => b.Content as string).ToArray();
        Assert.Contains("Run", buttons);
        Assert.Contains("Copy", buttons);
        Assert.Contains("Save", buttons);
        Assert.Contains("Clear", buttons);

        var status = panel.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text);
        Assert.Contains(document.Ai!.StatusText, status);
    }
}
