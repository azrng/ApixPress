using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task SendRequestAsync()
    {
        if (ActiveProjectTab is null)
        {
            StatusMessage = "请先打开一个项目标签页。";
            return;
        }

        await ActiveProjectTab.SendQuickRequestAsync();
        StatusMessage = ActiveProjectTab.StatusMessage;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task SaveCaseAsync()
    {
        if (ActiveProjectTab is null)
        {
            StatusMessage = "请先打开一个项目标签页。";
            return;
        }

        await ActiveProjectTab.SaveCurrentEditorAsync();
        StatusMessage = ActiveProjectTab.StatusMessage;
        NotifyShellState();
    }

    [RelayCommand]
    private void LoadSavedRequest(ExplorerItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.HasChildren && !item.CanLoad)
        {
            item.IsExpanded = !item.IsExpanded;
            NotifyShellState();
            return;
        }

        if (ActiveProjectTab is null)
        {
            return;
        }

        ActiveProjectTab.Catalog.LoadWorkspaceItem(item);
        StatusMessage = ActiveProjectTab.StatusMessage;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task DeleteSavedRequestAsync(ExplorerItemViewModel? item)
    {
        if (ActiveProjectTab is null || item is null)
        {
            return;
        }

        await ActiveProjectTab.Catalog.DeleteWorkspaceItemAsync(item);
        StatusMessage = ActiveProjectTab.StatusMessage;
        NotifyShellState();
    }

    [RelayCommand]
    private void LoadHistoryItem(RequestHistoryItemViewModel? item)
    {
        if (ActiveProjectTab is null || item is null)
        {
            return;
        }

        ActiveProjectTab.LoadHistoryRequest(item);
        StatusMessage = ActiveProjectTab.StatusMessage;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task SaveHistoryAsCaseAsync(RequestHistoryItemViewModel? item)
    {
        if (ActiveProjectTab is null || item is null)
        {
            return;
        }

        await ActiveProjectTab.SaveHistoryAsQuickRequestAsync(item);
        StatusMessage = ActiveProjectTab.StatusMessage;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        if (ActiveProjectTab is null)
        {
            return;
        }

        await ActiveProjectTab.HistoryPanel.ClearHistoryAsync();
        ActiveProjectTab.StatusMessage = "当前项目的请求历史已清空。";
        StatusMessage = ActiveProjectTab.StatusMessage;
        NotifyShellState();
    }

    public void UpdateWindowState(WindowState state)
    {
        IsWindowMaximized = state == WindowState.Maximized;
        NotifyShellState();
    }
}
