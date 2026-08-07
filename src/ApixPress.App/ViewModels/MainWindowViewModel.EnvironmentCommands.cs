using CommunityToolkit.Mvvm.Input;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private void OpenEnvironmentManager()
    {
        if (ActiveProjectTab is null)
        {
            StatusMessage = "请先打开一个项目标签页。";
            return;
        }

        IsEnvironmentManagerOpen = true;
        StatusMessage = $"正在管理项目 {ActiveProjectTab.Project.Name} 的环境。";
        NotifyShellState();
    }

    [RelayCommand]
    private void CloseEnvironmentManager()
    {
        IsEnvironmentManagerOpen = false;
        StatusMessage = ActiveProjectTab?.StatusMessage ?? BrowserStatusText;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task SaveEnvironmentManagerAsync()
    {
        if (ActiveProjectTab is null)
        {
            StatusMessage = "请先打开一个项目标签页。";
            return;
        }

        try
        {
            await ActiveProjectTab.SaveCurrentEnvironmentAsync();
            StatusMessage = ActiveProjectTab.StatusMessage;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
            StatusMessage = "保存环境失败，请检查环境名称和变量配置后重试。";
        }

        NotifyShellState();
    }

    [RelayCommand]
    private async Task SaveAndCloseEnvironmentManagerAsync()
    {
        await SaveEnvironmentManagerAsync();
        if (HasActiveProjectTab)
        {
            CloseEnvironmentManager();
        }
    }
}
