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
    private readonly IFolderService _folderService;

    [ObservableProperty]
    private ObservableCollection<SyncFolder> _folders = new();

    public MainViewModel(IFolderService folderService)
    {
        _folderService = folderService;
        _ = LoadFoldersAsync();
    }

    // Designer constructor
    public MainViewModel()
    {
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
                var addedFolder = await _folderService.AddFolderAsync(folderPath);
                
                if (addedFolder != null)
                {
                    // Refresh
                    await LoadFoldersAsync();
                }
            }
        }
    }

    [RelayCommand]
    private async Task RemoveFolderAsync(SyncFolder folder)
    {
        if (folder != null)
        {
            await _folderService.RemoveFolderAsync(folder.Id);
            Folders.Remove(folder);
        }
    }
}
