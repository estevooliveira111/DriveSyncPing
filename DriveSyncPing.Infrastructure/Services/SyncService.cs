using DriveSyncPing.Application.Services;
using DriveSyncPing.Domain.Entities;
using DriveSyncPing.Domain.Enums;
using DriveSyncPing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace DriveSyncPing.Infrastructure.Services;

public class SyncService : ISyncService
{
    private readonly AppDbContext _context;
    private readonly IGoogleDriveService _driveService;

    public SyncService(AppDbContext context, IGoogleDriveService driveService)
    {
        _context = context;
        _driveService = driveService;
    }

    public async Task<SyncJob> RunSyncAsync(
        SyncRunOptions options,
        Action<string, int> onProgressUpdate,
        CancellationToken cancellationToken = default)
    {
        onProgressUpdate(options.DryRun ? "Iniciando simulação (dry-run)..." : "Iniciando sincronização...", 0);

        // Cleanup runs regardless of cancellation so a stale "Running" job never lingers.
        await MarkInterruptedJobsAsync(CancellationToken.None);

        var job = new SyncJob
        {
            StartDate = DateTime.UtcNow,
            Status = JobStatus.Running,
            IsDryRun = options.DryRun,
            Trigger = options.Trigger
        };
        _context.SyncJobs.Add(job);
        await _context.SaveChangesAsync(CancellationToken.None);

        Log(job, OperationType.SyncStarted, null,
            options.DryRun ? "Simulação iniciada" : "Sincronização iniciada");
        await _context.SaveChangesAsync(CancellationToken.None);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var folders = await _context.SyncFolders.Where(f => f.IsEnabled).ToListAsync(cancellationToken);
            if (folders.Count == 0)
            {
                onProgressUpdate("Nenhuma pasta configurada.", 100);
                await FinalizeAsync(job, JobStatus.Completed, null, cancellationToken);
                return job;
            }

            for (int fi = 0; fi < folders.Count; fi++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ProcessFolderAsync(job, folders[fi], options, fi, folders.Count, onProgressUpdate, cancellationToken);
            }

            await FinalizeAsync(job, JobStatus.Completed, null, cancellationToken);
            onProgressUpdate(
                options.DryRun
                    ? $"Simulação concluída: {job.FilesUploaded} para enviar, {job.FilesIgnored} duplicados, {job.FilesDeleted} para excluir."
                    : $"Sincronização concluída: {job.FilesUploaded} enviados, {job.FilesIgnored} duplicados, {job.FilesFailed} falhas, {job.FilesDeleted} excluídos.",
                100);
        }
        catch (OperationCanceledException)
        {
            Log(job, OperationType.Cancelled, null, "Sincronização cancelada pelo usuário");
            await FinalizeAsync(job, JobStatus.Cancelled, "Cancelado pelo usuário", CancellationToken.None);
            onProgressUpdate("Sincronização cancelada.", 100);
        }
        catch (Exception ex)
        {
            Log(job, OperationType.Error, null, ex.Message);
            await FinalizeAsync(job, JobStatus.Failed, ex.Message, CancellationToken.None);
            onProgressUpdate($"Erro: {ex.Message}", 100);
        }

