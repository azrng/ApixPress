using Avalonia.Controls.Notifications;

namespace ApixPress.App.Services.Interfaces;

public interface IAppNotificationService
{
    void Show(string title, string content, NotificationType type = NotificationType.Information, TimeSpan? expiration = null);

    void ShowSuccess(string title, string content, TimeSpan? expiration = null);

    void ShowError(string title, string content, TimeSpan? expiration = null);
}
