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

        await ActiveProjectTab.Workflow.SendRequestAsync();
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

        await ActiveProjectTab.Workflow.SaveCurrentEditorAsync();
        StatusMessage = ActiveProjectTab.StatusMessage;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task SaveHttpCaseAsync()
    {
        if (ActiveProjectTab is null)
        {
            StatusMessage = "请先打开一个项目标签页。";
            return;
        }

        await ActiveProjectTab.Workflow.SaveHttpCaseAsync();
        StatusMessage = ActiveProjectTab.StatusMessage;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task LoadSavedRequest(ExplorerItemViewModel? item)
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

        await ActiveProjectTab.Catalog.LoadWorkspaceItem(item);
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
    private async Task LoadHistoryItemAsync(RequestHistoryItemViewModel? item)
    {
        if (ActiveProjectTab is null || item is null)
        {
            return;
        }

        await ActiveProjectTab.LoadHistoryRequestAsync(item);
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

        await ActiveProjectTab.Workflow.SaveHistoryAsQuickRequestAsync(item);
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
