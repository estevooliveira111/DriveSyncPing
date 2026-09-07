using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DriveSyncPing.Application.Services;

namespace DriveSyncPing.Tests.Infrastructure;

/// <summary>In-memory stand-in for Google Drive used by the sync tests.</summary>
public sealed class FakeGoogleDriveService : IGoogleDriveService
{
    private sealed class Node
    {
        public string Id = "";
        public string Name = "";
        public string? ParentId;
        public bool IsFolder;
        public long Size;
        public string? Sha256;
        public byte[] Content = Array.Empty<byte>();
    }

    private readonly List<Node> _nodes = new();
    private int _seq;

    public int UploadCount { get; private set; }

    /// <summary>When set, the reported size for validation is forced to this value.</summary>
    public long? ForceReportedSize { get; set; }

    /// <summary>Names that should throw on upload, to exercise failure handling.</summary>
    public HashSet<string> FailUploadForNames { get; } = new();

    /// <summary>Seeds an already-existing remote file so duplicate detection can be tested.</summary>
    public void SeedFile(string folderId, string name, long size, string? sha256)
    {
        _nodes.Add(new Node
        {
            Id = $"seed-{++_seq}",
            Name = name,
            ParentId = folderId,
            IsFolder = false,
            Size = size,
            Sha256 = sha256
        });
    }

    public Task<string?> CreateFolderAsync(string folderName, string? parentId = null, CancellationToken cancellationToken = default)
    {
        var existing = _nodes.FirstOrDefault(n =>
            n.IsFolder && n.Name == folderName && n.ParentId == (parentId ?? "root"));
        if (existing != null)
            return Task.FromResult<string?>(existing.Id);

        var node = new Node
        {
            Id = $"folder-{++_seq}",
            Name = folderName,
            ParentId = parentId ?? "root",
            IsFolder = true
        };
        _nodes.Add(node);
        return Task.FromResult<string?>(node.Id);
    }

    public Task<RemoteFile?> FindFileAsync(string fileName, string parentFolderId, CancellationToken cancellationToken = default)
    {
        var node = _nodes.FirstOrDefault(n => !n.IsFolder && n.Name == fileName && n.ParentId == parentFolderId);
        return Task.FromResult(node == null ? null : new RemoteFile(node.Id, node.Name, node.Size, node.Sha256));
    }

    public Task<RemoteFile?> GetFileAsync(string fileId, CancellationToken cancellationToken = default)
    {
        var node = _nodes.FirstOrDefault(n => n.Id == fileId);
        if (node == null)
            return Task.FromResult<RemoteFile?>(null);

        var size = ForceReportedSize ?? node.Size;
        return Task.FromResult<RemoteFile?>(new RemoteFile(node.Id, node.Name, size, node.Sha256));
    }

    public Task<RemoteFile?> UploadFileAsync(
        string localFilePath,
        string remoteFolderId,
        string sha256,
        Action<long, long>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var name = Path.GetFileName(localFilePath);
        if (FailUploadForNames.Contains(name))
            throw new IOException($"Falha simulada para {name}");

        UploadCount++;
        var bytes = File.ReadAllBytes(localFilePath);

        var node = new Node
        {
            Id = $"file-{++_seq}",
            Name = name,
            ParentId = remoteFolderId,
            IsFolder = false,
            Size = bytes.Length,
            Sha256 = sha256,
            Content = bytes
        };
        _nodes.Add(node);

        onProgress?.Invoke(bytes.Length, bytes.Length);
        return Task.FromResult<RemoteFile?>(new RemoteFile(node.Id, node.Name, node.Size, node.Sha256));
    }
}
