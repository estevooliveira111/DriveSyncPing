using System.Threading.Tasks;

namespace DriveSyncPing.Application.Services;

/// <summary>Strongly typed view over the key/value <c>Configurations</c> table.</summary>
public sealed class AppSettings
{
    public bool DeleteAfterUpload { get; set; }
    public bool DryRunByDefault { get; set; }
}

public interface ISettingsService
{
    Task<AppSettings> GetAsync();
    Task SaveAsync(AppSettings settings);
}
