using DriveSyncPing.Application.Services;
using DriveSyncPing.Domain.Entities;
using DriveSyncPing.Domain.Enums;
using DriveSyncPing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

    private string CalculateHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private string GetCategoryForExtension(string extension)
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

    private string GetSubfolderName(FileInfo fileInfo, OrganizationRule rule)
    {
        return rule switch
        {
            OrganizationRule.ByExtension => string.IsNullOrEmpty(fileInfo.Extension) ? "SemExtensao" : fileInfo.Extension.TrimStart('.').ToUpperInvariant(),
            OrganizationRule.ByType => GetCategoryForExtension(fileInfo.Extension),
            OrganizationRule.ByDate => fileInfo.LastWriteTimeUtc.ToString("yyyy-MM"),
            _ => string.Empty
        };
    }

    public async Task RunSyncAsync(Action<string, int> onProgressUpdate)
    {
        onProgressUpdate("Iniciando sincronização...", 0);

        var job = new SyncJob { StartDate = DateTime.UtcNow, Status = JobStatus.Running };
        _context.SyncJobs.Add(job);
        await _context.SaveChangesAsync();

        try
        {
            var folders = await _context.SyncFolders.Where(f => f.IsEnabled).ToListAsync();
            
            if (!folders.Any())
            {
                onProgressUpdate("Nenhuma pasta configurada.", 100);
                job.Status = JobStatus.Completed;
                job.EndDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return;
            }

            int totalFolders = folders.Count;
            int currentFolderIndex = 0;

            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder.Path)) continue;

                var folderName = new DirectoryInfo(folder.Path).Name;
                onProgressUpdate($"Criando pasta raiz '{folderName}' no Drive...", currentFolderIndex * 100 / totalFolders);

                var remoteFolderId = await _driveService.CreateFolderAsync(folderName);
                if (remoteFolderId == null) continue;

                var files = Directory.GetFiles(folder.Path, "*.*", SearchOption.AllDirectories);
                int totalFiles = files.Length;
                int currentFileIndex = 0;

                foreach (var filePath in files)
                {
                    var fileName = Path.GetFileName(filePath);
                    var relativePath = Path.GetRelativePath(folder.Path, filePath);
                    
                    onProgressUpdate($"Calculando hash: {fileName}", (currentFolderIndex * 100 / totalFolders) + (currentFileIndex * 100 / (totalFiles > 0 ? totalFiles : 1) / totalFolders));

                    var hash = CalculateHash(filePath);
                    var fileInfo = new FileInfo(filePath);

                    var syncFile = await _context.SyncFiles.FirstOrDefaultAsync(f => f.RelativePath == relativePath && f.SyncFolderId == folder.Id);
                    
                    if (syncFile == null)
                    {
                        syncFile = new SyncFile
                        {
                            SyncFolderId = folder.Id,
                            RelativePath = relativePath,
                            HashSha256 = hash,
                            SizeBytes = fileInfo.Length,
                            LastModified = fileInfo.LastWriteTimeUtc,
                            Status = SyncStatus.Pending
                        };
                        _context.SyncFiles.Add(syncFile);
                        await _context.SaveChangesAsync();
                    }
                    else if (syncFile.HashSha256 == hash && syncFile.Status == SyncStatus.Synced)
                    {
                        currentFileIndex++;
                        continue;
                    }
                    else
                    {
                        syncFile.HashSha256 = hash;
                        syncFile.SizeBytes = fileInfo.Length;
                        syncFile.LastModified = fileInfo.LastWriteTimeUtc;
                        syncFile.Status = SyncStatus.Pending;
                        await _context.SaveChangesAsync();
                    }

                    onProgressUpdate($"Enviando {fileName}...", (currentFolderIndex * 100 / totalFolders) + (currentFileIndex * 100 / (totalFiles > 0 ? totalFiles : 1) / totalFolders));
                    
                    try
                    {
                        // Determine Target Subfolder
                        string targetFolderId = remoteFolderId;
                        string subfolderName = GetSubfolderName(fileInfo, folder.OrganizationRule);

                        if (!string.IsNullOrEmpty(subfolderName))
                        {
                            var subfolderId = await _driveService.CreateFolderAsync(subfolderName, remoteFolderId);
                            if (subfolderId != null)
                            {
                                targetFolderId = subfolderId;
                            }
                        }

                        var remoteId = await _driveService.UploadFileAsync(filePath, targetFolderId, (sent, total) => 
                        {
                            // Optional progress
                        });

                        syncFile.Status = SyncStatus.Synced;

                        _context.SyncOperations.Add(new SyncOperation
                        {
                            SyncJobId = job.Id,
                            SyncFileId = syncFile.Id,
                            Type = OperationType.Upload,
                            Message = "Success",
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    catch (Exception ex)
                    {
                        syncFile.Status = SyncStatus.Failed;
                        _context.SyncOperations.Add(new SyncOperation
                        {
                            SyncJobId = job.Id,
                            SyncFileId = syncFile.Id,
                            Type = OperationType.Error,
                            Message = ex.Message,
                            Timestamp = DateTime.UtcNow
                        });
                    }

                    await _context.SaveChangesAsync();
                    currentFileIndex++;
                }

                currentFolderIndex++;
            }

            job.Status = JobStatus.Completed;
            job.EndDate = DateTime.UtcNow;
            onProgressUpdate("Sincronização concluída com sucesso!", 100);
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.EndDate = DateTime.UtcNow;
            onProgressUpdate($"Erro: {ex.Message}", 100);
        }
        
        await _context.SaveChangesAsync();
    }
}
