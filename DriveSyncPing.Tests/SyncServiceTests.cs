using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DriveSyncPing.Application.Services;
using DriveSyncPing.Domain.Entities;
using DriveSyncPing.Domain.Enums;
using DriveSyncPing.Infrastructure.Services;
using DriveSyncPing.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace DriveSyncPing.Tests;

public class SyncServiceTests
{
    private static void NoProgress(string _, int __) { }

    /// <summary>Resolves the "&lt;driveName&gt;/&lt;localFolderName&gt;" folder id in the fake Drive.</summary>
    private static async Task<string> ResolveRemoteFolderAsync(
        FakeGoogleDriveService drive, string localRoot, string driveName = AppSettings.DefaultDriveFolderName)
    {
        var rootId = await drive.CreateFolderAsync(driveName);
        return (await drive.CreateFolderAsync(new DirectoryInfo(localRoot).Name, rootId))!;
    }

    private static async Task<SyncFolder> AddFolderAsync(TestDatabase db, string path, OrganizationRule rule = OrganizationRule.None)
    {
        await using var ctx = db.CreateContext();
        var folder = new SyncFolder { Path = path, IsEnabled = true, OrganizationRule = rule };
        ctx.SyncFolders.Add(folder);
        await ctx.SaveChangesAsync();
        return folder;
    }

    [Fact]
    public async Task Uploads_pending_files_and_marks_them_validated()
    {
        using var db = new TestDatabase();
        using var ws = new TempWorkspace();
        ws.WriteFile("a.txt", "alpha");
        ws.WriteFile("sub/b.txt", "bravo");
        await AddFolderAsync(db, ws.Root);

        var drive = new FakeGoogleDriveService();
        await using var ctx = db.CreateContext();
        var service = new SyncService(ctx, drive);

        var job = await service.RunSyncAsync(new SyncRunOptions(), NoProgress);

        job.Status.Should().Be(JobStatus.Completed);
        job.FilesUploaded.Should().Be(2);
        drive.UploadCount.Should().Be(2);

        await using var verify = db.CreateContext();
        verify.SyncFiles.Should().OnlyContain(f => f.Status == SyncStatus.Validated);
        verify.SyncFiles.Should().OnlyContain(f => f.RemoteFileId != null && f.LastSyncedAt != null);
        verify.SyncOperations.Select(o => o.Type).Should().Contain(new[]
        {
            OperationType.SyncStarted, OperationType.Upload, OperationType.ValidationPassed, OperationType.SyncCompleted
        });
    }

    [Fact]
    public async Task Skips_duplicate_when_remote_hash_matches()
    {
        using var db = new TestDatabase();
        using var ws = new TempWorkspace();
        ws.WriteFile("dup.txt", "same-content");
        await AddFolderAsync(db, ws.Root);

        var drive = new FakeGoogleDriveService();
        var targetId = await ResolveRemoteFolderAsync(drive, ws.Root);
        drive.SeedFile(targetId, "dup.txt", 12, TempWorkspace.Sha256("same-content"));

        await using var ctx = db.CreateContext();
        var job = await new SyncService(ctx, drive).RunSyncAsync(new SyncRunOptions(), NoProgress);

        job.FilesIgnored.Should().Be(1);
        job.FilesUploaded.Should().Be(0);
        drive.UploadCount.Should().Be(0);

        await using var verify = db.CreateContext();
        verify.SyncFiles.Single().Status.Should().Be(SyncStatus.Ignored);
        verify.SyncOperations.Should().Contain(o => o.Type == OperationType.Ignore);
    }

    [Fact]
    public async Task Size_only_duplicate_is_ignored_but_never_deleted()
    {
        using var db = new TestDatabase();
        using var ws = new TempWorkspace();
        var local = ws.WriteFile("x.bin", "abcde");
        await AddFolderAsync(db, ws.Root);

        var drive = new FakeGoogleDriveService();
        var targetId = await ResolveRemoteFolderAsync(drive, ws.Root);
        drive.SeedFile(targetId, "x.bin", 5, sha256: null); // same size, unknown hash

        await using var ctx = db.CreateContext();
        var job = await new SyncService(ctx, drive)
            .RunSyncAsync(new SyncRunOptions { DeleteAfterUpload = true }, NoProgress);

        job.FilesIgnored.Should().Be(1);
        job.FilesDeleted.Should().Be(0);
        File.Exists(local).Should().BeTrue();
    }

