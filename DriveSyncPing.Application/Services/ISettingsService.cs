using System.Threading.Tasks;

namespace DriveSyncPing.Application.Services;

/// <summary>Strongly typed view over the key/value <c>Configurations</c> table.</summary>
public sealed class AppSettings
{
    /// <summary>Default name of the folder created at the root of Google Drive.</summary>
    public const string DefaultDriveFolderName = "DriveSyncPing";

    public bool DeleteAfterUpload { get; set; }
    public bool DryRunByDefault { get; set; }

    /// <summary>Name of the top-level Drive folder that holds every synced folder.</summary>
    public string DriveFolderName { get; set; } = DefaultDriveFolderName;
}

public interface ISettingsService
{
    Task<AppSettings> GetAsync();
    Task SaveAsync(AppSettings settings);
}
