using System.Threading.Tasks;

namespace DriveSyncPing.Application.Services;

public interface IGoogleAuthService
{
    Task<string?> LoginAsync();
    Task<bool> IsConnectedAsync();
    Task LogoutAsync();
    
    // New: expose the object that represents the authorized service.
    // We use object to avoid Application depending on Google.Apis
    // but in a real clean architecture we might do it differently.
    // For this MVP, we'll return an object that Infrastructure can cast.
    Task<object?> GetDriveServiceAsync();
}
