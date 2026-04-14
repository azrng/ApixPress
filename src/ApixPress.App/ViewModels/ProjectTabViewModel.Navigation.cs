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
            ? "这里可以导入 Swagger 文档。"
            : "这里可以查看当前项目的基本设置。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowProjectOverviewSettings()
    {
        SelectedWorkspaceSection = WorkspaceSections.ProjectSettings;
        SelectedProjectSettingsSection = ProjectSettingsSections.Overview;
        IsProjectImportDialogOpen = false;
        ClearPendingImportConfirmation();
        StatusMessage = "这里可以查看项目名称、项目 ID 和简介。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowProjectImportDataSettings()
    {
        SelectedWorkspaceSection = WorkspaceSections.ProjectSettings;
        SelectedProjectSettingsSection = ProjectSettingsSections.ImportData;
        IsProjectImportDialogOpen = false;
        ClearPendingImportConfirmation();
        StatusMessage = "这里可以导入 Swagger 文档。";
        NotifyShellState();
    }

    [RelayCommand]
    private void OpenProjectImportDialog()
    {
        SelectedWorkspaceSection = WorkspaceSections.ProjectSettings;
        SelectedProjectSettingsSection = ProjectSettingsSections.ImportData;
        SelectedImportDataMode = ImportDataModes.File;
        ClearPendingImportConfirmation();
        IsProjectImportDialogOpen = true;
        StatusMessage = "请选择 OpenAPI / Swagger 导入方式。";
        NotifyShellState();
    }

    [RelayCommand]
    private void CloseProjectImportDialog()
    {
        IsProjectImportDialogOpen = false;
        ClearPendingImportConfirmation();
        StatusMessage = "已返回导入数据页面。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowImportFileMode()
    {
        SelectedImportDataMode = ImportDataModes.File;
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowImportUrlMode()
    {
        SelectedImportDataMode = ImportDataModes.Url;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task PickSwaggerImportFileAsync()
    {
        if (IsImportDataBusy)
        {
            return;
        }

        var filePath = await _filePickerService.PickSwaggerJsonFileAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            SetImportDataStatus("未选择文件，当前保持原有导入配置。", ImportStatusStates.Info);
            NotifyShellState();
            return;
        }

        SelectedImportFilePath = filePath;
        SetImportDataStatus($"已选择 Swagger 文件：{Path.GetFileName(filePath)}", ImportStatusStates.Info);
        StatusMessage = $"已选择 Swagger 文件：{Path.GetFileName(filePath)}";
        NotifyShellState();
    }

    [RelayCommand]
    private async Task ImportSwaggerFileAsync()
    {
        if (!HasSelectedImportFile)
        {
            SetImportDataStatus("请先选择要导入的 Swagger/OpenAPI JSON 文件。", ImportStatusStates.Error);
            StatusMessage = "请先选择要导入的 Swagger 文件。";
            NotifyShellState();
            return;
        }

        await ImportSwaggerAsync(
            cancellationToken => _apiWorkspaceService.PreviewImportFromFileAsync(ProjectId, SelectedImportFilePath.Trim(), cancellationToken),
            cancellationToken => _apiWorkspaceService.ImportFromFileAsync(ProjectId, SelectedImportFilePath.Trim(), cancellationToken),
            document => $"Swagger 文件导入成功：{document.Name}");
    }

    [RelayCommand]
    private async Task ImportSwaggerUrlAsync()
    {
        var importTargetUrl = ImportUrl.Trim();
        if (string.IsNullOrWhiteSpace(importTargetUrl))
        {
            SetImportDataStatus("请输入 Swagger/OpenAPI 文档 URL。", ImportStatusStates.Error);
            StatusMessage = "请输入 Swagger 文档 URL。";
            NotifyShellState();
            return;
        }

        await ImportSwaggerAsync(
            cancellationToken => _apiWorkspaceService.PreviewImportFromUrlAsync(ProjectId, importTargetUrl, cancellationToken),
            cancellationToken => _apiWorkspaceService.ImportFromUrlAsync(ProjectId, importTargetUrl, cancellationToken),
            document => $"Swagger URL 导入成功：{document.Name}");
    }

    [RelayCommand]
    private async Task RefreshImportedApiDocumentsAsync()
    {
        await LoadImportedDocumentsAsync();
        StatusMessage = HasImportedApiDocuments
            ? $"已刷新已导入数据，共 {ImportedApiDocuments.Count} 份文档。"
            : "已刷新导入数据，当前项目还没有 Swagger 文档。";
        NotifyShellState();
    }

    [RelayCommand]
    private void OpenQuickRequestWorkspace()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var tab = ReuseActiveLandingOrCreateWorkspace();
        tab.ConfigureAsQuickRequest();
        IsWorkspaceTabMenuOpen = false;
        ActivateWorkspaceTabCore(tab);
        StatusMessage = "快捷请求标签已打开。";
        NotifyShellState();
    }

    [RelayCommand]
    private void OpenHttpInterfaceWorkspace()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var tab = ReuseActiveLandingOrCreateWorkspace();
        tab.ConfigureAsHttpInterface();
        IsWorkspaceTabMenuOpen = false;
        ActivateWorkspaceTabCore(tab);
        StatusMessage = "HTTP 接口标签已打开。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ReturnToInterfaceHome()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var landingTab = FindLandingWorkspaceTab() ?? CreateWorkspaceTab(activate: false);
        landingTab.ConfigureAsLanding();
        landingTab.ShowInTabStrip = true;
        ActivateWorkspaceTabCore(landingTab);
        IsWorkspaceTabMenuOpen = false;
        StatusMessage = "已返回新建页。";
        NotifyShellState();
    }

    [RelayCommand]
    private void CreateWorkspaceTab()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var tab = CreateWorkspaceTab(activate: true, showInTabStrip: true);
        tab.ConfigureAsLanding();
        IsWorkspaceTabMenuOpen = false;
        StatusMessage = "已新建一个工作标签。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ToggleWorkspaceTabMenu()
    {
        IsWorkspaceTabMenuOpen = !IsWorkspaceTabMenuOpen;
    }

    [RelayCommand]
    private void CloseCurrentWorkspaceFromMenu()
    {
        IsWorkspaceTabMenuOpen = false;
        CloseWorkspaceTab(ActiveWorkspaceTab);
    }

    [RelayCommand]
    private void CloseOtherWorkspaceTabs()
    {
        IsWorkspaceTabMenuOpen = false;
        if (ActiveWorkspaceTab is null)
        {
            return;
        }

        var tabsToRemove = WorkspaceTabs
            .Where(item => !ReferenceEquals(item, ActiveWorkspaceTab))
            .ToList();
        foreach (var tab in tabsToRemove)
        {
            DetachWorkspaceTab(tab);
            WorkspaceTabs.Remove(tab);
        }

        if (!WorkspaceTabs.Contains(ActiveWorkspaceTab))
        {
            EnsureLandingWorkspaceTab();
        }
        else
        {
            ActivateWorkspaceTabCore(ActiveWorkspaceTab);
        }

        StatusMessage = tabsToRemove.Count == 0 ? "当前没有其它标签页可关闭。" : "已关闭其它标签页。";
        NotifyShellState();
    }

    [RelayCommand]
    private void CloseAllWorkspaceTabs()
    {
        IsWorkspaceTabMenuOpen = false;
        if (WorkspaceTabs.Count == 0)
        {
            return;
        }

        foreach (var tab in WorkspaceTabs.ToList())
        {
            DetachWorkspaceTab(tab);
        }

        WorkspaceTabs.Clear();
        ActiveWorkspaceTab = null;
        EnsureLandingWorkspaceTab();
        StatusMessage = "已关闭全部标签页。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ActivateWorkspaceTab(RequestWorkspaceTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        IsWorkspaceTabMenuOpen = false;
        ActivateWorkspaceTabCore(tab);
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        StatusMessage = tab.IsLandingTab ? "已切换到新建页。" : $"已切换到标签：{tab.HeaderText}";
        NotifyShellState();
    }

    [RelayCommand]
    private void CloseWorkspaceTab(RequestWorkspaceTabViewModel? tab)
    {
        if (tab is null || !WorkspaceTabs.Contains(tab))
        {
            return;
        }

        IsWorkspaceTabMenuOpen = false;
        var removedIndex = WorkspaceTabs.IndexOf(tab);
        DetachWorkspaceTab(tab);
        WorkspaceTabs.Remove(tab);

        if (WorkspaceTabs.Count == 0)
        {
            EnsureLandingWorkspaceTab();
        }
        else if (ReferenceEquals(ActiveWorkspaceTab, tab))
        {
            var nextIndex = Math.Clamp(removedIndex - 1, 0, WorkspaceTabs.Count - 1);
            ActivateWorkspaceTabCore(WorkspaceTabs[nextIndex]);
        }

        StatusMessage = "工作标签已关闭。";
        NotifyShellState();
    }
}
