using DriveSyncPing.Application.Services;
using DriveSyncPing.Domain.Entities;
using DriveSyncPing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace DriveSyncPing.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private const string DeleteAfterUploadKey = "DeleteAfterUpload";
    private const string DryRunByDefaultKey = "DryRunByDefault";

    private readonly AppDbContext _context;

    public SettingsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AppSettings> GetAsync()
    {
        var rows = await _context.Configurations.ToDictionaryAsync(c => c.Key, c => c.Value);
        return new AppSettings
        {
            DeleteAfterUpload = ReadBool(rows, DeleteAfterUploadKey),
            DryRunByDefault = ReadBool(rows, DryRunByDefaultKey)
        };
    }

    public async Task SaveAsync(AppSettings settings)
    {
        await UpsertAsync(DeleteAfterUploadKey, settings.DeleteAfterUpload ? "true" : "false");
        await UpsertAsync(DryRunByDefaultKey, settings.DryRunByDefault ? "true" : "false");
        await _context.SaveChangesAsync();
    }

    private static bool ReadBool(System.Collections.Generic.IReadOnlyDictionary<string, string> rows, string key) =>
        rows.TryGetValue(key, out var value) && value == "true";

    private async Task UpsertAsync(string key, string value)
    {
        var row = await _context.Configurations.FirstOrDefaultAsync(c => c.Key == key);
        if (row == null)
            _context.Configurations.Add(new AppConfiguration { Key = key, Value = value });
        else
            row.Value = value;
    }
}
