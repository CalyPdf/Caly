using System;
using Android.App;
using Android.Content.PM;
using Android.Runtime;

using Avalonia;
using Avalonia.Android;
using Caly.Core;

namespace Caly.Android
{
    [Application]
    public class CalyAndroidApplication : AvaloniaAndroidApplication<App>
    {
        protected CalyAndroidApplication(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        { }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            return base.CustomizeAppBuilder(builder)
                .WithInterFont()
                .UseSkia()
                .With(new AndroidPlatformOptions()
                {
                    RenderingMode = new[] { AndroidRenderingMode.Software }
                });
        }
    }

    [Activity(
        Label = "Caly.Android",
        Theme = "@style/MyTheme.NoActionBar",
        Icon = "@drawable/icon",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
    public class MainActivity : AvaloniaMainActivity
    { }
}
