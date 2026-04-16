using CommunityToolkit.Mvvm.Input;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private void OpenSettingsDialog()
    {
        CurrentSettingsSection = SettingsSections.General;
        IsSettingsDialogOpen = true;
        IsNotificationCenterOpen = false;
        StatusMessage = "可在这里调整通用设置和查看版本信息。";
        NotifyShellState();
    }

    [RelayCommand]
    private void CloseSettingsDialog()
    {
        IsSettingsDialogOpen = false;
        StatusMessage = ActiveProjectTab?.StatusMessage ?? BrowserStatusText;
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowGeneralSettings()
    {
        CurrentSettingsSection = SettingsSections.General;
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowAboutSettings()
    {
        CurrentSettingsSection = SettingsSections.About;
        NotifyShellState();
    }

    [RelayCommand]
    private void ToggleNotificationCenter()
    {
        IsNotificationCenterOpen = !IsNotificationCenterOpen;
        if (IsNotificationCenterOpen)
        {
            IsSettingsDialogOpen = false;
            MarkAllNotificationsRead();
            StatusMessage = "这里展示近期动态和提醒。";
        }
        else
        {
            StatusMessage = ActiveProjectTab?.StatusMessage ?? BrowserStatusText;
        }

        NotifyShellState();
    }

    [RelayCommand]
    private void MarkAllNotificationsRead()
    {
        foreach (var item in Notifications)
        {
            item.IsUnread = false;
        }

        NotifyShellState();
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsCheckingForUpdates)
        {
            return;
        }

        if (!_applicationUpdateService.IsConfigured)
        {
            AboutUpdateStatus = "尚未配置更新源，请先补充 appsettings.json 中的 Update 节点。";
            StatusMessage = AboutUpdateStatus;
            NotifyShellState();
            return;
        }

        IsCheckingForUpdates = true;
        AboutUpdateStatus = $"正在检查 {UpdateChannelName} 更新...";
        StatusMessage = AboutUpdateStatus;
        NotifyShellState();

        try
        {
            var checkResult = await _applicationUpdateService.CheckForUpdatesAsync(CurrentAppVersion, CancellationToken.None);
            LastUpdateCheckText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            if (!checkResult.IsSuccess || checkResult.Data is null)
            {
                AboutUpdateStatus = $"检查更新失败：{checkResult.Message}";
                StatusMessage = AboutUpdateStatus;
                NotifyShellState();
                return;
            }

            LatestAvailableVersion = checkResult.Data.LatestVersion;
            if (!checkResult.Data.HasUpdate)
            {
                AboutUpdateStatus = $"当前已是最新版本 {checkResult.Data.CurrentVersion}。";
                StatusMessage = AboutUpdateStatus;
                NotifyShellState();
                return;
            }

            AboutUpdateStatus = $"发现新版本 {checkResult.Data.LatestVersion}，正在启动更新程序...";
            StatusMessage = AboutUpdateStatus;
            NotifyShellState();

            var startResult = await _applicationUpdateService.StartUpdateAsync(CurrentAppVersion, CancellationToken.None);
            if (!startResult.IsSuccess)
            {
                AboutUpdateStatus = $"启动更新失败：{startResult.Message}";
                StatusMessage = AboutUpdateStatus;
                NotifyShellState();
                return;
            }

            AboutUpdateStatus = $"更新程序已启动，将通过 {UpdateChannelName} 拉取新版本。";
            StatusMessage = AboutUpdateStatus;
            NotifyShellState();
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }
}
