using CommunityToolkit.Mvvm.Input;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private void OpenSettingsDialog()
    {
        ShellPanels.OpenSettingsDialogCommand.Execute(null);
    }

    [RelayCommand]
    private void CloseSettingsDialog()
    {
        ShellPanels.CloseSettingsDialogCommand.Execute(null);
    }

    [RelayCommand]
    private void ToggleNotificationCenter()
    {
        ShellPanels.ToggleNotificationCenterCommand.Execute(null);
    }

    [RelayCommand]
    private void MarkAllNotificationsRead()
    {
        ShellPanels.MarkAllNotificationsReadCommand.Execute(null);
    }
}
