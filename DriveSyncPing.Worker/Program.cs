using DriveSyncPing.Application.Services;
using DriveSyncPing.Infrastructure.Data;
using DriveSyncPing.Infrastructure.Services;
using DriveSyncPing.Worker;
using Microsoft.EntityFrameworkCore;

DotNetEnv.Env.TraversePath().Load();

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDriveSyncPingDatabase();
builder.Services.AddTransient<IFolderService, FolderService>();
builder.Services.AddSingleton<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddTransient<IGoogleDriveService, GoogleDriveService>();
builder.Services.AddTransient<ISyncService, SyncService>();
builder.Services.AddTransient<ISettingsService, SettingsService>();
builder.Services.AddTransient<IHistoryService, HistoryService>();
builder.Services.AddTransient<IScheduleService, ScheduleService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

host.Run();
