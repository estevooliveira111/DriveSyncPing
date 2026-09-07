using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DriveSyncPing.Application.Services;
using DriveSyncPing.Domain.Entities;
using DriveSyncPing.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DriveSyncPing.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IFolderService? _folderService;
    private readonly IGoogleAuthService? _authService;
    private readonly ISyncService? _syncService;
    private readonly ISettingsService? _settingsService;
    private readonly IHistoryService? _historyService;
    private readonly IScheduleService? _scheduleService;

    private CancellationTokenSource? _syncCts;

    [ObservableProperty]
    private ObservableCollection<SyncFolder> _folders = new();

    public List<OrganizationRule> AvailableRules { get; } =
        Enum.GetValues<OrganizationRule>().ToList();

    [ObservableProperty] private string _googleDriveStatus = "Não conectado";
    [ObservableProperty] private bool _isConnected;

    [ObservableProperty] private string _syncStatusText = "Pronto";
    [ObservableProperty] private int _syncProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartSync))]
    private bool _isSyncing;

    // --- Settings (sections 9, 12) ---
    [ObservableProperty] private bool _deleteAfterUpload;
    [ObservableProperty] private bool _dryRun;
    [ObservableProperty] private string _driveFolderName = AppSettings.DefaultDriveFolderName;


    // --- Schedule (section 13) ---
    [ObservableProperty] private bool _scheduleEnabled;
    [ObservableProperty] private string _scheduleTime = "02:00";
    [ObservableProperty] private bool _daySunday;
    [ObservableProperty] private bool _dayMonday = true;
    [ObservableProperty] private bool _dayTuesday = true;
    [ObservableProperty] private bool _dayWednesday = true;
    [ObservableProperty] private bool _dayThursday = true;
    [ObservableProperty] private bool _dayFriday = true;
    [ObservableProperty] private bool _daySaturday;
    [ObservableProperty] private string _settingsStatus = string.Empty;

    // --- History / logs (section 10, 14) ---
    [ObservableProperty] private ObservableCollection<SyncJob> _history = new();
    [ObservableProperty] private SyncJob? _selectedJob;
    [ObservableProperty] private ObservableCollection<SyncOperation> _selectedJobOperations = new();
    [ObservableProperty] private ObservableCollection<SyncOperation> _lastErrors = new();

    public bool CanStartSync => !IsSyncing;

    public MainViewModel(
        IFolderService folderService,
        IGoogleAuthService authService,
        ISyncService syncService,
        ISettingsService settingsService,
        IHistoryService historyService,
        IScheduleService scheduleService)
    {
        _folderService = folderService;
        _authService = authService;
        _syncService = syncService;
        _settingsService = settingsService;
        _historyService = historyService;
        _scheduleService = scheduleService;

        _ = InitializeAsync();
    }

    public MainViewModel()
    {
    }

    private async Task InitializeAsync()
    {
        await LoadFoldersAsync();
        await CheckAuthStatusAsync();
        await LoadSettingsAsync();
        await LoadScheduleAsync();
        await LoadHistoryAsync();
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
        if (global::Avalonia.Application.Current?.ApplicationLifetime
                is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow != null)
        {
            var result = await desktop.MainWindow.StorageProvider.OpenFolderPickerAsync(
                new global::Avalonia.Platform.Storage.FolderPickerOpenOptions
                {
                    Title = "Selecione uma pasta para sincronizar",
                    AllowMultiple = false
                });

            if (result.Count > 0 && _folderService != null)
            {
                var added = await _folderService.AddFolderAsync(result[0].Path.LocalPath);
                if (added != null)
                    await LoadFoldersAsync();
            }
        }
    }

    [RelayCommand]
    private async Task RemoveFolderAsync(SyncFolder? folder)
    {
        if (folder != null && _folderService != null)
        {
            await _folderService.RemoveFolderAsync(folder.Id);
            Folders.Remove(folder);
        }
    }

    public void OnRuleChanged(SyncFolder folder, OrganizationRule rule)
    {
        if (folder != null && _folderService != null)
            _ = _folderService.UpdateFolderRuleAsync(folder.Id, rule);
    }

    [RelayCommand(CanExecute = nameof(CanStartSync))]
    private async Task RunSyncAsync()
    {
        if (_syncService == null) return;

        IsSyncing = true;
        SyncProgress = 0;
        _syncCts = new CancellationTokenSource();

        if (_folderService != null)
        {
            foreach (var folder in Folders)
                await _folderService.UpdateFolderRuleAsync(folder.Id, folder.OrganizationRule);
        }

        var options = new SyncRunOptions
        {
            DryRun = DryRun,
            DeleteAfterUpload = DeleteAfterUpload,
            DriveFolderName = DriveFolderName,
            Trigger = SyncTrigger.Manual
        };

        try
        {
            await Task.Run(() => _syncService.RunSyncAsync(options, ReportProgress, _syncCts.Token));
        }
        finally
        {
            _syncCts.Dispose();
            _syncCts = null;
            IsSyncing = false;
            await LoadHistoryAsync();
        }
    }

    [RelayCommand]
    private void CancelSync() => _syncCts?.Cancel();

    private void ReportProgress(string status, int progress)
    {
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            SyncStatusText = status;
            SyncProgress = progress;
        });
    }

    // ---------- Settings ----------

    private async Task LoadSettingsAsync()
    {
        if (_settingsService == null) return;
        var settings = await _settingsService.GetAsync();
        DeleteAfterUpload = settings.DeleteAfterUpload;
        DryRun = settings.DryRunByDefault;
        DriveFolderName = settings.DriveFolderName;
    }

    private async Task LoadScheduleAsync()
    {
        if (_scheduleService == null) return;
        var config = await _scheduleService.GetAsync();
        ScheduleEnabled = config.IsEnabled;
        ScheduleTime = config.TimeOfDay;

        var days = config.GetDays().ToHashSet();
        DaySunday = days.Contains(DayOfWeek.Sunday);
        DayMonday = days.Contains(DayOfWeek.Monday);
        DayTuesday = days.Contains(DayOfWeek.Tuesday);
        DayWednesday = days.Contains(DayOfWeek.Wednesday);
        DayThursday = days.Contains(DayOfWeek.Thursday);
        DayFriday = days.Contains(DayOfWeek.Friday);
        DaySaturday = days.Contains(DayOfWeek.Saturday);
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (_settingsService != null)
        {
            var folderName = string.IsNullOrWhiteSpace(DriveFolderName)
                ? AppSettings.DefaultDriveFolderName
                : DriveFolderName.Trim();
            DriveFolderName = folderName;

            await _settingsService.SaveAsync(new AppSettings
            {
                DeleteAfterUpload = DeleteAfterUpload,
                DryRunByDefault = DryRun,
                DriveFolderName = folderName
            });
        }

        if (_scheduleService != null)
        {
            var days = new List<int>();
            if (DaySunday) days.Add((int)DayOfWeek.Sunday);
            if (DayMonday) days.Add((int)DayOfWeek.Monday);
            if (DayTuesday) days.Add((int)DayOfWeek.Tuesday);
            if (DayWednesday) days.Add((int)DayOfWeek.Wednesday);
            if (DayThursday) days.Add((int)DayOfWeek.Thursday);
            if (DayFriday) days.Add((int)DayOfWeek.Friday);
            if (DaySaturday) days.Add((int)DayOfWeek.Saturday);

            var config = await _scheduleService.GetAsync();
            config.IsEnabled = ScheduleEnabled;
            config.TimeOfDay = ScheduleTime;
            config.DaysOfWeek = string.Join(',', days);
            await _scheduleService.SaveAsync(config);
        }

        SettingsStatus = $"Configurações salvas às {DateTime.Now:HH:mm:ss}.";
    }

    // ---------- History ----------

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        if (_historyService == null) return;
        var jobs = await _historyService.GetJobsAsync();
        History = new ObservableCollection<SyncJob>(jobs);

        var recentErrors = await _historyService.GetRecentOperationsAsync(200);
        LastErrors = new ObservableCollection<SyncOperation>(
            recentErrors.Where(o => o.Type is OperationType.Error or OperationType.ValidationFailed).Take(30));
    }

    partial void OnSelectedJobChanged(SyncJob? value) => _ = LoadJobOperationsAsync(value);

    private async Task LoadJobOperationsAsync(SyncJob? job)
    {
        if (job == null || _historyService == null)
        {
            SelectedJobOperations = new ObservableCollection<SyncOperation>();
            return;
        }

        var ops = await _historyService.GetOperationsAsync(job.Id);
        SelectedJobOperations = new ObservableCollection<SyncOperation>(ops);
    }
}
