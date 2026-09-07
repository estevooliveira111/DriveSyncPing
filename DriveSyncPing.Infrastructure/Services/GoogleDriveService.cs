using DriveSyncPing.Application.Services;
using Google.Apis.Drive.v3;
using Google.Apis.Upload;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace DriveSyncPing.Infrastructure.Services;

public class GoogleDriveService : IGoogleDriveService
{
    private readonly IGoogleAuthService _authService;

    public GoogleDriveService(IGoogleAuthService authService)
    {
        _authService = authService;
    }

    private async Task<DriveService?> GetServiceAsync()
    {
        var obj = await _authService.GetDriveServiceAsync();
        return obj as DriveService;
    }

    public async Task<string?> CreateFolderAsync(string folderName, string? parentId = null)
    {
        var service = await GetServiceAsync();
        if (service == null) return null;

        // Check if exists
        var query = $"name = '{folderName}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
        if (!string.IsNullOrEmpty(parentId))
        {
            query += $" and '{parentId}' in parents";
        }

        var listRequest = service.Files.List();
        listRequest.Q = query;
        listRequest.Fields = "files(id, name)";
        var listResponse = await listRequest.ExecuteAsync();

        var existingFolder = listResponse.Files?.FirstOrDefault();
        if (existingFolder != null)
        {
            return existingFolder.Id;
        }

        // Create new
        var folderMetadata = new Google.Apis.Drive.v3.Data.File()
        {
            Name = folderName,
            MimeType = "application/vnd.google-apps.folder"
        };

        if (!string.IsNullOrEmpty(parentId))
        {
            folderMetadata.Parents = new[] { parentId };
        }

        var request = service.Files.Create(folderMetadata);
        request.Fields = "id";
        var folder = await request.ExecuteAsync();

        return folder.Id;
    }

    public async Task<string?> UploadFileAsync(string localFilePath, string remoteFolderId, Action<long, long> onProgress)
    {
        var service = await GetServiceAsync();
        if (service == null) return null;

        var fileName = Path.GetFileName(localFilePath);
        var fileMetadata = new Google.Apis.Drive.v3.Data.File()
        {
            Name = fileName,
            Parents = new[] { remoteFolderId }
        };

        var fileInfo = new FileInfo(localFilePath);
        long totalBytes = fileInfo.Length;

        string mimeType = "application/octet-stream";
        // Simplistic mime type check could be added here

        using (var stream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read))
        {
            var request = service.Files.Create(fileMetadata, stream, mimeType);
            request.Fields = "id";
            
            request.ProgressChanged += (IUploadProgress progress) =>
            {
                if (progress.Status == UploadStatus.Uploading)
                {
                    onProgress(progress.BytesSent, totalBytes);
                }
            };

            var response = await request.UploadAsync();

            if (response.Status == UploadStatus.Completed)
            {
                return request.ResponseBody?.Id;
            }
            
            throw new Exception(response.Exception?.Message ?? "Upload failed");
        }
    }
}
