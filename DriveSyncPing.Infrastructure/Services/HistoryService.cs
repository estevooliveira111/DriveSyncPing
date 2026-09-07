using DriveSyncPing.Application.Services;
using DriveSyncPing.Domain.Entities;
using DriveSyncPing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DriveSyncPing.Infrastructure.Services;

public class HistoryService : IHistoryService
{
    private readonly AppDbContext _context;

    public HistoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<SyncJob>> GetJobsAsync(int limit = 50) =>
        await _context.SyncJobs
            .AsNoTracking()
            .OrderByDescending(j => j.StartDate)
            .Take(limit)
            .ToListAsync();

    public async Task<List<SyncOperation>> GetOperationsAsync(int jobId) =>
        await _context.SyncOperations
            .AsNoTracking()
            .Where(o => o.SyncJobId == jobId)
            .OrderBy(o => o.Timestamp)
            .ThenBy(o => o.Id)
            .ToListAsync();

    public async Task<List<SyncOperation>> GetRecentOperationsAsync(int limit = 200) =>
        await _context.SyncOperations
            .AsNoTracking()
            .OrderByDescending(o => o.Timestamp)
            .ThenByDescending(o => o.Id)
            .Take(limit)
            .ToListAsync();
}
