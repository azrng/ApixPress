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
        await Task.Delay(240);
        LastUpdateCheckText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        AboutUpdateStatus = $"已完成 Mock 检查，发现可用版本 {LatestMockVersion}。";
        StatusMessage = AboutUpdateStatus;
        NotifyShellState();
    }
}
