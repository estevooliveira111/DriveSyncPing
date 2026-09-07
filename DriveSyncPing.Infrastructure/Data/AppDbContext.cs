using DriveSyncPing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DriveSyncPing.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public DbSet<AppConfiguration> Configurations => Set<AppConfiguration>();
    public DbSet<SyncFolder> SyncFolders => Set<SyncFolder>();
    public DbSet<SyncFile> SyncFiles => Set<SyncFile>();
    public DbSet<SyncJob> SyncJobs => Set<SyncJob>();
    public DbSet<SyncOperation> SyncOperations => Set<SyncOperation>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppConfiguration>(entity =>
        {
            entity.HasKey(e => e.Key);
        });

        modelBuilder.Entity<SyncFolder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasMany(e => e.Files)
                  .WithOne(e => e.SyncFolder)
                  .HasForeignKey(e => e.SyncFolderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SyncFile>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<SyncJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasMany(e => e.Operations)
                  .WithOne(e => e.SyncJob)
                  .HasForeignKey(e => e.SyncJobId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SyncOperation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.SyncFile)
                  .WithMany()
                  .HasForeignKey(e => e.SyncFileId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
