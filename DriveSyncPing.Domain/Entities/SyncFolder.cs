namespace DriveSyncPing.Domain.Entities;

public class SyncFolder
{
    public int Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    
    public ICollection<SyncFile> Files { get; set; } = new List<SyncFile>();
}