    [Fact]
    public async Task Deletes_local_file_only_after_successful_validation()
    {
        using var db = new TestDatabase();
        using var ws = new TempWorkspace();
        var local = ws.WriteFile("gone.txt", "remove me");
        await AddFolderAsync(db, ws.Root);

        var drive = new FakeGoogleDriveService();
        await using var ctx = db.CreateContext();
        var job = await new SyncService(ctx, drive)
            .RunSyncAsync(new SyncRunOptions { DeleteAfterUpload = true }, NoProgress);

        job.FilesDeleted.Should().Be(1);
        File.Exists(local).Should().BeFalse();

        await using var verify = db.CreateContext();
        verify.SyncFiles.Single().Status.Should().Be(SyncStatus.Deleted);
        verify.SyncOperations.Should().Contain(o => o.Type == OperationType.DeleteLocal);
    }

    [Fact]
    public async Task Keeps_local_file_when_validation_fails()
    {
        using var db = new TestDatabase();
        using var ws = new TempWorkspace();
        var local = ws.WriteFile("keep.txt", "important");
        await AddFolderAsync(db, ws.Root);

        var drive = new FakeGoogleDriveService { ForceReportedSize = 999_999 };
        await using var ctx = db.CreateContext();
        var job = await new SyncService(ctx, drive)
            .RunSyncAsync(new SyncRunOptions { DeleteAfterUpload = true }, NoProgress);

        job.FilesFailed.Should().Be(1);
        job.FilesDeleted.Should().Be(0);
        File.Exists(local).Should().BeTrue();

        await using var verify = db.CreateContext();
        verify.SyncFiles.Single().Status.Should().Be(SyncStatus.Failed);
        verify.SyncOperations.Should().Contain(o => o.Type == OperationType.ValidationFailed);
    }

    [Fact]
    public async Task Dry_run_changes_nothing_but_reports_planned_actions()
    {
        using var db = new TestDatabase();
        using var ws = new TempWorkspace();
        var local = ws.WriteFile("plan.txt", "content");
        await AddFolderAsync(db, ws.Root);

        var drive = new FakeGoogleDriveService();
        await using var ctx = db.CreateContext();
        var job = await new SyncService(ctx, drive)
            .RunSyncAsync(new SyncRunOptions { DryRun = true, DeleteAfterUpload = true }, NoProgress);

        job.IsDryRun.Should().BeTrue();
        job.FilesUploaded.Should().Be(1);   // "would upload"
        job.FilesDeleted.Should().Be(1);    // "would delete"
        drive.UploadCount.Should().Be(0);
        File.Exists(local).Should().BeTrue();

        await using var verify = db.CreateContext();
        verify.SyncFiles.Single().Status.Should().Be(SyncStatus.Pending);
        verify.SyncOperations.Select(o => o.Type)
            .Should().Contain(new[] { OperationType.DryRunUpload, OperationType.DryRunDelete });
    }

    [Fact]
    public async Task Resume_retries_failed_files_and_skips_already_synced_ones()
    {
        using var db = new TestDatabase();
        using var ws = new TempWorkspace();
        ws.WriteFile("ok.txt", "fine");
        ws.WriteFile("broken.txt", "boom");
        await AddFolderAsync(db, ws.Root);

        var drive = new FakeGoogleDriveService();
        drive.FailUploadForNames.Add("broken.txt");

        await using (var ctx = db.CreateContext())
        {
            var first = await new SyncService(ctx, drive).RunSyncAsync(new SyncRunOptions(), NoProgress);
            first.FilesUploaded.Should().Be(1);
            first.FilesFailed.Should().Be(1);
        }

        drive.FailUploadForNames.Clear();

        await using (var ctx = db.CreateContext())
        {
            var second = await new SyncService(ctx, drive).RunSyncAsync(new SyncRunOptions(), NoProgress);
            second.FilesUploaded.Should().Be(1); // only the previously failed file
        }

        drive.UploadCount.Should().Be(2); // ok.txt once, broken.txt once (retry)

        await using var verify = db.CreateContext();
        verify.SyncFiles.Should().OnlyContain(f => f.Status == SyncStatus.Validated);
    }

