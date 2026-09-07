using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DriveSyncPing.Application.Services;
using DriveSyncPing.Domain.Entities;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace DriveSyncPing.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IFolderService? _folderService;
    private readonly IGoogleAuthService? _authService;

    [ObservableProperty]
    private ObservableCollection<SyncFolder> _folders = new();

    [ObservableProperty]
    private string _googleDriveStatus = "Não conectado";

    [ObservableProperty]
    private bool _isConnected = false;

    public MainViewModel(IFolderService folderService, IGoogleAuthService authService)
    {
        _folderService = folderService;
        _authService = authService;
        
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
}
