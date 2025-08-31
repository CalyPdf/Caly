using Avalonia;
using Avalonia.Headless;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Caly.Core;
using Caly.Core.Services.Interfaces;
using Caly.Core.ViewModels;
using Caly.Core.Views;
using Caly.Tests;
using Caly.Tests.Mock;
using Microsoft.Extensions.DependencyInjection;
using Moq;

// https://github.com/AvaloniaUI/Avalonia.Samples/tree/08d80e0d34025e4c243c882661ee30a7f63b0e3f/src/Avalonia.Samples/Testing/TestableApp.Headless.XUnit
// https://docs.avaloniaui.net/docs/concepts/headless/headless-xunit

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Caly.Tests
{
    public class TestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions()
            {
                UseHeadlessDrawing = false
            });
    }

    public class TestApp : App
    {
        private const string _pdfFilePath = @"C:\Users\Bob\source\repos\Caly\Caly.Tests\TIKA-584-0.pdf";

        public new static TestApp Current => App.Current as TestApp;

        public MainWindow? MainWindow { get; private set; }
        public MainViewModel? MainViewModel { get; private set; } 

        public override void OverrideRegisteredServices(ServiceCollection services)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                MainViewModel = new MainViewModel();
                MainWindow = new MainWindow
                {
                    DataContext = MainViewModel,
                    Width = 1000,
                    Height = 500
                };
                services.AddSingleton(_ => (Visual)MainWindow);
            });

            // IStorageProvider
            foreach (var s in services.Where(sd => sd.ServiceType == typeof(IStorageProvider)).ToArray())
            {
                services.Remove(s);
            }

            var file = new Mock<IStorageFile>();
            file.Setup(f => f.Path).Returns(new Uri(_pdfFilePath));
            file.Setup(f => f.OpenReadAsync()).Returns(Task.FromResult((Stream)new FileStream(_pdfFilePath, FileMode.Open)));

            var provider = new Mock<IStorageProvider>();
            provider.Setup(x => x.OpenFilePickerAsync(It.IsAny<FilePickerOpenOptions>())).ReturnsAsync([file.Object]);
            provider.Setup(x => x.CanOpen).Returns(true);
  
            services.AddSingleton(provider.Object);
            //Ioc.Default.ConfigureServices(services.BuildServiceProvider());

            // IClipboardService
            foreach (var s in services.Where(sd => sd.ServiceType == typeof(IClipboardService)).ToArray())
            {
                services.Remove(s);
            }
            services.AddSingleton<IClipboardService, InMemoryClipboardService>();
        }
    }
}
