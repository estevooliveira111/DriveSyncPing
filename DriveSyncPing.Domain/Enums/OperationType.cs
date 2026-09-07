namespace DriveSyncPing.Domain.Enums;

public enum OperationType
{
    Upload,
    DeleteLocal,
    Ignore,
    Error,
    SyncStarted,
    SyncCompleted,
    ValidationPassed,
    ValidationFailed,
    Cancelled,
    DryRunUpload,
    DryRunDelete
}
