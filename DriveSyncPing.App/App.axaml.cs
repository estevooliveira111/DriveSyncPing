using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DriveSyncPing.App.ViewModels;
using DriveSyncPing.App.Views;
using DriveSyncPing.Application.Services;
using DriveSyncPing.Infrastructure.Data;
using DriveSyncPing.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace DriveSyncPing.App;

public partial class App : Avalonia.Application
{
    public static IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        // Infrastructure
        services.AddDriveSyncPingDatabase();
        services.AddTransient<IFolderService, FolderService>();
        services.AddSingleton<IGoogleAuthService, GoogleAuthService>();
        services.AddTransient<IGoogleDriveService, GoogleDriveService>();
        services.AddTransient<ISyncService, SyncService>();
        services.AddTransient<ISettingsService, SettingsService>();
        services.AddTransient<IHistoryService, HistoryService>();
        services.AddTransient<IScheduleService, ScheduleService>();

        // ViewModels
        services.AddTransient<MainViewModel>();

        Services = services.BuildServiceProvider();

        // Migrate DB on startup
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
