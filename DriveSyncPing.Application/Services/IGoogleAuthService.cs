using System.Threading.Tasks;

namespace DriveSyncPing.Application.Services;

public interface IGoogleAuthService
{
    Task<string?> LoginAsync();
    Task<bool> IsConnectedAsync();
    Task LogoutAsync();
}
