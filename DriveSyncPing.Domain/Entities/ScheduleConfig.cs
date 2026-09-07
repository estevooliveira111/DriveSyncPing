namespace DriveSyncPing.Domain.Entities;

/// <summary>
/// Single-row entity that holds the automatic-sync schedule.
/// </summary>
public class ScheduleConfig
{
    public int Id { get; set; }

    public bool IsEnabled { get; set; }

    /// <summary>Comma-separated <see cref="DayOfWeek"/> integers (0 = Sunday). Empty means every day.</summary>
    public string DaysOfWeek { get; set; } = string.Empty;

    /// <summary>Time of day to run, stored as "HH:mm" (local time).</summary>
    public string TimeOfDay { get; set; } = "02:00";

    /// <summary>UTC timestamp of the last automatic run, used to avoid double firing.</summary>
    public DateTime? LastRunUtc { get; set; }

    public IEnumerable<DayOfWeek> GetDays()
    {
        if (string.IsNullOrWhiteSpace(DaysOfWeek))
            return Enum.GetValues<DayOfWeek>();

        return DaysOfWeek
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => int.TryParse(s, out _))
            .Select(s => (DayOfWeek)int.Parse(s))
            .Distinct()
            .ToArray();
    }

    public bool TryGetTime(out TimeSpan time) =>
        TimeSpan.TryParse(TimeOfDay, out time);
}
