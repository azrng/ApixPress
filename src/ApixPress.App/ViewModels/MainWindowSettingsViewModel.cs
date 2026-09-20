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
    private const string StorageSection = "storage";
    private const string AboutSection = "about";

    // 更新就绪后到关窗的停留时间：给用户看到“即将自动重启”的缓冲，避免窗口瞬间消失又被当成异常退出
    private const int UpdateRestartDelayMilliseconds = 1500;

    private readonly IAppShellSettingsService _appShellSettingsService;
    private readonly IApplicationUpdateService _applicationUpdateService;
    private readonly IApplicationRestartService _applicationRestartService;
    private readonly IWindowHostService _windowHostService;
    private readonly IFilePickerService _filePickerService;
    private readonly ISystemDataService _systemDataService;
    private readonly Action<string> _setStatusMessage;
    private readonly Action _notifyShellState;
    private readonly Action _clearSystemDataViews;
    private readonly SemaphoreSlim _shellSettingsSaveSemaphore = new(1, 1);
    private CancellationTokenSource? _shellSettingsSaveCancellationTokenSource;
    private bool _initialized;
    private bool _isApplyingShellSettings;

    public MainWindowSettingsViewModel(
        IAppShellSettingsService appShellSettingsService,
        IApplicationUpdateService applicationUpdateService,
        IApplicationRestartService applicationRestartService,
        IWindowHostService windowHostService,
        IFilePickerService filePickerService,
        ISystemDataService systemDataService,
        string currentAppVersion,
        Action<string> setStatusMessage,
        Action notifyShellState,
        Action clearSystemDataViews)
    {
        _appShellSettingsService = appShellSettingsService;
        _applicationUpdateService = applicationUpdateService;
        _applicationRestartService = applicationRestartService;
        _windowHostService = windowHostService;
        _filePickerService = filePickerService;
        _systemDataService = systemDataService;
        _setStatusMessage = setStatusMessage;
        _notifyShellState = notifyShellState;
        _clearSystemDataViews = clearSystemDataViews;
        CurrentAppVersion = currentAppVersion;
        UpdateChannelName = _applicationUpdateService.ChannelName;
        AboutUpdateStatus = _applicationUpdateService.IsConfigured
            ? $"当前通过 {UpdateChannelName} 检查更新。"
            : "尚未配置更新源，请先补充 appsettings.json 中的 Update 节点。";
    }

    public string CurrentAppVersion { get; }

    public string UpdateChannelName { get; }

    public bool ShowGeneralSettingsSection => CurrentSettingsSection == GeneralSection;

    public bool ShowStorageSettingsSection => CurrentSettingsSection == StorageSection;

    public bool ShowAboutSettingsSection => CurrentSettingsSection == AboutSection;

    public string CurrentSettingsTitle => CurrentSettingsSection switch
    {
        StorageSection => "存储",
        AboutSection => "关于",
        _ => "通用"
    };

    public string CurrentSettingsSubtitle => CurrentSettingsSection switch
    {
        StorageSection => "设置数据库保存目录，并管理本地系统数据。",
        AboutSection => "查看版本信息和更新状态。",
        _ => "设置会自动保存到用户数据目录，并在后续请求中生效。"
    };

    public string CheckForUpdatesButtonText => IsCheckingForUpdates
        ? "检查中..."
        : IsUpdating ? "更新中..." : "检查更新";

    public string ClearSystemDataButtonText => IsClearingSystemData ? "清空中..." : "一键清空系统数据";

    public string DefaultStorageDirectoryPath => AppStoragePaths.DefaultStorageDirectory;

    public string EffectiveStorageDirectoryPath => string.IsNullOrWhiteSpace(StorageDirectoryPath)
        ? DefaultStorageDirectoryPath
        : AppStoragePaths.ResolveStorageDirectory(StorageDirectoryPath);

    public string StorageDirectoryModeText => string.IsNullOrWhiteSpace(StorageDirectoryPath)
        ? "当前使用默认存储目录"
        : "当前使用自定义存储目录";

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
    private string generalSettingsSaveStatus = "设置会自动保存到用户数据目录。";

    [ObservableProperty]
    private string storageDirectoryPath = string.Empty;

    [ObservableProperty]
    private string storageSettingsSaveStatus = "数据库目录修改后将在下次启动时生效。";

    [ObservableProperty]
    private string clearSystemDataStatus = "清空后会删除所有项目、接口、用例、环境与请求历史数据，应用会回到空项目状态。";

    [ObservableProperty]
    private bool isClearSystemDataConfirmDialogOpen;

    [ObservableProperty]
    private bool isSystemDataRestartPromptOpen;

    [ObservableProperty]
    private bool isClearingSystemData;

    [ObservableProperty]
    private bool isRestartingApplication;

    [ObservableProperty]
    private bool isCheckingForUpdates;

    [ObservableProperty]
    private string aboutUpdateStatus = "尚未检查更新。";

    [ObservableProperty]
    private string lastUpdateCheckText = "尚未检查";

    [ObservableProperty]
    private string latestAvailableVersion = "尚未检查";

    [ObservableProperty]
    private bool isUpdating;

    [ObservableProperty]
    private double updateProgressPercent;

    [ObservableProperty]
    private bool isUpdateProgressIndeterminate;

    [ObservableProperty]
    private string updateStageText = string.Empty;

    private CancellationTokenSource? _updateCancellationTokenSource;

    protected override void DisposeManaged()
    {
        CancellationTokenSourceHelper.CancelAndDispose(ref _shellSettingsSaveCancellationTokenSource);
        CancellationTokenSourceHelper.CancelAndDispose(ref _updateCancellationTokenSource);
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

    public void SelectStorageSection()
    {
        if (IsDisposed)
        {
            return;
        }

        CurrentSettingsSection = StorageSection;
    }

    partial void OnCurrentSettingsSectionChanged(string value)
    {
        OnPropertyChanged(nameof(ShowGeneralSettingsSection));
        OnPropertyChanged(nameof(ShowStorageSettingsSection));
        OnPropertyChanged(nameof(ShowAboutSettingsSection));
        OnPropertyChanged(nameof(CurrentSettingsTitle));
        OnPropertyChanged(nameof(CurrentSettingsSubtitle));
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

    partial void OnStorageDirectoryPathChanged(string value)
    {
        OnPropertyChanged(nameof(EffectiveStorageDirectoryPath));
        OnPropertyChanged(nameof(StorageDirectoryModeText));
        TriggerShellSettingsSave();
    }

    partial void OnIsCheckingForUpdatesChanged(bool value)
    {
        OnPropertyChanged(nameof(CheckForUpdatesButtonText));
    }

    partial void OnIsUpdatingChanged(bool value)
    {
        OnPropertyChanged(nameof(CheckForUpdatesButtonText));
        CancelUpdateCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsClearingSystemDataChanged(bool value)
    {
        OnPropertyChanged(nameof(ClearSystemDataButtonText));
    }

    partial void OnIsRestartingApplicationChanged(bool value)
    {
        OnPropertyChanged(nameof(RestartApplicationButtonText));
    }

    public string RestartApplicationButtonText => IsRestartingApplication ? "重启中..." : "立即重启";

    [RelayCommand]
    private void ShowGeneralSettings()
    {
        SelectGeneralSection();
    }

    [RelayCommand]
    private void ShowStorageSettings()
    {
        SelectStorageSection();
    }

    [RelayCommand]
    private void ShowAboutSettings()
    {
        CurrentSettingsSection = AboutSection;
    }

    [RelayCommand]
    private async Task ChooseStorageDirectoryAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        var directory = await _filePickerService.PickStorageDirectoryAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(directory))
        {
            StorageSettingsSaveStatus = "未选择目录，当前数据库存储设置保持不变。";
            return;
        }

        StorageDirectoryPath = directory;
        StorageSettingsSaveStatus = "已选择新的数据库存储目录，保存后将在下次启动时生效。";
        PublishStatus("已更新数据库存储目录，下次启动后生效。");
    }

    [RelayCommand]
    private void ResetStorageDirectory()
    {
        if (IsDisposed)
        {
            return;
        }

        StorageDirectoryPath = string.Empty;
        StorageSettingsSaveStatus = "已恢复默认数据库存储目录，下次启动后生效。";
        PublishStatus("已恢复默认数据库存储目录。");
    }

    [RelayCommand]
    private void RequestClearSystemData()
    {
        if (IsDisposed)
        {
            return;
        }

        IsClearSystemDataConfirmDialogOpen = true;
    }

    [RelayCommand]
    private void CancelClearSystemData()
    {
        if (IsDisposed || IsClearingSystemData)
        {
            return;
        }

        IsClearSystemDataConfirmDialogOpen = false;
        ClearSystemDataStatus = "已取消清空系统数据。";
    }

    [RelayCommand]
    private async Task ConfirmClearSystemDataAsync()
    {
        if (IsDisposed || IsClearingSystemData)
        {
            return;
        }

        IsClearingSystemData = true;
        ClearSystemDataStatus = "正在清空所有系统数据...";

        try
        {
            var result = await _systemDataService.ClearAllAsync(CancellationToken.None);
            if (!result.IsSuccess)
            {
                var failedMessage = $"清空系统数据失败：{result.Message}";
                ClearSystemDataStatus = failedMessage;
                PublishStatus(failedMessage);
                return;
            }

            _clearSystemDataViews();
            IsClearSystemDataConfirmDialogOpen = false;
            IsSystemDataRestartPromptOpen = true;
            var successMessage = "已清空所有系统数据，建议重启应用以释放全部运行状态。";
            ClearSystemDataStatus = successMessage;
            PublishStatus(successMessage);
        }
        finally
        {
            IsClearingSystemData = false;
        }
    }

    [RelayCommand]
    private void DismissSystemDataRestartPrompt()
    {
        if (IsDisposed || IsRestartingApplication)
        {
            return;
        }

        IsSystemDataRestartPromptOpen = false;
        PublishStatus("已稍后重启，系统数据已清空。");
    }

    [RelayCommand]
    private async Task RestartApplicationAsync()
    {
        if (IsDisposed || IsRestartingApplication)
        {
            return;
        }

        IsRestartingApplication = true;
        PublishStatus("正在重启 ApixPress...");

        try
        {
            var result = await _applicationRestartService.RestartAsync(CancellationToken.None);
            if (!result.IsSuccess)
            {
                var failedMessage = $"重启失败：{result.Message}";
                PublishStatus(failedMessage);
                ClearSystemDataStatus = failedMessage;
                return;
            }

            _windowHostService.MainWindow?.Close();
        }
        finally
        {
            IsRestartingApplication = false;
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsCheckingForUpdates || IsUpdating || IsDisposed)
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

            var startingMessage = $"发现新版本 {checkResult.Data.LatestVersion}，开始下载更新包...";
            PublishStatus(startingMessage);
            AboutUpdateStatus = startingMessage;

            await RunUpdateFlowAsync(checkResult.Data);
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    /// <summary>
    /// 主进程内执行完整更新：下载→校验→解压→生成替换脚本→退出并自动重启。
    /// 成功后关闭主窗口，由隐藏脚本等待进程退出后覆盖安装目录并重启新版本。
    /// </summary>
    private async Task RunUpdateFlowAsync(AppUpdateCheckResultDto updateInfo)
    {
        IsUpdating = true;
        UpdateProgressPercent = 0;
        IsUpdateProgressIndeterminate = false;
        CancellationTokenSourceHelper.CancelAndDispose(ref _updateCancellationTokenSource);
        _updateCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _updateCancellationTokenSource.Token;
        var progress = new Progress<AppUpdateProgressDto>(ApplyUpdateProgress);

        try
        {
            var prepared = await _applicationUpdateService.PrepareUpdatePackageAsync(updateInfo, progress, cancellationToken);
            if (!prepared.IsSuccess || prepared.Data is null)
            {
                ReportUpdateAborted(prepared.Message);
                return;
            }

            var applyingMessage = "更新包已就绪，正在应用更新...";
            UpdateStageText = applyingMessage;
            AboutUpdateStatus = applyingMessage;
            PublishStatus(applyingMessage);

            var applied = await _applicationUpdateService.ApplyStagedUpdateAsync(prepared.Data, cancellationToken);
            if (!applied.IsSuccess)
            {
                ReportUpdateAborted(applied.Message);
                return;
            }

            // 脚本已启动，更新进入不可取消阶段；取消按钮随 IsUpdating 复位而禁用
            IsUpdating = false;

            var readyMessage = $"更新已就绪（{updateInfo.LatestVersion}），即将退出并自动重启...";
            UpdateStageText = readyMessage;
            AboutUpdateStatus = readyMessage;
            PublishStatus(readyMessage);

            // 短暂停留让用户看到完成状态：窗口瞬间消失会再次造成“程序消失”的错觉
            await Task.Delay(UpdateRestartDelayMilliseconds, CancellationToken.None);
            _windowHostService.MainWindow?.Close();
        }
        finally
        {
            IsUpdating = false;
            CancellationTokenSourceHelper.CancelAndDispose(ref _updateCancellationTokenSource);
        }
    }

    /// <summary>取消进行中的更新：中断下载并让服务端清理临时工作区。</summary>
    [RelayCommand(CanExecute = nameof(CanCancelUpdate))]
    private void CancelUpdate()
    {
        _updateCancellationTokenSource?.Cancel();
    }

    private bool CanCancelUpdate() => IsUpdating && !IsDisposed;

    /// <summary>更新流程中止时区分“用户取消”与“真实失败”，避免把主动取消当错误弹给用户。</summary>
    private void ReportUpdateAborted(string serviceMessage)
    {
        var isCancelled = _updateCancellationTokenSource is { IsCancellationRequested: true };
        var message = isCancelled ? "已取消更新。" : serviceMessage;
        UpdateStageText = string.Empty;
        UpdateProgressPercent = 0;
        AboutUpdateStatus = message;
        PublishStatus(message);
    }

    private void ApplyUpdateProgress(AppUpdateProgressDto progress)
    {
        if (progress.Stage != AppUpdateProgressStage.Downloading)
        {
            IsUpdateProgressIndeterminate = false;
            UpdateStageText = progress.Stage switch
            {
                AppUpdateProgressStage.Verifying => "校验安装包哈希...",
                AppUpdateProgressStage.Extracting => "解压安装包...",
                _ => "正在准备更新..."
            };
            return;
        }

        if (progress.TotalBytes is { } totalBytes and > 0)
        {
            IsUpdateProgressIndeterminate = false;
            var percent = Math.Round(progress.ReceivedBytes * 100.0 / totalBytes, 1);
            UpdateProgressPercent = percent;
            UpdateStageText = $"下载更新包 {percent:0.0}%（{FormatMegabytes(progress.ReceivedBytes)}/{FormatMegabytes(totalBytes)}）";
            return;
        }

        // 服务端未返回 Content-Length：进度条退化为不确定态，只展示已下载量
        IsUpdateProgressIndeterminate = true;
        UpdateStageText = $"下载更新包 {FormatMegabytes(progress.ReceivedBytes)}";
    }

    private static string FormatMegabytes(long bytes)
    {
        return $"{bytes / 1024.0 / 1024.0:0.0} MB";
    }

    private async Task LoadShellSettingsAsync()
    {
        _isApplyingShellSettings = true;
        try
        {
            var result = await _appShellSettingsService.LoadAsync(CancellationToken.None);
            var settings = result.Data ?? new AppShellSettingsDto();

            StorageDirectoryPath = settings.StorageDirectoryPath;
            RequestTimeoutMilliseconds = settings.RequestTimeoutMilliseconds;
            ValidateSslCertificate = settings.ValidateSslCertificate;
            AutoFollowRedirects = settings.AutoFollowRedirects;
            SendNoCacheHeader = settings.SendNoCacheHeader;
            EnableVerboseLogging = settings.EnableVerboseLogging;
            EnableUpdateReminder = settings.EnableUpdateReminder;
            GeneralSettingsSaveStatus = result.IsSuccess
                ? "设置会自动保存到用户数据目录。"
                : "设置读取失败，已回退默认值。";
            StorageSettingsSaveStatus = result.IsSuccess
                ? "数据库目录修改后将在下次启动时生效。"
                : "设置读取失败，已回退默认存储目录。";
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
                StorageDirectoryPath = StorageDirectoryPath,
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
            StorageSettingsSaveStatus = result.IsSuccess
                ? $"已保存存储设置 {DateTime.Now:HH:mm:ss}，下次启动后生效。"
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
