using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.DependencyInjection;
using UrsaNotification = Ursa.Controls.Notification;
using UrsaWindowNotificationManager = Ursa.Controls.WindowNotificationManager;

namespace ApixPress.App.Services.Implementations;

public sealed class AppNotificationService : IAppNotificationService, ISingletonDependency
{
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromSeconds(3);

    private readonly IWindowHostService _windowHostService;
    private UrsaWindowNotificationManager? _notificationManager;
    private Window? _registeredWindow;

    public AppNotificationService(IWindowHostService windowHostService)
    {
        _windowHostService = windowHostService;
    }

    public void Show(string title, string content, NotificationType type = NotificationType.Information, TimeSpan? expiration = null)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            var manager = ResolveManager();
            if (manager is null)
            {
                return;
            }

            manager.Show(new UrsaNotification(title, content, type, expiration ?? DefaultExpiration, true, null, null));
        });
    }

    public void ShowSuccess(string title, string content, TimeSpan? expiration = null)
    {
        Show(title, content, NotificationType.Success, expiration);
    }

    public void ShowError(string title, string content, TimeSpan? expiration = null)
    {
        Show(title, content, NotificationType.Error, expiration);
    }

    private UrsaWindowNotificationManager? ResolveManager()
    {
        var window = _windowHostService.MainWindow;
        if (window is null)
        {
            return null;
        }

        if (ReferenceEquals(window, _registeredWindow) && _notificationManager is not null)
        {
            ConfigureManager(_notificationManager);
            return _notificationManager;
        }

        if (UrsaWindowNotificationManager.TryGetNotificationManager(window, out var existingManager) && existingManager is not null)
        {
            _notificationManager = existingManager;
        }
        else
        {
            _notificationManager = new UrsaWindowNotificationManager(window);
        }
        _registeredWindow = window;
        ConfigureManager(_notificationManager);
        return _notificationManager;
    }

    private static void ConfigureManager(UrsaWindowNotificationManager manager)
    {
        manager.Position = NotificationPosition.TopRight;
        manager.MaxItems = 3;
        manager.Margin = new Thickness(0, 52, 20, 0);
    }
}
