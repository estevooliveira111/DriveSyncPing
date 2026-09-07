using System;
using System.Threading;
using System.Threading.Tasks;

namespace DriveSyncPing.Application.Services;

/// <summary>Lightweight view of a file that lives on Google Drive.</summary>
public sealed record RemoteFile(string Id, string Name, long? SizeBytes, string? Sha256);

public interface IGoogleDriveService
{
    Task<string?> CreateFolderAsync(string folderName, string? parentId = null, CancellationToken cancellationToken = default);

    /// <summary>Returns the file with <paramref name="fileName"/> inside <paramref name="parentFolderId"/>, or null.</summary>
    Task<RemoteFile?> FindFileAsync(string fileName, string parentFolderId, CancellationToken cancellationToken = default);

    /// <summary>Reads the metadata (size + stored hash) of an already uploaded file.</summary>
    Task<RemoteFile?> GetFileAsync(string fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a local file and records <paramref name="sha256"/> as an appProperty so future
    /// runs can detect duplicates. Returns the created remote file.
    /// </summary>
    Task<RemoteFile?> UploadFileAsync(
        string localFilePath,
        string remoteFolderId,
        string sha256,
        Action<long, long>? onProgress = null,
        CancellationToken cancellationToken = default);
}
