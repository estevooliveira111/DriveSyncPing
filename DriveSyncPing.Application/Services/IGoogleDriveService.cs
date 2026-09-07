using System;
using System.Threading.Tasks;

namespace DriveSyncPing.Application.Services;

public interface IGoogleDriveService
{
    Task<string?> CreateFolderAsync(string folderName, string? parentId = null);
    Task<string?> UploadFileAsync(string localFilePath, string remoteFolderId, Action<long, long> onProgress);
}
