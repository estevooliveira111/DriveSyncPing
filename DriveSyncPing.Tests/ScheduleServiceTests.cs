using System;
using System.Threading.Tasks;
using DriveSyncPing.Domain.Entities;
using DriveSyncPing.Infrastructure.Services;
using DriveSyncPing.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace DriveSyncPing.Tests;

public class ScheduleServiceTests
{
    [Fact]
    public async Task Get_creates_default_config_when_missing()
    {
        using var db = new TestDatabase();
        await using var ctx = db.CreateContext();

        var config = await new ScheduleService(ctx).GetAsync();

        config.Id.Should().BeGreaterThan(0);
        config.IsEnabled.Should().BeFalse();
        config.TimeOfDay.Should().Be("02:00");
    }

    [Fact]
    public async Task Save_persists_days_and_time()
    {
        using var db = new TestDatabase();

        await using (var ctx = db.CreateContext())
        {
            var svc = new ScheduleService(ctx);
            var config = await svc.GetAsync();
            config.IsEnabled = true;
            config.TimeOfDay = "07:30";
            config.DaysOfWeek = "1,3,5";
            await svc.SaveAsync(config);
        }

        await using (var ctx = db.CreateContext())
        {
            var reloaded = await new ScheduleService(ctx).GetAsync();
            reloaded.IsEnabled.Should().BeTrue();
            reloaded.TimeOfDay.Should().Be("07:30");
            reloaded.GetDays().Should().BeEquivalentTo(new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday });
        }
    }

    [Fact]
    public async Task IsRunDue_false_when_disabled()
    {
        using var db = new TestDatabase();
        await using var ctx = db.CreateContext();
        var svc = new ScheduleService(ctx);
        var config = await svc.GetAsync();
        config.IsEnabled = false;
        await svc.SaveAsync(config);

        (await svc.IsRunDueAsync(DateTime.Now)).Should().BeFalse();
    }

    [Fact]
    public async Task IsRunDue_true_once_per_slot_then_false_after_mark()
    {
        using var db = new TestDatabase();
        await using var ctx = db.CreateContext();
        var svc = new ScheduleService(ctx);

        var now = new DateTime(2026, 9, 7, 9, 0, 0, DateTimeKind.Local); // a Monday
        var config = await svc.GetAsync();
        config.IsEnabled = true;
        config.TimeOfDay = "08:00";
        config.DaysOfWeek = ((int)now.DayOfWeek).ToString();
        await svc.SaveAsync(config);

        (await svc.IsRunDueAsync(now)).Should().BeTrue();

        await svc.MarkRunAsync(now.ToUniversalTime());

        (await svc.IsRunDueAsync(now.AddMinutes(5))).Should().BeFalse();
    }

    [Fact]
    public async Task IsRunDue_false_before_scheduled_time_and_on_wrong_day()
    {
        using var db = new TestDatabase();
        await using var ctx = db.CreateContext();
        var svc = new ScheduleService(ctx);

        var monday9Am = new DateTime(2026, 9, 7, 9, 0, 0, DateTimeKind.Local);
        var config = await svc.GetAsync();
        config.IsEnabled = true;
        config.TimeOfDay = "10:00";
        config.DaysOfWeek = "2"; // Tuesday only
        await svc.SaveAsync(config);

        (await svc.IsRunDueAsync(monday9Am)).Should().BeFalse();           // wrong day
        config.DaysOfWeek = ((int)monday9Am.DayOfWeek).ToString();
        await svc.SaveAsync(config);
        (await svc.IsRunDueAsync(monday9Am)).Should().BeFalse();           // before 10:00
    }
}
