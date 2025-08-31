using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

            var openDocButton = window.GetVisualDescendants().OfType<Button>().SingleOrDefault(v => v.Name == "PART_AddItemButton");
            Assert.NotNull(openDocButton);

            bool buttonClicked = false;
            openDocButton.Click += (sender, args) => buttonClicked = true;

            var buttonLoc = new Point(openDocButton.Bounds.Left + openDocButton.Bounds.Width / 2,
                                      openDocButton.Bounds.Top + openDocButton.Bounds.Height / 2);
            window.MouseDown(buttonLoc, MouseButton.Left);
            window.MouseUp(buttonLoc, MouseButton.Left);
            Assert.True(buttonClicked);

            //Dispatcher.UIThread.RunJobs();

            while (mvm.PdfDocuments.Count == 0)
            {
                await Task.Delay(500);
            }

            /*
            var doc = mvm.PdfDocuments[mvm.SelectedDocumentIndex];
            Assert.NotNull(doc.WaitOpenAsync);
            //await Task.Run(() => doc.WaitOpenAsync);
            */

            //Dispatcher.UIThread.RunJobs();
            //AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            var frame = window.CaptureRenderedFrame();
            frame?.Save("file.png");
        }

        [AvaloniaFact]
        public async Task CanOpenPdf()
        {
            var window = TestApp.Current.MainWindow;
            Assert.NotNull(window);

            var mvm = TestApp.Current.MainViewModel;
            Assert.NotNull(mvm);

            // Show the window, as it's required to get layout processed:
            window.Show();

            await mvm.OpenFileCommand.ExecuteAsync(null);
            //await Task.Delay(100_000);

            while (mvm.PdfDocuments.Count == 0)
            {
                await Task.Delay(500);
            }

            

            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            var frame = window.CaptureRenderedFrame();
            frame?.Save("file.png");
        }
    }
}