        return job;
    }

    private async Task ProcessFolderAsync(
        SyncJob job,
        SyncFolder folder,
        SyncRunOptions options,
        int folderIndex,
        int folderCount,
        Action<string, int> onProgressUpdate,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folder.Path))
        {
            Log(job, OperationType.Error, null, $"Pasta não encontrada: {folder.Path}");
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        int BaseProgress(int fileIndex, int fileCount) =>
            (folderIndex * 100 / folderCount) + (fileCount == 0 ? 0 : fileIndex * 100 / fileCount / folderCount);

        var folderName = new DirectoryInfo(folder.Path).Name;
        var rootFolderName = string.IsNullOrWhiteSpace(options.DriveFolderName)
            ? AppSettings.DefaultDriveFolderName
            : options.DriveFolderName.Trim();
        string? remoteFolderId = null;

        if (!options.DryRun)
        {
            onProgressUpdate($"Preparando pasta '{rootFolderName}/{folderName}' no Drive...", BaseProgress(0, 1));

            var rootId = await _driveService.CreateFolderAsync(rootFolderName, null, cancellationToken);
            if (rootId == null)
            {
                Log(job, OperationType.Error, null, $"Não foi possível criar a pasta '{rootFolderName}' no Drive.");
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            remoteFolderId = await _driveService.CreateFolderAsync(folderName, rootId, cancellationToken);
            if (remoteFolderId == null)
            {
                Log(job, OperationType.Error, null, $"Não foi possível criar a pasta '{rootFolderName}/{folderName}' no Drive.");
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }
        }

        var files = Directory.GetFiles(folder.Path, "*.*", SearchOption.AllDirectories);
        var subfolderCache = new Dictionary<string, string>(StringComparer.Ordinal);

        for (int i = 0; i < files.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = files[i];
            var fileName = Path.GetFileName(filePath);
            var relativePath = Path.GetRelativePath(folder.Path, filePath);
            int progress = BaseProgress(i, files.Length);

            onProgressUpdate($"Analisando {fileName}...", progress);

            var fileInfo = new FileInfo(filePath);
            var hash = CalculateHash(filePath);

            var syncFile = await _context.SyncFiles
                .FirstOrDefaultAsync(f => f.RelativePath == relativePath && f.SyncFolderId == folder.Id, cancellationToken);

            bool alreadyProcessed = syncFile != null
                && syncFile.HashSha256 == hash
                && syncFile.Status is SyncStatus.Validated or SyncStatus.Synced or SyncStatus.Deleted;

            if (alreadyProcessed)
                continue; // resume: skip files already uploaded in a previous run

            if (syncFile == null)
            {
                syncFile = new SyncFile { SyncFolderId = folder.Id, RelativePath = relativePath };
                _context.SyncFiles.Add(syncFile);
            }

            syncFile.HashSha256 = hash;
            syncFile.SizeBytes = fileInfo.Length;
            syncFile.LastModified = fileInfo.LastWriteTimeUtc;
            syncFile.Status = SyncStatus.Pending;
            await _context.SaveChangesAsync(cancellationToken);

            if (options.DryRun)
            {
                Log(job, OperationType.DryRunUpload, syncFile.Id, $"Enviaria: {relativePath}");
                job.FilesUploaded++;
                if (options.DeleteAfterUpload)
                {
                    Log(job, OperationType.DryRunDelete, syncFile.Id, $"Excluiria após validação: {relativePath}");
                    job.FilesDeleted++;
                }
                await _context.SaveChangesAsync(cancellationToken);
                continue;
            }

            var targetFolderId = await ResolveTargetFolderAsync(
                remoteFolderId!, fileInfo, folder.OrganizationRule, subfolderCache, cancellationToken);

            // --- Duplicate detection (section 7) ---
            var remoteExisting = await _driveService.FindFileAsync(fileName, targetFolderId, cancellationToken);
            if (remoteExisting != null && IsSameContent(remoteExisting, hash, fileInfo.Length))
            {
                syncFile.RemoteFileId = remoteExisting.Id;
                syncFile.Status = SyncStatus.Ignored;
                Log(job, OperationType.Ignore, syncFile.Id, $"Duplicado ignorado: {relativePath}");
                job.FilesIgnored++;

                if (options.DeleteAfterUpload && remoteExisting.Sha256 == hash)
                    TryDeleteLocal(job, syncFile, filePath, relativePath);

                await _context.SaveChangesAsync(cancellationToken);
                continue;
            }

            // --- Upload (section 5) ---
            onProgressUpdate($"Enviando {fileName}...", progress);
            syncFile.Status = SyncStatus.Uploading;
            await _context.SaveChangesAsync(cancellationToken);

            RemoteFile? uploaded;
            try
            {
                uploaded = await _driveService.UploadFileAsync(filePath, targetFolderId, hash, null, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                syncFile.Status = SyncStatus.Failed;
                Log(job, OperationType.Error, syncFile.Id, $"Falha ao enviar {relativePath}: {ex.Message}");
                job.FilesFailed++;
                await _context.SaveChangesAsync(cancellationToken);
                continue;
            }

            syncFile.RemoteFileId = uploaded?.Id;
            syncFile.Status = SyncStatus.Synced;

            // --- Validation (section 8) ---
            if (!await ValidateUploadAsync(job, syncFile, uploaded, hash, fileInfo.Length, relativePath, cancellationToken))
            {
                job.FilesFailed++;
                await _context.SaveChangesAsync(cancellationToken);
                continue;
            }

            syncFile.Status = SyncStatus.Validated;
            syncFile.LastSyncedAt = DateTime.UtcNow;
            job.FilesUploaded++;
            Log(job, OperationType.Upload, syncFile.Id, $"Enviado: {relativePath}");

            // --- Safe deletion (section 9) ---
            if (options.DeleteAfterUpload)
                TryDeleteLocal(job, syncFile, filePath, relativePath);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<bool> ValidateUploadAsync(
        SyncJob job, SyncFile syncFile, RemoteFile? uploaded, string hash, long localSize, string relativePath,
        CancellationToken cancellationToken)
    {
        RemoteFile? remote = uploaded;
        if (remote?.Id is { Length: > 0 } id)
            remote = await _driveService.GetFileAsync(id, cancellationToken) ?? uploaded;

        if (remote == null)
        {
            syncFile.Status = SyncStatus.Failed;
            Log(job, OperationType.ValidationFailed, syncFile.Id, $"Validação falhou (arquivo remoto não encontrado): {relativePath}");
            return false;
        }

        if (remote.SizeBytes is { } size && size != localSize)
        {
            syncFile.Status = SyncStatus.Failed;
            Log(job, OperationType.ValidationFailed, syncFile.Id,
                $"Validação falhou (tamanho {size} != {localSize}): {relativePath}");
            return false;
        }

        if (!string.IsNullOrEmpty(remote.Sha256) && remote.Sha256 != hash)
        {
            syncFile.Status = SyncStatus.Failed;
            Log(job, OperationType.ValidationFailed, syncFile.Id, $"Validação falhou (hash divergente): {relativePath}");
            return false;
        }

        Log(job, OperationType.ValidationPassed, syncFile.Id, $"Upload validado: {relativePath}");
        return true;
    }

    private void TryDeleteLocal(SyncJob job, SyncFile syncFile, string filePath, string relativePath)
    {
        try
        {
            File.Delete(filePath);
            syncFile.Status = SyncStatus.Deleted;
            job.FilesDeleted++;
            Log(job, OperationType.DeleteLocal, syncFile.Id, $"Arquivo local excluído: {relativePath}");
        }
        catch (Exception ex)
        {
            Log(job, OperationType.Error, syncFile.Id, $"Não foi possível excluir {relativePath}: {ex.Message}");
        }
    }

    private async Task<string> ResolveTargetFolderAsync(
        string rootFolderId, FileInfo fileInfo, OrganizationRule rule,
        Dictionary<string, string> cache, CancellationToken cancellationToken)
    {
        var subfolderName = GetSubfolderName(fileInfo, rule);
        if (string.IsNullOrEmpty(subfolderName))
            return rootFolderId;

        if (cache.TryGetValue(subfolderName, out var cached))
            return cached;

        var subfolderId = await _driveService.CreateFolderAsync(subfolderName, rootFolderId, cancellationToken);
        var resolved = subfolderId ?? rootFolderId;
        cache[subfolderName] = resolved;
        return resolved;
    }

    private async Task MarkInterruptedJobsAsync(CancellationToken cancellationToken)
    {
        var stale = await _context.SyncJobs.Where(j => j.Status == JobStatus.Running).ToListAsync(cancellationToken);
        foreach (var j in stale)
        {
            j.Status = JobStatus.Failed;
            j.EndDate = DateTime.UtcNow;
            j.ErrorMessage ??= "Processo interrompido; retomando na próxima execução.";
        }
        if (stale.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task FinalizeAsync(SyncJob job, JobStatus status, string? error, CancellationToken cancellationToken)
    {
        job.Status = status;
        job.EndDate = DateTime.UtcNow;
        job.ErrorMessage = error;
        if (status == JobStatus.Completed)
            Log(job, OperationType.SyncCompleted, null, "Sincronização concluída");
        await _context.SaveChangesAsync(cancellationToken);
    }

    private void Log(SyncJob job, OperationType type, int? syncFileId, string? message) =>
        _context.SyncOperations.Add(new SyncOperation
        {
            SyncJobId = job.Id,
            SyncFileId = syncFileId,
            Type = type,
            Message = message,
            Timestamp = DateTime.UtcNow
        });

    private static bool IsSameContent(RemoteFile remote, string hash, long size)
    {
        if (!string.IsNullOrEmpty(remote.Sha256))
            return remote.Sha256 == hash;
        return remote.SizeBytes is { } s && s == size;
    }

    private static string CalculateHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexStringLower(hash);
    }

    private static string GetCategoryForExtension(string extension)
    {
        extension = extension.ToLowerInvariant().TrimStart('.');

        string[] images = { "jpg", "jpeg", "png", "gif", "bmp", "webp", "tiff" };
        string[] documents = { "pdf", "doc", "docx", "txt", "xls", "xlsx", "ppt", "pptx", "csv" };
        string[] videos = { "mp4", "mkv", "avi", "mov", "wmv" };
        string[] audios = { "mp3", "wav", "flac", "aac", "ogg" };

        if (images.Contains(extension)) return "Imagens";
        if (documents.Contains(extension)) return "Documentos";
        if (videos.Contains(extension)) return "Vídeos";
        if (audios.Contains(extension)) return "Áudios";
        return "Outros";
    }

    private static string GetSubfolderName(FileInfo fileInfo, OrganizationRule rule) => rule switch
    {
        OrganizationRule.ByExtension => string.IsNullOrEmpty(fileInfo.Extension)
            ? "SemExtensao"
            : fileInfo.Extension.TrimStart('.').ToUpperInvariant(),
        OrganizationRule.ByType => GetCategoryForExtension(fileInfo.Extension),
        OrganizationRule.ByDate => fileInfo.LastWriteTimeUtc.ToString("yyyy-MM"),
        _ => string.Empty
    };
}
