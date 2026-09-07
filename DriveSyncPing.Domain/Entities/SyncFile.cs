using DriveSyncPing.Domain.Enums;

namespace DriveSyncPing.Domain.Entities;

public class SyncFile
{
    public int Id { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string HashSha256 { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public SyncStatus Status { get; set; } = SyncStatus.Pending;
    public DateTime? LastModified { get; set; }

    /// <summary>Id of the file created on Google Drive (null until uploaded).</summary>
    public string? RemoteFileId { get; set; }

    /// <summary>UTC timestamp of the last successful upload + validation.</summary>
    public DateTime? LastSyncedAt { get; set; }

    public int SyncFolderId { get; set; }
    public SyncFolder? SyncFolder { get; set; }
}
