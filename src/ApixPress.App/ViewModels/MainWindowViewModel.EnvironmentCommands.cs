using CommunityToolkit.Mvvm.Input;
using Ursa.Controls;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel
{
    /// <summary>打开环境管理弹窗；关闭由弹窗内容命令或 chrome 关闭钮经 RequestClose 完成。</summary>
    [RelayCommand]
    private async Task OpenEnvironmentManagerAsync()
    {
        if (ActiveProjectTab is null)
        {
            StatusMessage = "请先打开一个项目标签页。";
            return;
        }

        StatusMessage = $"正在管理项目 {ActiveProjectTab.Project.Name} 的环境。";
        NotifyShellState();
        await _dialogService.ShowDialogAsync<Views.Controls.EnvironmentManagerDialogView>(this, "环境变量");
        StatusMessage = ActiveProjectTab?.StatusMessage ?? BrowserStatusText;
        NotifyShellState();
    }

    [RelayCommand]
    private void CloseEnvironmentManager()
    {
        RequestClose?.Invoke(this, DialogResult.None);
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
