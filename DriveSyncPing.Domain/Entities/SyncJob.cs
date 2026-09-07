using DriveSyncPing.Domain.Enums;

namespace DriveSyncPing.Domain.Entities;

public class SyncJob
{
    public int Id { get; set; }
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Running;
    public string? ErrorMessage { get; set; }

    public bool IsDryRun { get; set; }
    public SyncTrigger Trigger { get; set; } = SyncTrigger.Manual;

    public int FilesUploaded { get; set; }
    public int FilesIgnored { get; set; }
    public int FilesFailed { get; set; }
    public int FilesDeleted { get; set; }

    public ICollection<SyncOperation> Operations { get; set; } = new List<SyncOperation>();
}
