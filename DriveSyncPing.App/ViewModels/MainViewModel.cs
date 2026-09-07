using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DriveSyncPing.Application.Services;
using DriveSyncPing.Domain.Entities;
using DriveSyncPing.Domain.Enums;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace DriveSyncPing.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IFolderService? _folderService;
    private readonly IGoogleAuthService? _authService;
    private readonly ISyncService? _syncService;

    [ObservableProperty]
    private ObservableCollection<SyncFolder> _folders = new();

    public List<OrganizationRule> AvailableRules { get; } = Enum.GetValues(typeof(OrganizationRule)).Cast<OrganizationRule>().ToList();

    [ObservableProperty]
    private string _googleDriveStatus = "Não conectado";

    [ObservableProperty]
    private bool _isConnected = false;

    [ObservableProperty]
    private string _syncStatusText = "Pronto";

    [ObservableProperty]
    private int _syncProgress = 0;

    [ObservableProperty]
    private bool _isSyncing = false;

    public MainViewModel(IFolderService folderService, IGoogleAuthService authService, ISyncService syncService)
    {
        _folderService = folderService;
        _authService = authService;
        _syncService = syncService;
        
        _ = LoadFoldersAsync();
        _ = CheckAuthStatusAsync();
    }

    public MainViewModel()
    {
    }

    private async Task CheckAuthStatusAsync()
    {
        if (_authService == null) return;
        IsConnected = await _authService.IsConnectedAsync();
        GoogleDriveStatus = IsConnected ? "Conectado ao Google Drive" : "Não conectado";
    }

    [RelayCommand]
    private async Task LoginGoogleAsync()
    {
        if (_authService == null) return;
        
        GoogleDriveStatus = "Conectando...";
        var result = await _authService.LoginAsync();
        
        if (result != null && result.StartsWith("Erro"))
        {
            GoogleDriveStatus = result;
            IsConnected = false;
        }
        else
        {
            IsConnected = true;
            GoogleDriveStatus = "Conectado ao Google Drive";
        }
    }

    [RelayCommand]
    private async Task LogoutGoogleAsync()
    {
        if (_authService == null) return;
        await _authService.LogoutAsync();
        IsConnected = false;
        GoogleDriveStatus = "Não conectado";
    }

    private async Task LoadFoldersAsync()
    {
        if (_folderService == null) return;
        var folders = await _folderService.GetFoldersAsync();
        Folders = new ObservableCollection<SyncFolder>(folders);
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var result = await desktop.MainWindow.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Selecione uma pasta para sincronizar",
                AllowMultiple = false
            });

            if (result != null && result.Count > 0)
            {
                var folderPath = result[0].Path.LocalPath;
                var addedFolder = await _folderService?.AddFolderAsync(folderPath)!;
                
                if (addedFolder != null)
                {
                    await LoadFoldersAsync();
                }
            }
        }
    }

    [RelayCommand]
    private async Task RemoveFolderAsync(SyncFolder folder)
    {
        if (folder != null && _folderService != null)
        {
            await _folderService.RemoveFolderAsync(folder.Id);
            Folders.Remove(folder);
        }
    }

    // Called when the ComboBox selection changes
    public void OnRuleChanged(SyncFolder folder, OrganizationRule rule)
    {
        if (folder != null && _folderService != null)
        {
            _ = _folderService.UpdateFolderRuleAsync(folder.Id, rule);
        }
    }

    [RelayCommand]
    private async Task RunSyncAsync()
    {
        if (_syncService == null) return;
        
        IsSyncing = true;
        SyncProgress = 0;
        
        // Save current rules to DB
        if (_folderService != null)
        {
            foreach (var folder in Folders)
            {
                await _folderService.UpdateFolderRuleAsync(folder.Id, folder.OrganizationRule);
            }
        }
        
        await Task.Run(async () =>
        {
            await _syncService.RunSyncAsync((status, progress) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SyncStatusText = status;
                    SyncProgress = progress;
                });
            });
        });
        
        IsSyncing = false;
    }
}
