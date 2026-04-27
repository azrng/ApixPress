using Avalonia.Controls;
using Avalonia.Controls.Notifications;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel
{
    public async Task InitializeAsync()
    {
        if (_initialized || IsDisposed)
        {
            return;
        }

        _initialized = true;
        IsBusy = true;
        await SettingsCenter.InitializeAsync();
        await ProjectPanel.LoadProjectsAsync(autoSelect: false);
        StatusMessage = BrowserStatusText;
        IsBusy = false;
        NotifyShellState();
        _ = CheckStartupUpdateReminderAsync();
    }

    private async Task CheckStartupUpdateReminderAsync()
    {
        if (IsDisposed
            || !SettingsCenter.EnableUpdateReminder
            || !_applicationUpdateService.IsConfigured)
        {
            return;
        }

        try
        {
            var result = await _applicationUpdateService.CheckForUpdatesAsync(ResolveCurrentAppVersion(), CancellationToken.None);
            if (IsDisposed || !result.IsSuccess || result.Data is not { HasUpdate: true } updateInfo)
            {
                return;
            }

            var latestVersion = string.IsNullOrWhiteSpace(updateInfo.LatestVersion)
                ? "新版本"
                : updateInfo.LatestVersion;
            var message = $"发现 ApixPress {latestVersion}，可前往设置中心检查并启动更新。";
            ShellPanels.Notifications.Insert(0, new NotificationItemViewModel
            {
                Title = "发现新版本",
                Message = message,
                RelativeTimeText = "刚刚",
                IsUnread = true
            });
            _appNotificationService.Show("发现新版本", message, NotificationType.Information, TimeSpan.FromSeconds(6));
            NotifyShellState();
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // Startup update reminders must never interrupt application launch.
        }
    }
}
