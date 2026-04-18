using CommunityToolkit.Mvvm.Input;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
    [RelayCommand]
    private void ShowHttpDebugEditorMode()
    {
        if (ActiveWorkspaceTab is null || !ActiveWorkspaceTab.IsHttpInterfaceTab)
        {
            return;
        }

        ActiveWorkspaceTab.HttpEditorViewIndex = 0;
        NotifyWorkspaceEditorState();
    }

    [RelayCommand]
    private void ShowHttpDesignEditorMode()
    {
        if (ActiveWorkspaceTab is null || !ActiveWorkspaceTab.IsHttpInterfaceTab)
        {
            return;
        }

        ActiveWorkspaceTab.HttpEditorViewIndex = 1;
        NotifyWorkspaceEditorState();
    }

    [RelayCommand]
    private void ShowHttpDocumentPreviewMode()
    {
        if (ActiveWorkspaceTab is null || !ActiveWorkspaceTab.IsHttpInterfaceTab)
        {
            return;
        }

        ActiveWorkspaceTab.HttpEditorViewIndex = 2;
        NotifyWorkspaceEditorState();
    }

    [RelayCommand]
    private void ShowInterfaceManagement()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        EnsureLandingWorkspaceTab();
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

    [RelayCommand]
    private void ShowProjectSettings()
    {
        SelectedWorkspaceSection = WorkspaceSections.ProjectSettings;
        SelectedProjectSettingsSection = ProjectSettingsSections.Overview;
        IsProjectImportDialogOpen = false;
        ClearPendingImportConfirmation();
        StatusMessage = ShowProjectSettingsImportDataSection
            ? ProjectSettingsTexts.ImportDescription
            : ProjectSettingsTexts.OverviewDescription;
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowProjectOverviewSettings()
    {
        SelectedWorkspaceSection = WorkspaceSections.ProjectSettings;
        SelectedProjectSettingsSection = ProjectSettingsSections.Overview;
        IsProjectImportDialogOpen = false;
        ClearPendingImportConfirmation();
        StatusMessage = ProjectSettingsTexts.OverviewDescription;
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowProjectImportDataSettings()
    {
        SelectedWorkspaceSection = WorkspaceSections.ProjectSettings;
        SelectedProjectSettingsSection = ProjectSettingsSections.ImportData;
        IsProjectImportDialogOpen = false;
        ClearPendingImportConfirmation();
        StatusMessage = ProjectSettingsTexts.ImportDescription;
        NotifyShellState();
    }
}
