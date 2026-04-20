using CommunityToolkit.Mvvm.Input;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
    [RelayCommand]
    private void ShowInterfaceManagement()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        Workspace.EnsureLandingWorkspaceTab();
        StatusMessage = ActiveWorkspaceTab?.IsLandingTab == true
            ? "接口管理已就绪，可在中间新建 HTTP 接口或快捷请求。"
            : "接口管理已打开。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowRequestHistory()
    {
        SelectedWorkspaceSection = WorkspaceSections.RequestHistory;
        StatusMessage = HasHistory ? "这里展示当前项目的请求历史。" : "当前项目还没有请求历史。";
        NotifyShellState();
    }
}
