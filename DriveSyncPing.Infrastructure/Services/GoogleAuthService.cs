using DriveSyncPing.Application.Services;
using DriveSyncPing.Infrastructure.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Util.Store;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DriveSyncPing.Infrastructure.Services;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly AppDbContext _context;
    private static readonly string[] Scopes = { DriveService.Scope.DriveFile };
    private UserCredential? _credential;

    public GoogleAuthService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string?> LoginAsync()
    {
        try
        {
            var clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                return "Erro: GOOGLE_CLIENT_ID ou GOOGLE_CLIENT_SECRET não configurado no .env";
            }

            var secrets = new ClientSecrets
            {
                ClientId = clientId,
                ClientSecret = clientSecret
            };

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var tokenPath = Path.Combine(localAppData, "DriveSyncPing", "token.json");

            _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(tokenPath, true));

            return "Conectado ao Google Drive com sucesso!";
        }
        catch (Exception ex)
        {
            return $"Erro ao conectar: {ex.Message}";
        }
    }

    public Task<bool> IsConnectedAsync()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var tokenPath = Path.Combine(localAppData, "DriveSyncPing", "token.json");
        return Task.FromResult(Directory.Exists(tokenPath) && Directory.GetFiles(tokenPath).Length > 0);
    }

    public Task LogoutAsync()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var tokenPath = Path.Combine(localAppData, "DriveSyncPing", "token.json");
        if (Directory.Exists(tokenPath))
        {
            Directory.Delete(tokenPath, true);
        }
        _credential = null;
        return Task.CompletedTask;
    }
}
