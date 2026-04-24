using System.ComponentModel;
using ApixPress.App.Models.DTOs;

namespace ApixPress.App.ViewModels;

internal sealed class ProjectTabLifecycleCoordinator
{
    private readonly string _projectId;
    private readonly Func<string> _getProjectName;
    private readonly UseCasesPanelViewModel _useCasesPanel;
    private readonly EnvironmentPanelViewModel _environmentPanel;
    private readonly RequestHistoryPanelViewModel _historyPanel;
    private readonly ProjectImportViewModel _import;
    private readonly ProjectWorkspaceTabsViewModel _workspace;
    private readonly ProjectQuickRequestSaveViewModel _quickRequestSave;
    private readonly ProjectWorkspaceShellViewModel _shell;
    private readonly ProjectRequestEditorViewModel _editor;
    private readonly ProjectTabHostContext _hostContext;
    private bool _initialized;

    public ProjectTabLifecycleCoordinator(
        string projectId,
        Func<string> getProjectName,
        UseCasesPanelViewModel useCasesPanel,
        EnvironmentPanelViewModel environmentPanel,
        RequestHistoryPanelViewModel historyPanel,
        ProjectImportViewModel import,
        ProjectWorkspaceTabsViewModel workspace,
        ProjectQuickRequestSaveViewModel quickRequestSave,
        ProjectWorkspaceShellViewModel shell,
        ProjectRequestEditorViewModel editor,
        ProjectTabHostContext hostContext)
    {
        _projectId = projectId;
        _getProjectName = getProjectName;
        _useCasesPanel = useCasesPanel;
        _environmentPanel = environmentPanel;
        _historyPanel = historyPanel;
        _import = import;
        _workspace = workspace;
        _quickRequestSave = quickRequestSave;
        _shell = shell;
        _editor = editor;
        _hostContext = hostContext;
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await LoadWorkspaceAsync();
    }

    public async Task RefreshAsync()
    {
        await LoadWorkspaceAsync(_environmentPanel.SelectedEnvironment?.Id);
        _hostContext.SetStatusMessage($"项目 {_getProjectName()} 已刷新。");
        _hostContext.NotifyShellState();
    }

    public async Task SaveCurrentEnvironmentAsync(string currentEnvironmentLabel)
    {
        if (!_environmentPanel.HasSelectedEnvironment)
        {
            _hostContext.SetStatusMessage("请先选择环境后再保存。");
            _hostContext.NotifyShellState();
            return;
        }

        await _environmentPanel.SaveEnvironmentCommand.ExecuteAsync(null);
        _hostContext.SetStatusMessage($"环境 {currentEnvironmentLabel} 已保存。");
        _hostContext.NotifyShellState();
    }

    public void LoadHistoryRequest(RequestHistoryItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var targetTab = _hostContext.GetActiveWorkspaceTab()?.IsLandingTab == true
            ? _hostContext.GetActiveWorkspaceTab()
            : _workspace.FindFirstQuickRequestTab() ?? _workspace.CreateWorkspaceTab(activate: false);

        targetTab ??= _workspace.CreateWorkspaceTab(activate: false);
        targetTab.ConfigureAsQuickRequest();
        targetTab.ApplySnapshot(item.RequestSnapshot);
        if (item.ResponseSnapshot is not null)
        {
            targetTab.ResponseSection.ApplyResult(Azrng.Core.Results.ResultModel<ResponseSnapshotDto>.Success(item.ResponseSnapshot), item.RequestSnapshot);
        }

        _workspace.ActivateWorkspaceTab(targetTab);
        _shell.SelectRequestHistorySection();
        _hostContext.SetStatusMessage($"已加载历史请求：{item.Method} {item.Url}");
        _hostContext.NotifyShellState();
    }

    public void OnSelectedEnvironmentChanged(ProjectEnvironmentItemViewModel? environment)
    {
        _hostContext.SetStatusMessage(environment is null
            ? "当前项目尚未配置环境。"
            : $"当前环境已切换为：{environment.Name}");
        NotifyWorkspaceEditorState();
    }

    public void OnWorkspaceActiveWorkspaceTabChanged(RequestWorkspaceTabViewModel? oldValue, RequestWorkspaceTabViewModel? newValue)
    {
        if (newValue is null || !newValue.IsQuickRequestTab)
        {
            _quickRequestSave.Dismiss();
        }
    }

    public void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjectWorkspaceTabsViewModel.ActiveWorkspaceTab))
        {
            _hostContext.NotifyActiveWorkspaceTabChanged();
            _shell.NotifyWorkspaceStateChanged();
        }
        else if (e.PropertyName == nameof(ProjectWorkspaceTabsViewModel.IsWorkspaceTabMenuOpen))
        {
            _hostContext.NotifyWorkspaceTabMenuChanged();
        }
    }

    private async Task LoadWorkspaceAsync(string? preferredEnvironmentId = null)
    {
        _useCasesPanel.SetProjectContext(_projectId);
        _historyPanel.SetProjectContext(_projectId);
        await _environmentPanel.LoadProjectAsync(_projectId, preferredEnvironmentId);
        await _historyPanel.LoadHistoryAsync();
        _workspace.EnsureLandingWorkspaceTab();
        _hostContext.NotifyShellState();
    }

    private void NotifyWorkspaceEditorState()
    {
        _hostContext.NotifyWorkspaceBindingsChanged();
        _editor.NotifyStateChanged();
        _hostContext.NotifyShellState();
    }
}
