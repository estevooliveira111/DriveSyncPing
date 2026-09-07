using System.Collections.Generic;
using System.Threading.Tasks;
using DriveSyncPing.Domain.Entities;

namespace DriveSyncPing.Application.Services;

public interface IHistoryService
{
    /// <summary>Most recent sync jobs, newest first.</summary>
    Task<List<SyncJob>> GetJobsAsync(int limit = 50);

    /// <summary>Operations logged for a given job, oldest first.</summary>
    Task<List<SyncOperation>> GetOperationsAsync(int jobId);

    /// <summary>Operations across every job, newest first (global log view).</summary>
    Task<List<SyncOperation>> GetRecentOperationsAsync(int limit = 200);
}
