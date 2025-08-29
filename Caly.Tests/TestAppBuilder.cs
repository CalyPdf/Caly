using Avalonia;
using Avalonia.Headless;
using Caly.Core;
using Caly.Core.Services.Interfaces;
using Caly.Tests;
using Caly.Tests.Mock;
using Microsoft.Extensions.DependencyInjection;

// https://github.com/AvaloniaUI/Avalonia.Samples/tree/08d80e0d34025e4c243c882661ee30a7f63b0e3f/src/Avalonia.Samples/Testing/TestableApp.Headless.XUnit
// https://docs.avaloniaui.net/docs/concepts/headless/headless-xunit

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Caly.Tests
{
    public class TestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    public class TestApp : App
    {
        public override void RegisterLifetimeDependantServices(ServiceCollection services)
        {
            services.AddSingleton<IClipboardService, InMemoryClipboardService>();
        }
    }
}
