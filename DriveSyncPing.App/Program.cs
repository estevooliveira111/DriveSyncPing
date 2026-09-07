using Avalonia;
using System;
using DotNetEnv;

namespace DriveSyncPing.App;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args) 
    {
        // Load .env
        Env.Load("../.env");
        Env.Load(".env");

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
