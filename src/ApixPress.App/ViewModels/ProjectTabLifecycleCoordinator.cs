using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using ApixPress.App.Messages;
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
    private readonly ProjectInterfaceRootWorkspaceViewModel _interfaceRoot;
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
        ProjectInterfaceRootWorkspaceViewModel interfaceRoot,
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
        _interfaceRoot = interfaceRoot;
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
        _hostContext.Messenger.Send(new StatusMessageRequest($"项目 {_getProjectName()} 已刷新。"));
        _hostContext.Messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
    }

    public async Task ReloadAfterProjectDataClearedAsync()
    {
        _quickRequestSave.Dismiss();
        _import.DismissDialog();
        _import.ResetImportedDocuments();
        _workspace.ResetToLanding();
        await LoadWorkspaceAsync();
        _shell.ShowProjectSettingsSection();
        _hostContext.Messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.BindingsChanged));
        _hostContext.Messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
    }

    public async Task SaveCurrentEnvironmentAsync(string currentEnvironmentLabel)
    {
        if (!_environmentPanel.HasSelectedEnvironment)
        {
            _hostContext.Messenger.Send(new StatusMessageRequest("请先选择环境后再保存。"));
            _hostContext.Messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
            return;
        }

        await _environmentPanel.SaveEnvironmentCommand.ExecuteAsync(null);
        _hostContext.Messenger.Send(new StatusMessageRequest($"环境 {currentEnvironmentLabel} 已保存。"));
        _hostContext.Messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
    }

    public async Task LoadHistoryRequestAsync(RequestHistoryItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var detail = await _historyPanel.EnsureHistoryDetailLoadedAsync(item);
        if (detail is null)
        {
            _hostContext.Messenger.Send(new StatusMessageRequest("载入历史请求失败，未找到对应记录。"));
            _hostContext.Messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
            return;
        }

        var targetTab = _hostContext.GetActiveWorkspaceTab()?.IsLandingTab == true
            ? _hostContext.GetActiveWorkspaceTab()
            : _workspace.FindFirstQuickRequestTab() ?? _workspace.CreateWorkspaceTab(activate: false);

        targetTab ??= _workspace.CreateWorkspaceTab(activate: false);
        targetTab.ConfigureAsQuickRequest();
        targetTab.ApplySnapshot(detail.RequestSnapshot);
        if (detail.ResponseSnapshot is not null)
        {
            targetTab.ResponseSection.ApplyResult(Azrng.Core.Results.ResultModel<ResponseSnapshotDto>.Success(detail.ResponseSnapshot), detail.RequestSnapshot);
        }

        _workspace.ActivateWorkspaceTab(targetTab);
        _shell.SelectRequestHistorySection();
        _hostContext.Messenger.Send(new StatusMessageRequest($"已加载历史请求：{item.Method} {item.Url}"));
        _hostContext.Messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
    }

    public void OnSelectedEnvironmentChanged(ProjectEnvironmentItemViewModel? environment)
    {
        _hostContext.Messenger.Send(new StatusMessageRequest(environment is null
            ? "当前项目尚未配置环境。"
            : $"当前环境已切换为：{environment.Name}"));
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
            _hostContext.Messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ActiveTabChanged));
            _shell.NotifyWorkspaceStateChanged();
        }
        else if (e.PropertyName == nameof(ProjectWorkspaceTabsViewModel.IsWorkspaceTabMenuOpen))
        {
            _hostContext.Messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.TabMenuChanged));
        }
    }

    private async Task LoadWorkspaceAsync(string? preferredEnvironmentId = null)
    {
        _useCasesPanel.SetProjectContext(_projectId);
        _historyPanel.SetProjectContext(_projectId);
        await _environmentPanel.LoadProjectAsync(_projectId, preferredEnvironmentId);
        await _useCasesPanel.LoadCasesAsync();
        await _interfaceRoot.InitializeAsync();
        _workspace.EnsureLandingWorkspaceTab();
        _hostContext.Messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
    }

    private void NotifyWorkspaceEditorState()
    {
        _hostContext.Messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.EditorState | WorkspaceStateChangeFlags.ShellState));
        _editor.NotifyStateChanged();
    }
}
