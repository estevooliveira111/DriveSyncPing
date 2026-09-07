using DriveSyncPing.Domain.Enums;

namespace DriveSyncPing.Domain.Entities;

public class SyncOperation
{
    public int Id { get; set; }
    public OperationType Type { get; set; }
    public string? Message { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public int SyncJobId { get; set; }
    public SyncJob? SyncJob { get; set; }
    
    public int? SyncFileId { get; set; }
    public SyncFile? SyncFile { get; set; }
}
