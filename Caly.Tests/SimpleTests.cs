using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Caly.Core.Controls;
using Caly.Core.ViewModels;
using Caly.Core.Views;

namespace Caly.Tests
{
    // https://docs.avaloniaui.net/docs/concepts/headless/

    public class SimpleTests
    {
        [AvaloniaFact]
        public async Task CanStart()
        {
            var window = TestApp.Current.MainWindow;
            Assert.NotNull(window);

            var mvm = TestApp.Current.MainViewModel;
            Assert.NotNull(mvm);

            // Show the window, as it's required to get layout processed:
            window.Show();

            Dispatcher.UIThread.RunJobs();

            var openDocButton = window.GetVisualDescendants().OfType<Button>().SingleOrDefault(v => v.Name == "PART_AddItemButton");
            Assert.NotNull(openDocButton);

            bool buttonClicked = false;
            openDocButton.Click += (sender, args) => buttonClicked = true;

            var buttonLoc = new Point(openDocButton.Bounds.Left + openDocButton.Bounds.Width / 2,
                                      openDocButton.Bounds.Top + openDocButton.Bounds.Height / 2);

            window.MouseDown(buttonLoc, MouseButton.Left);
            window.MouseUp(buttonLoc, MouseButton.Left);

            Assert.True(buttonClicked);

            Dispatcher.UIThread.RunJobs();

            var frame = window.CaptureRenderedFrame();
            frame?.Save("file.png");
        }

        [AvaloniaFact]
        public async Task CanOpenPdfAndLoadPages()
        {
            var window = TestApp.Current.MainWindow;
            Assert.NotNull(window);

            var mvm = TestApp.Current.MainViewModel;
            Assert.NotNull(mvm);

            window.Show();

            Dispatcher.UIThread.RunJobs();

            await mvm.OpenFileCommand.ExecuteAsync(null);

            await WaitWhile(() => mvm.PdfDocuments.Count == 0);

            var doc = mvm.PdfDocuments[0];
            Assert.NotNull(doc);

            Dispatcher.UIThread.RunJobs();

            var docsControl = window.FindDescendantOfType<PdfDocumentsTabsControl>();
            Assert.NotNull(docsControl);

            var frame = window.CaptureRenderedFrame();
            frame?.Save("file.png");

            await WaitWhile(() => doc.Pages.Count == 0);

            Dispatcher.UIThread.RunJobs();

            frame = window.CaptureRenderedFrame();
            frame?.Save("file_1.png");

            var docSplitView = docsControl.GetVisualDescendants()
                .OfType<SplitView>()
                .SingleOrDefault(v => v.Name == "PART_SplitView");
            Assert.NotNull(docSplitView);

            var docControl = docSplitView.FindDescendantOfType<PdfDocumentControl>();
            Assert.NotNull(docControl);

            var leftNavigationBar = docsControl.GetVisualDescendants()
                .OfType<TabControl>()
                .SingleOrDefault(v => v.Name == "PART_TabControlNavigation");
            Assert.NotNull(leftNavigationBar);

            var selectedNavigationTab = leftNavigationBar.SelectedContent;
            var docThumbnails = selectedNavigationTab as PdfDocumentThumbnailControl;
            Assert.NotNull(docThumbnails);

            await WaitWhile(() =>
            {
                int pageCount = Math.Max(doc.Pages.Count, 3); // Load max first 3 pages
                return doc.Pages
                    .Take(pageCount)
                    .Any(p => p is { IsPageVisible: true, IsPageRendering: true } || p.PdfTextLayer is null);
            });

            frame = window.CaptureRenderedFrame();
            frame?.Save("file_2.png");

            var listBoxThumbnails = docThumbnails.GetVisualDescendants()
                .OfType<ListBox>()
                .SingleOrDefault(v => v.Name == "PART_ListBox");
            Assert.NotNull(listBoxThumbnails);

            Dispatcher.UIThread.RunJobs();

            var realisedListBoxThumbnails = listBoxThumbnails
                .GetRealizedContainers()
                .OfType<ListBoxItem>()
                .ToArray();
            Assert.NotEmpty(realisedListBoxThumbnails);
            Assert.True(realisedListBoxThumbnails[0].IsSelected);

            var realisedThumbnails = realisedListBoxThumbnails
                .SelectMany(l => l.GetVisualDescendants().OfType<PdfPageThumbnailControl>())
                .ToArray();
            Assert.NotEmpty(realisedThumbnails);

            await WaitWhile(() =>
            {
                return realisedThumbnails.Any(t => t is { VisibleArea: not null, Thumbnail: null });
            });
            
            Dispatcher.UIThread.RunJobs();

            frame = window.CaptureRenderedFrame();
            frame?.Save("file_3.png");
        }

        private static async Task WaitWhile(Func<bool> condition, int waitForMs = 500, int maxWaitMs = 30 * 1000)
        {
            var waited = 0;
            while (condition() && waited < maxWaitMs)
            {
                await Task.Delay(waitForMs);
                waited += waitForMs;
            }
        }
    }
}
