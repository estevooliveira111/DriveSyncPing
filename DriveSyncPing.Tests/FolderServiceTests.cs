using System.Threading.Tasks;
using DriveSyncPing.Domain.Enums;
using DriveSyncPing.Infrastructure.Services;
using DriveSyncPing.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace DriveSyncPing.Tests;

public class FolderServiceTests
{
    [Fact]
    public async Task Adds_validates_and_removes_folders()
    {
        using var db = new TestDatabase();
        using var ws = new TempWorkspace();

        await using var ctx = db.CreateContext();
        var service = new FolderService(ctx);

        (await service.ValidatePathAsync(ws.Root)).Should().BeTrue();
        (await service.ValidatePathAsync("/definitely/not/here")).Should().BeFalse();

        var added = await service.AddFolderAsync(ws.Root);
        added.Should().NotBeNull();

        // adding the same path again returns the existing folder, no duplicate
        var again = await service.AddFolderAsync(ws.Root);
        again!.Id.Should().Be(added!.Id);
        (await service.GetFoldersAsync()).Should().HaveCount(1);

        await service.RemoveFolderAsync(added.Id);
        (await service.GetFoldersAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Rejects_invalid_path()
    {
        using var db = new TestDatabase();
        await using var ctx = db.CreateContext();
        var service = new FolderService(ctx);

        (await service.AddFolderAsync("   ")).Should().BeNull();
    }

    [Fact]
    public async Task Updates_organization_rule()
    {
        using var db = new TestDatabase();
        using var ws = new TempWorkspace();

        await using var ctx = db.CreateContext();
        var service = new FolderService(ctx);
        var folder = await service.AddFolderAsync(ws.Root);

        await service.UpdateFolderRuleAsync(folder!.Id, OrganizationRule.ByDate);

        await using var verify = db.CreateContext();
        verify.SyncFolders.Find(folder.Id)!.OrganizationRule.Should().Be(OrganizationRule.ByDate);
    }
}
