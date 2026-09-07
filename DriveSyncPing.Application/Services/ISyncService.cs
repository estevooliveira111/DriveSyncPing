using System;
using System.Threading;
using System.Threading.Tasks;
using DriveSyncPing.Domain.Entities;
using DriveSyncPing.Domain.Enums;

namespace DriveSyncPing.Application.Services;

/// <summary>Options that control a single synchronization run.</summary>
public sealed class SyncRunOptions
{
    /// <summary>When true nothing is uploaded or deleted; the run only reports what it would do.</summary>
    public bool DryRun { get; init; }

    /// <summary>When true, local files are deleted after the upload has been validated.</summary>
    public bool DeleteAfterUpload { get; init; }

    /// <summary>What started the run (manual button vs. scheduler).</summary>
    public SyncTrigger Trigger { get; init; } = SyncTrigger.Manual;

    public static SyncRunOptions Default => new();
}

public interface ISyncService
{
    Task<SyncJob> RunSyncAsync(
        SyncRunOptions options,
        Action<string, int> onProgressUpdate,
        CancellationToken cancellationToken = default);
}
