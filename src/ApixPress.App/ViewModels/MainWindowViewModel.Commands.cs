using Avalonia.Controls;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel
{
    public async Task InitializeAsync()
    {
        if (_initialized)
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
    }
}
