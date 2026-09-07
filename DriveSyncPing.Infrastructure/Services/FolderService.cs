using DriveSyncPing.Application.Services;
using DriveSyncPing.Domain.Entities;
using DriveSyncPing.Domain.Enums;
using DriveSyncPing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DriveSyncPing.Infrastructure.Services;

public class FolderService : IFolderService
{
    private readonly AppDbContext _context;

    public FolderService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<SyncFolder>> GetFoldersAsync()
    {
        return await _context.SyncFolders.ToListAsync();
    }

    public async Task<SyncFolder?> AddFolderAsync(string path)
    {
        if (!await ValidatePathAsync(path))
            return null;

        var existing = await _context.SyncFolders.FirstOrDefaultAsync(f => f.Path == path);
        if (existing != null)
            return existing;

        var folder = new SyncFolder { Path = path, IsEnabled = true, OrganizationRule = OrganizationRule.None };
        _context.SyncFolders.Add(folder);
        await _context.SaveChangesAsync();
        
        return folder;
    }

    public async Task RemoveFolderAsync(int id)
    {
        var folder = await _context.SyncFolders.FindAsync(id);
        if (folder != null)
        {
            _context.SyncFolders.Remove(folder);
            await _context.SaveChangesAsync();
        }
    }

    public Task<bool> ValidatePathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return Task.FromResult(false);

        try
        {
            Directory.GetDirectories(path);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public async Task UpdateFolderRuleAsync(int id, OrganizationRule rule)
    {
        var folder = await _context.SyncFolders.FindAsync(id);
        if (folder != null)
        {
            folder.OrganizationRule = rule;
            await _context.SaveChangesAsync();
        }
    }
}
