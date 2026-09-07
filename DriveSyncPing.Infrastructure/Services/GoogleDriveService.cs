using DriveSyncPing.Application.Services;
using Google.Apis.Drive.v3;
using Google.Apis.Upload;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DriveSyncPing.Infrastructure.Services;

public class GoogleDriveService : IGoogleDriveService
{
    private const string HashPropertyKey = "sha256";
    private readonly IGoogleAuthService _authService;

    public GoogleDriveService(IGoogleAuthService authService)
    {
        _authService = authService;
    }

    private async Task<DriveService> GetServiceAsync()
    {
        var obj = await _authService.GetDriveServiceAsync();
        return obj as DriveService
            ?? throw new InvalidOperationException("Google Drive não está conectado. Faça login antes de sincronizar.");
    }

    private static string EscapeForQuery(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");

    public async Task<string?> CreateFolderAsync(string folderName, string? parentId = null, CancellationToken cancellationToken = default)
    {
        var service = await GetServiceAsync();

        var query = $"name = '{EscapeForQuery(folderName)}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
        query += !string.IsNullOrEmpty(parentId)
            ? $" and '{EscapeForQuery(parentId)}' in parents"
            : " and 'root' in parents";

        var listRequest = service.Files.List();
        listRequest.Q = query;
        listRequest.Fields = "files(id, name)";
        var listResponse = await listRequest.ExecuteAsync(cancellationToken);

        var existingFolder = listResponse.Files?.FirstOrDefault();
        if (existingFolder != null)
            return existingFolder.Id;

        var folderMetadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = folderName,
            MimeType = "application/vnd.google-apps.folder"
        };
        if (!string.IsNullOrEmpty(parentId))
            folderMetadata.Parents = new[] { parentId };

        var request = service.Files.Create(folderMetadata);
        request.Fields = "id";
        var folder = await request.ExecuteAsync(cancellationToken);
        return folder.Id;
    }

    public async Task<RemoteFile?> FindFileAsync(string fileName, string parentFolderId, CancellationToken cancellationToken = default)
    {
        var service = await GetServiceAsync();

        var listRequest = service.Files.List();
        listRequest.Q = $"name = '{EscapeForQuery(fileName)}' and '{EscapeForQuery(parentFolderId)}' in parents and trashed = false";
        listRequest.Fields = "files(id, name, size, appProperties)";
        var response = await listRequest.ExecuteAsync(cancellationToken);

        var file = response.Files?.FirstOrDefault();
        return file == null ? null : ToRemoteFile(file);
    }

    public async Task<RemoteFile?> GetFileAsync(string fileId, CancellationToken cancellationToken = default)
    {
        var service = await GetServiceAsync();

        var getRequest = service.Files.Get(fileId);
        getRequest.Fields = "id, name, size, appProperties, trashed";
        var file = await getRequest.ExecuteAsync(cancellationToken);

        return file == null || file.Trashed == true ? null : ToRemoteFile(file);
    }

    public async Task<RemoteFile?> UploadFileAsync(
        string localFilePath,
        string remoteFolderId,
        string sha256,
        Action<long, long>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var service = await GetServiceAsync();

        var fileName = Path.GetFileName(localFilePath);
        var fileMetadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = fileName,
            Parents = new[] { remoteFolderId },
            AppProperties = new Dictionary<string, string> { [HashPropertyKey] = sha256 }
        };

        long totalBytes = new FileInfo(localFilePath).Length;

        using var stream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read);
        var request = service.Files.Create(fileMetadata, stream, "application/octet-stream");
        request.Fields = "id, name, size, appProperties";

        if (onProgress != null)
        {
            request.ProgressChanged += progress =>
            {
                if (progress.Status == UploadStatus.Uploading)
                    onProgress(progress.BytesSent, totalBytes);
            };
        }

        var response = await request.UploadAsync(cancellationToken);
        if (response.Status != UploadStatus.Completed)
            throw new Exception(response.Exception?.Message ?? "Falha no upload.");

        onProgress?.Invoke(totalBytes, totalBytes);

        var body = request.ResponseBody;
        return body != null
            ? ToRemoteFile(body)
            : new RemoteFile(string.Empty, fileName, totalBytes, sha256);
    }

    private static RemoteFile ToRemoteFile(Google.Apis.Drive.v3.Data.File file)
    {
        string? hash = null;
        file.AppProperties?.TryGetValue(HashPropertyKey, out hash);
        return new RemoteFile(file.Id, file.Name, file.Size, hash);
    }
}