    [Fact]
    public async Task Cancelled_token_marks_job_cancelled()
    {
        using var db = new TestDatabase();
        using var ws = new TempWorkspace();
        ws.WriteFile("a.txt", "alpha");
        await AddFolderAsync(db, ws.Root);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var drive = new FakeGoogleDriveService();
        await using var ctx = db.CreateContext();
        var job = await new SyncService(ctx, drive).RunSyncAsync(new SyncRunOptions(), NoProgress, cts.Token);

        job.Status.Should().Be(JobStatus.Cancelled);
        drive.UploadCount.Should().Be(0);

        await using var verify = db.CreateContext();
        verify.SyncOperations.Should().Contain(o => o.Type == OperationType.Cancelled);
    }

    [Fact]
    public async Task Interrupted_running_job_is_marked_failed_on_next_run()
    {
        using var db = new TestDatabase();
        using var ws = new TempWorkspace();
        await AddFolderAsync(db, ws.Root);

        await using (var seed = db.CreateContext())
        {
            seed.SyncJobs.Add(new SyncJob { Status = JobStatus.Running });
            await seed.SaveChangesAsync();
        }

        var drive = new FakeGoogleDriveService();
        await using var ctx = db.CreateContext();
        await new SyncService(ctx, drive).RunSyncAsync(new SyncRunOptions(), NoProgress);

        await using var verify = db.CreateContext();
        var stale = verify.SyncJobs.OrderBy(j => j.Id).First();
        stale.Status.Should().Be(JobStatus.Failed);
        stale.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task Organizes_uploads_into_subfolders_by_rule()
    {
        using var db = new TestDatabase();
        using var ws = new TempWorkspace();
        ws.WriteFile("photo.jpg", "img");
        ws.WriteFile("notes.txt", "txt");
        await AddFolderAsync(db, ws.Root, OrganizationRule.ByType);

        var drive = new FakeGoogleDriveService();
        await using var ctx = db.CreateContext();
        var job = await new SyncService(ctx, drive).RunSyncAsync(new SyncRunOptions(), NoProgress);

        job.FilesUploaded.Should().Be(2);
        drive.FolderExists(AppSettings.DefaultDriveFolderName).Should().BeTrue();
        var rootName = new DirectoryInfo(ws.Root).Name;
        var driveRootId = await drive.CreateFolderAsync(AppSettings.DefaultDriveFolderName);
        drive.FolderExists(rootName, driveRootId).Should().BeTrue();
        var folderId = (await drive.CreateFolderAsync(rootName, driveRootId))!;
        drive.FolderExists("Imagens", folderId).Should().BeTrue();
        drive.FolderExists("Documentos", folderId).Should().BeTrue();
        drive.FileExists("photo.jpg", (await drive.CreateFolderAsync("Imagens", folderId))!).Should().BeTrue();
    }

    [Fact]
    public async Task Uses_custom_drive_folder_name_from_options()
    {
        using var db = new TestDatabase();
        using var ws = new TempWorkspace();
        ws.WriteFile("a.txt", "alpha");
        await AddFolderAsync(db, ws.Root);

        var drive = new FakeGoogleDriveService();
        await using var ctx = db.CreateContext();
        var job = await new SyncService(ctx, drive)
            .RunSyncAsync(new SyncRunOptions { DriveFolderName = "Meus Backups" }, NoProgress);

        job.FilesUploaded.Should().Be(1);
        drive.FolderExists("Meus Backups").Should().BeTrue();
        drive.FolderExists(AppSettings.DefaultDriveFolderName).Should().BeFalse();

        var customRootId = await drive.CreateFolderAsync("Meus Backups");
        drive.FolderExists(new DirectoryInfo(ws.Root).Name, customRootId).Should().BeTrue();
    }

    [Fact]
    public async Task Blank_drive_folder_name_falls_back_to_default()
    {
        using var db = new TestDatabase();
        using var ws = new TempWorkspace();
        ws.WriteFile("a.txt", "alpha");
        await AddFolderAsync(db, ws.Root);

        var drive = new FakeGoogleDriveService();
        await using var ctx = db.CreateContext();
        await new SyncService(ctx, drive)
            .RunSyncAsync(new SyncRunOptions { DriveFolderName = "   " }, NoProgress);

        drive.FolderExists(AppSettings.DefaultDriveFolderName).Should().BeTrue();
    }
}
