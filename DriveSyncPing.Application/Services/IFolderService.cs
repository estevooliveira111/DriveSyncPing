using DriveSyncPing.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DriveSyncPing.Application.Services;

public interface IFolderService
{
    Task<List<SyncFolder>> GetFoldersAsync();
    Task<SyncFolder?> AddFolderAsync(string path);
    Task RemoveFolderAsync(int id);
    Task<bool> ValidatePathAsync(string path);
}
