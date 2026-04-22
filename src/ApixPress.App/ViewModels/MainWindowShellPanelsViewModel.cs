using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public sealed partial class MainWindowShellPanelsViewModel : ViewModelBase
{
    private readonly Action<string> _setStatusMessage;
    private readonly Func<string> _getDefaultStatusMessage;

    public MainWindowShellPanelsViewModel(
        MainWindowSettingsViewModel settingsCenter,
        ObservableCollection<NotificationItemViewModel> notifications,
        Action<string> setStatusMessage,
        Func<string> getDefaultStatusMessage)
    {
        SettingsCenter = settingsCenter;
        Notifications = notifications;
        _setStatusMessage = setStatusMessage;
        _getDefaultStatusMessage = getDefaultStatusMessage;

        Notifications.CollectionChanged += OnNotificationsCollectionChanged;
        AttachNotificationHandlers(Notifications);
    }

    public MainWindowSettingsViewModel SettingsCenter { get; }

    public ObservableCollection<NotificationItemViewModel> Notifications { get; }

    public bool HasUnreadNotifications => Notifications.Any(item => item.IsUnread);

    [ObservableProperty]
    private bool isSettingsDialogOpen;

    [ObservableProperty]
    private bool isNotificationCenterOpen;

    protected override void DisposeManaged()
    {
        Notifications.CollectionChanged -= OnNotificationsCollectionChanged;
        DetachNotificationHandlers(Notifications);
        SettingsCenter.Dispose();
    }

    [RelayCommand]
    private void OpenSettingsDialog()
    {
        if (IsDisposed)
        {
            return;
        }

        SettingsCenter.SelectGeneralSection();
        IsSettingsDialogOpen = true;
        IsNotificationCenterOpen = false;
        _setStatusMessage("可在这里调整通用设置和查看版本信息。");
    }

    [RelayCommand]
    private void CloseSettingsDialog()
    {
        if (IsDisposed)
        {
            return;
        }

        IsSettingsDialogOpen = false;
        _setStatusMessage(_getDefaultStatusMessage());
    }

    [RelayCommand]
    private void ToggleNotificationCenter()
    {
        if (IsDisposed)
        {
            return;
        }

        IsNotificationCenterOpen = !IsNotificationCenterOpen;
        if (IsNotificationCenterOpen)
        {
            IsSettingsDialogOpen = false;
            MarkAllNotificationsRead();
            _setStatusMessage("这里展示近期动态和提醒。");
            return;
        }

        _setStatusMessage(_getDefaultStatusMessage());
    }

    [RelayCommand]
    private void MarkAllNotificationsRead()
    {
        if (IsDisposed)
        {
            return;
        }

        foreach (var item in Notifications)
        {
            item.IsUnread = false;
        }
    }

    private void OnNotificationsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        if (e.OldItems is not null)
        {
            DetachNotificationHandlers(e.OldItems.OfType<NotificationItemViewModel>());
        }

        if (e.NewItems is not null)
        {
            AttachNotificationHandlers(e.NewItems.OfType<NotificationItemViewModel>());
        }

        OnPropertyChanged(nameof(HasUnreadNotifications));
    }

    private void OnNotificationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(e.PropertyName)
            || e.PropertyName == nameof(NotificationItemViewModel.IsUnread))
        {
            OnPropertyChanged(nameof(HasUnreadNotifications));
        }
    }

    private void AttachNotificationHandlers(IEnumerable<NotificationItemViewModel> items)
    {
        foreach (var item in items)
        {
            item.PropertyChanged += OnNotificationPropertyChanged;
        }
    }

    private void DetachNotificationHandlers(IEnumerable<NotificationItemViewModel> items)
    {
        foreach (var item in items)
        {
            item.PropertyChanged -= OnNotificationPropertyChanged;
        }
    }
}
