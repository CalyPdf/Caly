using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Caly.Tests.Integration.TestAppBuilder))]

namespace Caly.Tests.Integration;

public sealed class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Application>();
}
