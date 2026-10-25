using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using ApixPress.App.Helpers;
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
    private readonly ProjectHttpCaseSaveViewModel _httpCaseSave;
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
        ProjectHttpCaseSaveViewModel httpCaseSave,
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
        _httpCaseSave = httpCaseSave;
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
        _httpCaseSave.Dismiss();
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

        var snapshot = detail.RequestSnapshot;
        // 相对路径请求依赖环境 BaseUrl 拼接，快捷请求又强制完整地址；改开 HTTP 接口标签承载，
        // 并回切记录时的环境。旧快照未记录环境（EnvironmentId 为空）时保持当前环境不动。
        var openAsHttpInterface = !ProjectRequestWorkflowViewModel.HasAbsoluteHttpUrl(snapshot.Url);
        if (openAsHttpInterface)
        {
            RestoreRecordedEnvironment(snapshot.EnvironmentId);
        }

        var targetTab = openAsHttpInterface
            ? _workspace.ReuseActiveLandingOrCreateWorkspace()
            : _hostContext.GetActiveWorkspaceTab()?.IsLandingTab == true
                ? _hostContext.GetActiveWorkspaceTab()
                : _workspace.FindFirstQuickRequestTab() ?? _workspace.CreateWorkspaceTab(activate: false);

        targetTab ??= _workspace.CreateWorkspaceTab(activate: false);
        if (openAsHttpInterface)
        {
            targetTab.ConfigureAsHttpInterface();
        }
        else
        {
            targetTab.ConfigureAsQuickRequest();
        }

        targetTab.ApplySnapshot(snapshot);
        if (detail.ResponseSnapshot is not null)
        {
            targetTab.ResponseSection.ApplyResult(Azrng.Core.Results.ResultModel<ResponseSnapshotDto>.Success(detail.ResponseSnapshot), snapshot);
        }

        // ActivateWorkspaceTab 会切回接口管理分区；载入后直接进入可编辑的请求工作区。
        _workspace.ActivateWorkspaceTab(targetTab);
        _shell.SelectInterfaceManagementSection();
        _hostContext.Messenger.Send(new StatusMessageRequest($"已加载历史请求：{item.Method} {item.Url}"));
        _hostContext.Messenger.Send(new WorkspaceStateChangedMessage(
            WorkspaceStateChangeFlags.EditorState | WorkspaceStateChangeFlags.ShellState));
    }

    /// <summary>把当前环境切回历史记录发送时的环境；与环境下拉选择同路径，赋值即触发激活与状态提示。</summary>
    private void RestoreRecordedEnvironment(string environmentId)
    {
        if (string.IsNullOrWhiteSpace(environmentId))
        {
            return;
        }

        var recorded = _environmentPanel.Environments.FirstOrDefault(item =>
            string.Equals(item.Id, environmentId, StringComparison.OrdinalIgnoreCase));
        if (recorded is null || ReferenceEquals(recorded, _environmentPanel.SelectedEnvironment))
        {
            return;
        }

        _environmentPanel.SelectedEnvironment = recorded;
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

        if (newValue is null || !newValue.IsHttpInterfaceTab)
        {
            _httpCaseSave.Dismiss();
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
        await Task.WhenAll(
            PerfLog.MeasureAsync("开项目·环境加载",
                () => _environmentPanel.LoadProjectAsync(_projectId, preferredEnvironmentId)),
            PerfLog.MeasureAsync("开项目·用例加载", () => _useCasesPanel.LoadCasesAsync()),
            PerfLog.MeasureAsync("开项目·接口树初始化", () => _interfaceRoot.InitializeAsync()),
            _import.LoadExportSettingsAsync());
        _workspace.EnsureLandingWorkspaceTab();
        _hostContext.Messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
    }

    private void NotifyWorkspaceEditorState()
    {
        _hostContext.Messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.EditorState | WorkspaceStateChangeFlags.ShellState));
        _editor.NotifyStateChanged();
    }
}
