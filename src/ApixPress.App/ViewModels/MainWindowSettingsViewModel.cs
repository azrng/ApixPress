using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public sealed partial class MainWindowSettingsViewModel : ViewModelBase
{
    private const string GeneralSection = "general";
    private const string AboutSection = "about";

    private readonly IAppShellSettingsService _appShellSettingsService;
    private readonly IApplicationUpdateService _applicationUpdateService;
    private readonly IWindowHostService _windowHostService;
    private readonly Action<string> _setStatusMessage;
    private readonly Action _notifyShellState;
    private readonly SemaphoreSlim _shellSettingsSaveSemaphore = new(1, 1);
    private CancellationTokenSource? _shellSettingsSaveCancellationTokenSource;
    private bool _initialized;
    private bool _isApplyingShellSettings;

    public MainWindowSettingsViewModel(
        IAppShellSettingsService appShellSettingsService,
        IApplicationUpdateService applicationUpdateService,
        IWindowHostService windowHostService,
        string currentAppVersion,
        Action<string> setStatusMessage,
        Action notifyShellState)
    {
        _appShellSettingsService = appShellSettingsService;
        _applicationUpdateService = applicationUpdateService;
        _windowHostService = windowHostService;
        _setStatusMessage = setStatusMessage;
        _notifyShellState = notifyShellState;
        CurrentAppVersion = currentAppVersion;
        UpdateChannelName = _applicationUpdateService.ChannelName;
        AboutUpdateStatus = _applicationUpdateService.IsConfigured
            ? $"当前通过 {UpdateChannelName} 检查更新。"
            : "尚未配置更新源，请先补充 appsettings.json 中的 Update 节点。";
    }

    public string CurrentAppVersion { get; }

    public string UpdateChannelName { get; }

    public bool ShowGeneralSettingsSection => CurrentSettingsSection == GeneralSection;

    public bool ShowAboutSettingsSection => CurrentSettingsSection == AboutSection;

    public string CurrentSettingsTitle => ShowAboutSettingsSection ? "关于" : "通用";

    public string CheckForUpdatesButtonText => IsCheckingForUpdates ? "检查中..." : "检查更新";

    [ObservableProperty]
    private string currentSettingsSection = GeneralSection;

    [ObservableProperty]
    private decimal requestTimeoutMilliseconds = 30000;

    [ObservableProperty]
    private bool validateSslCertificate = true;

    [ObservableProperty]
    private bool autoFollowRedirects = true;

    [ObservableProperty]
    private bool sendNoCacheHeader;

    [ObservableProperty]
    private bool enableVerboseLogging;

    [ObservableProperty]
    private bool enableUpdateReminder = true;

    [ObservableProperty]
    private string generalSettingsSaveStatus = "设置会自动保存到本地工作目录。";

    [ObservableProperty]
    private bool isCheckingForUpdates;

    [ObservableProperty]
    private string aboutUpdateStatus = "尚未检查更新。";

    [ObservableProperty]
    private string lastUpdateCheckText = "尚未检查";

    [ObservableProperty]
    private string latestAvailableVersion = "尚未检查";

    protected override void DisposeManaged()
    {
        CancellationTokenSourceHelper.CancelAndDispose(ref _shellSettingsSaveCancellationTokenSource);
    }

    public async Task InitializeAsync()
    {
        if (_initialized || IsDisposed)
        {
            return;
        }

        _initialized = true;
        await LoadShellSettingsAsync();
    }

    public void SelectGeneralSection()
    {
        if (IsDisposed)
        {
            return;
        }

        CurrentSettingsSection = GeneralSection;
    }

    partial void OnCurrentSettingsSectionChanged(string value)
    {
        OnPropertyChanged(nameof(ShowGeneralSettingsSection));
        OnPropertyChanged(nameof(ShowAboutSettingsSection));
        OnPropertyChanged(nameof(CurrentSettingsTitle));
        _notifyShellState();
    }

    partial void OnRequestTimeoutMillisecondsChanged(decimal value)
    {
        TriggerShellSettingsSave();
    }

    partial void OnValidateSslCertificateChanged(bool value)
    {
        TriggerShellSettingsSave();
    }

    partial void OnAutoFollowRedirectsChanged(bool value)
    {
        TriggerShellSettingsSave();
    }

    partial void OnSendNoCacheHeaderChanged(bool value)
    {
        TriggerShellSettingsSave();
    }

    partial void OnEnableVerboseLoggingChanged(bool value)
    {
        TriggerShellSettingsSave();
    }

    partial void OnEnableUpdateReminderChanged(bool value)
    {
        TriggerShellSettingsSave();
    }

    partial void OnIsCheckingForUpdatesChanged(bool value)
    {
        OnPropertyChanged(nameof(CheckForUpdatesButtonText));
    }

    [RelayCommand]
    private void ShowGeneralSettings()
    {
        SelectGeneralSection();
    }

    [RelayCommand]
    private void ShowAboutSettings()
    {
        CurrentSettingsSection = AboutSection;
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsCheckingForUpdates || IsDisposed)
        {
            return;
        }

        if (!_applicationUpdateService.IsConfigured)
        {
            const string message = "尚未配置更新源，请先补充 appsettings.json 中的 Update 节点。";
            PublishStatus(message);
            AboutUpdateStatus = message;
            return;
        }

        IsCheckingForUpdates = true;
        var checkingMessage = $"正在检查 {UpdateChannelName} 更新...";
        PublishStatus(checkingMessage);
        AboutUpdateStatus = checkingMessage;

        try
        {
            var checkResult = await _applicationUpdateService.CheckForUpdatesAsync(CurrentAppVersion, CancellationToken.None);
            LastUpdateCheckText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            if (!checkResult.IsSuccess || checkResult.Data is null)
            {
                var failedMessage = $"检查更新失败：{checkResult.Message}";
                PublishStatus(failedMessage);
                AboutUpdateStatus = failedMessage;
                return;
            }

            LatestAvailableVersion = checkResult.Data.LatestVersion;
            if (!checkResult.Data.HasUpdate)
            {
                var latestMessage = $"当前已是最新版本 {checkResult.Data.CurrentVersion}。";
                PublishStatus(latestMessage);
                AboutUpdateStatus = latestMessage;
                return;
            }

            var startingMessage = $"发现新版本 {checkResult.Data.LatestVersion}，正在启动更新程序...";
            PublishStatus(startingMessage);
            AboutUpdateStatus = startingMessage;

            var startResult = await _applicationUpdateService.StartUpdateAsync(checkResult.Data, CancellationToken.None);
            if (!startResult.IsSuccess)
            {
                var startFailedMessage = $"启动更新失败：{startResult.Message}";
                PublishStatus(startFailedMessage);
                AboutUpdateStatus = startFailedMessage;
                return;
            }

            var startedMessage = $"更新程序已启动，将通过 {UpdateChannelName} 拉取新版本。";
            PublishStatus(startedMessage);
            AboutUpdateStatus = startedMessage;
            _windowHostService.MainWindow?.Close();
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    private async Task LoadShellSettingsAsync()
    {
        _isApplyingShellSettings = true;
        try
        {
            var result = await _appShellSettingsService.LoadAsync(CancellationToken.None);
            var settings = result.Data ?? new AppShellSettingsDto();

            RequestTimeoutMilliseconds = settings.RequestTimeoutMilliseconds;
            ValidateSslCertificate = settings.ValidateSslCertificate;
            AutoFollowRedirects = settings.AutoFollowRedirects;
            SendNoCacheHeader = settings.SendNoCacheHeader;
            EnableVerboseLogging = settings.EnableVerboseLogging;
            EnableUpdateReminder = settings.EnableUpdateReminder;
            GeneralSettingsSaveStatus = result.IsSuccess
                ? "设置会自动保存到本地工作目录。"
                : "设置读取失败，已回退默认值。";
        }
        finally
        {
            _isApplyingShellSettings = false;
        }
    }

    private void TriggerShellSettingsSave()
    {
        if (_isApplyingShellSettings || !_initialized || IsDisposed)
        {
            return;
        }

        var cancellationToken = CancellationTokenSourceHelper.Refresh(ref _shellSettingsSaveCancellationTokenSource).Token;
        _ = SaveShellSettingsDeferredAsync(cancellationToken);
    }

    private async Task SaveShellSettingsDeferredAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(250, cancellationToken);
            await SaveShellSettingsAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task SaveShellSettingsAsync(CancellationToken cancellationToken)
    {
        await _shellSettingsSaveSemaphore.WaitAsync(cancellationToken);
        try
        {
            var result = await _appShellSettingsService.SaveAsync(new AppShellSettingsDto
            {
                RequestTimeoutMilliseconds = (int)RequestTimeoutMilliseconds,
                ValidateSslCertificate = ValidateSslCertificate,
                AutoFollowRedirects = AutoFollowRedirects,
                SendNoCacheHeader = SendNoCacheHeader,
                EnableVerboseLogging = EnableVerboseLogging,
                EnableUpdateReminder = EnableUpdateReminder
            }, cancellationToken);

            GeneralSettingsSaveStatus = result.IsSuccess
                ? $"已自动保存 {DateTime.Now:HH:mm:ss}"
                : $"保存失败：{result.Message}";
            _notifyShellState();
        }
        finally
        {
            _shellSettingsSaveSemaphore.Release();
        }
    }

    private void PublishStatus(string message)
    {
        _setStatusMessage(message);
        _notifyShellState();
    }
}
