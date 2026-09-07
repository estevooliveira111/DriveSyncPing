using DriveSyncPing.Application.Services;
using DriveSyncPing.Domain.Entities;
using DriveSyncPing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DriveSyncPing.Infrastructure.Services;

public class ScheduleService : IScheduleService
{
    private readonly AppDbContext _context;

    public ScheduleService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ScheduleConfig> GetAsync()
    {
        var config = await _context.ScheduleConfigs.FirstOrDefaultAsync();
        if (config == null)
        {
            config = new ScheduleConfig();
            _context.ScheduleConfigs.Add(config);
            await _context.SaveChangesAsync();
        }
        return config;
    }

    public async Task SaveAsync(ScheduleConfig config)
    {
        var existing = await _context.ScheduleConfigs.FirstOrDefaultAsync();
        if (existing == null)
        {
            _context.ScheduleConfigs.Add(config);
        }
        else
        {
            existing.IsEnabled = config.IsEnabled;
            existing.DaysOfWeek = config.DaysOfWeek;
            existing.TimeOfDay = config.TimeOfDay;
            existing.LastRunUtc = config.LastRunUtc;
        }
        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsRunDueAsync(DateTime nowLocal)
    {
        var config = await _context.ScheduleConfigs.AsNoTracking().FirstOrDefaultAsync();
        if (config is not { IsEnabled: true })
            return false;

        if (!config.GetDays().Contains(nowLocal.DayOfWeek))
            return false;

        if (!config.TryGetTime(out var scheduled))
            return false;

        var scheduledToday = nowLocal.Date + scheduled;
        if (nowLocal < scheduledToday)
            return false;

        return config.LastRunUtc == null || config.LastRunUtc.Value.ToLocalTime() < scheduledToday;
    }

    public async Task MarkRunAsync(DateTime whenUtc)
    {
        var config = await GetAsync();
        config.LastRunUtc = whenUtc;
        await _context.SaveChangesAsync();
    }
}
