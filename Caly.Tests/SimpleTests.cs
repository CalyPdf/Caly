using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Caly.Core.ViewModels;
using Caly.Core.Views;

namespace Caly.Tests
{
    // https://docs.avaloniaui.net/docs/concepts/headless/

    public class SimpleTests
    {
        [AvaloniaFact]
        public void CanStart()
        {
            // Create a window and set the view model as its data context:
            var window = new MainWindow
            {
                DataContext = new MainViewModel()
            };

            // Show the window, as it's required to get layout processed:
            window.Show();

            var openDocButton = window.GetVisualDescendants().OfType<Button>().SingleOrDefault(v => v.Name == "PART_AddItemButton");
            Assert.NotNull(openDocButton);

            bool buttonClicked = false;
            openDocButton.Click += (sender, args) => buttonClicked  = true;

            var buttonLoc = new Point(openDocButton.Bounds.Left + openDocButton.Bounds.Width / 2, 
                                      openDocButton.Bounds.Top + openDocButton.Bounds.Height / 2);
            window.MouseDown(buttonLoc, MouseButton.Left);
            window.MouseUp(buttonLoc, MouseButton.Left);
            Assert.True(buttonClicked);
        }
    }
}
