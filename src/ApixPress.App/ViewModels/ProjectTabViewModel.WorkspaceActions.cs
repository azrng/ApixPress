namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await LoadWorkspaceAsync();
    }

    private async Task LoadWorkspaceAsync(string? preferredEnvironmentId = null)
    {
        UseCasesPanel.SetProjectContext(ProjectId);
        HistoryPanel.SetProjectContext(ProjectId);
        await EnvironmentPanel.LoadProjectAsync(ProjectId, preferredEnvironmentId);
        var loadHistoryTask = HistoryPanel.LoadHistoryAsync();
        var loadImportedDocumentsTask = Import.LoadImportedDocumentsAsync(manageBusyState: false);
        await Task.WhenAll(loadHistoryTask, loadImportedDocumentsTask);
        Workspace.EnsureLandingWorkspaceTab();
        NotifyShellState();
    }

    public async Task RefreshAsync()
    {
        await LoadWorkspaceAsync(EnvironmentPanel.SelectedEnvironment?.Id);
        StatusMessage = $"项目 {Project.Name} 已刷新。";
        NotifyShellState();
    }

    public async Task SaveCurrentEnvironmentAsync()
    {
        if (!EnvironmentPanel.HasSelectedEnvironment)
        {
            StatusMessage = "请先选择环境后再保存。";
            NotifyShellState();
            return;
        }

        await EnvironmentPanel.SaveEnvironmentCommand.ExecuteAsync(null);
        StatusMessage = $"环境 {CurrentEnvironmentLabel} 已保存。";
        NotifyShellState();
    }
}
