using System;
using System.Threading.Tasks;
using DriveSyncPing.Domain.Entities;

namespace DriveSyncPing.Application.Services;

public interface IScheduleService
{
    Task<ScheduleConfig> GetAsync();
    Task SaveAsync(ScheduleConfig config);

    /// <summary>
    /// Returns true when an automatic run is due for <paramref name="nowLocal"/> and has not
    /// already been fired for the current slot.
    /// </summary>
    Task<bool> IsRunDueAsync(DateTime nowLocal);

    /// <summary>Records that an automatic run happened, so it is not fired again this slot.</summary>
    Task MarkRunAsync(DateTime whenUtc);
}
