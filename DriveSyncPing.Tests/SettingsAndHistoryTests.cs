using System.Threading.Tasks;
using DriveSyncPing.Application.Services;
using DriveSyncPing.Domain.Entities;
using DriveSyncPing.Domain.Enums;
using DriveSyncPing.Infrastructure.Services;
using DriveSyncPing.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace DriveSyncPing.Tests;

public class SettingsServiceTests
{
    [Fact]
    public async Task Defaults_are_false_when_nothing_stored()
    {
        using var db = new TestDatabase();
        await using var ctx = db.CreateContext();

        var settings = await new SettingsService(ctx).GetAsync();

        settings.DeleteAfterUpload.Should().BeFalse();
        settings.DryRunByDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Save_then_get_roundtrips_and_updates_existing_rows()
    {
        using var db = new TestDatabase();

        await using (var ctx = db.CreateContext())
            await new SettingsService(ctx).SaveAsync(new AppSettings { DeleteAfterUpload = true, DryRunByDefault = true });

        await using (var ctx = db.CreateContext())
        {
            var settings = await new SettingsService(ctx).GetAsync();
            settings.DeleteAfterUpload.Should().BeTrue();
            settings.DryRunByDefault.Should().BeTrue();
        }

        await using (var ctx = db.CreateContext())
            await new SettingsService(ctx).SaveAsync(new AppSettings { DeleteAfterUpload = false, DryRunByDefault = true });

        await using (var ctx = db.CreateContext())
        {
            var settings = await new SettingsService(ctx).GetAsync();
            settings.DeleteAfterUpload.Should().BeFalse();
            settings.DryRunByDefault.Should().BeTrue();
            ctx.Configurations.Should().HaveCount(2); // rows updated, not duplicated
        }
    }
}

public class HistoryServiceTests
{
    [Fact]
    public async Task Returns_jobs_newest_first_and_operations_for_a_job()
    {
        using var db = new TestDatabase();

        int jobId;
        await using (var ctx = db.CreateContext())
        {
            var older = new SyncJob { StartDate = new System.DateTime(2026, 1, 1) };
            var newer = new SyncJob { StartDate = new System.DateTime(2026, 6, 1) };
            ctx.SyncJobs.AddRange(older, newer);
            await ctx.SaveChangesAsync();
            jobId = newer.Id;

            ctx.SyncOperations.Add(new SyncOperation { SyncJobId = jobId, Type = OperationType.Upload, Message = "a" });
            ctx.SyncOperations.Add(new SyncOperation { SyncJobId = jobId, Type = OperationType.Error, Message = "b" });
            await ctx.SaveChangesAsync();
        }

        await using var verify = db.CreateContext();
        var history = new HistoryService(verify);

        (await history.GetJobsAsync()).First().Id.Should().Be(jobId);
        (await history.GetOperationsAsync(jobId)).Should().HaveCount(2);
        (await history.GetRecentOperationsAsync()).Should().Contain(o => o.Type == OperationType.Error);
    }
}
