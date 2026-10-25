using System.Collections.ObjectModel;
using System.ComponentModel;
using ApixPress.App.Messages;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;
using CommunityToolkit.Mvvm.Messaging;

namespace ApixPress.App.ViewModels;

internal sealed class ProjectTabComposition : DisposableObject
{
    private sealed class Builder
    {
        private readonly ProjectWorkspaceItemViewModel _project;
        private readonly RequestWorkspaceTabViewModel _fallbackWorkspaceTab;
        private readonly IRequestExecutionService _requestExecutionService;
        private readonly IRequestCaseService _requestCaseService;
        private readonly IRequestHistoryService _requestHistoryService;
        private readonly ISystemDataService _systemDataService;
        private readonly IProjectWorkspaceService _projectWorkspaceService;
        private readonly IEnvironmentVariableService _environmentVariableService;
        private readonly IApiWorkspaceService _apiWorkspaceService;
        private readonly IFilePickerService _filePickerService;
        private readonly IAppNotificationService _appNotificationService;
        private readonly IProjectDataExportService _projectDataExportService;
        private readonly IProjectHttpSettingsService _projectHttpSettingsService;
        private readonly IClipboardService _clipboardService;
        private readonly Func<string, Task> _handleProjectDeletedAsync;
        private readonly ProjectTabHostContext _hostContext;

        private ProjectImportViewModel? _importViewModel;
        private ProjectQuickRequestSaveViewModel? _quickRequestSaveViewModel;
        private ProjectHttpCaseSaveViewModel? _httpCaseSaveViewModel;
        private ProjectRequestWorkflowViewModel? _workflowViewModel;
        private ProjectWorkspaceShellViewModel? _shellViewModel;
        private Func<Task> _ensureRequestHistoryLoadedAsync = static () => Task.CompletedTask;

        private IMessenger Messenger => _hostContext.Messenger;

        public Builder(
            ProjectWorkspaceItemViewModel project,
            RequestWorkspaceTabViewModel fallbackWorkspaceTab,
            IRequestExecutionService requestExecutionService,
            IRequestCaseService requestCaseService,
            IRequestHistoryService requestHistoryService,
            ISystemDataService systemDataService,
            IProjectWorkspaceService projectWorkspaceService,
            IEnvironmentVariableService environmentVariableService,
            IApiWorkspaceService apiWorkspaceService,
            IFilePickerService filePickerService,
            IAppNotificationService appNotificationService,
            IProjectDataExportService projectDataExportService,
            IProjectHttpSettingsService projectHttpSettingsService,
            IClipboardService clipboardService,
            Func<string, Task> handleProjectDeletedAsync,
            ProjectTabHostContext hostContext)
        {
            _project = project;
            _fallbackWorkspaceTab = fallbackWorkspaceTab;
            _requestExecutionService = requestExecutionService;
            _requestCaseService = requestCaseService;
            _requestHistoryService = requestHistoryService;
            _systemDataService = systemDataService;
            _projectWorkspaceService = projectWorkspaceService;
            _environmentVariableService = environmentVariableService;
            _apiWorkspaceService = apiWorkspaceService;
            _filePickerService = filePickerService;
            _appNotificationService = appNotificationService;
            _projectDataExportService = projectDataExportService;
            _projectHttpSettingsService = projectHttpSettingsService;
            _clipboardService = clipboardService;
            _handleProjectDeletedAsync = handleProjectDeletedAsync;
            _hostContext = hostContext;
        }

        public ProjectTabComposition Build()
        {
            var environmentPanel = new EnvironmentPanelViewModel(_environmentVariableService, Messenger);
            var historyPanel = new RequestHistoryPanelViewModel(_requestHistoryService);
            _ensureRequestHistoryLoadedAsync = historyPanel.EnsureHistoryLoadedAsync;
            var workspace = CreateWorkspace();
            var useCasesPanel = new UseCasesPanelViewModel(_requestCaseService, workspace, _hostContext.DialogService, Messenger);
            var workspaceContext = CreateWorkspaceContext(workspace, environmentPanel, historyPanel);
            var shell = CreateShell(workspaceContext);
            var editor = new ProjectRequestEditorViewModel(workspaceContext, _hostContext.DialogService, _clipboardService, Messenger);
            ProjectInterfaceRootWorkspaceViewModel? interfaceRoot = null;
            var catalog = CreateCatalog(
                useCasesPanel,
                workspace,
                async () =>
                {
                    Messenger.Send(new StatusMessageRequest("正在打开接口根配置..."));
                    workspace.EnsureInterfaceRootWorkspaceTab();
                    Messenger.Send(new NavigationRequestMessage(NavigationTarget.InterfaceRootWorkspace));
                    _shellViewModel?.SelectInterfaceManagementSection();
                    if (interfaceRoot is not null)
                    {
                        await interfaceRoot.InitializeAsync();
                    }

                    _shellViewModel?.NotifyWorkspaceStateChanged();
                    Messenger.Send(new StatusMessageRequest("接口根配置已打开。"));
                    Messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
                });
            useCasesPanel.SetRequestCaseLoader(catalog.LoadSavedRequestAsync);
            interfaceRoot = CreateInterfaceRoot(useCasesPanel, catalog);
            var import = CreateImport(catalog, environmentPanel);
            var workflow = CreateWorkflow(workspace, historyPanel, environmentPanel, catalog, interfaceRoot, workspaceContext);
            var quickRequestSave = CreateQuickRequestSave(workspaceContext);
            var httpCaseSave = CreateHttpCaseSave(workspaceContext);
            var summary = CreateSummary(environmentPanel, useCasesPanel, historyPanel, import);
            var lifecycle = CreateLifecycle(useCasesPanel, environmentPanel, historyPanel, import, workspace, quickRequestSave, httpCaseSave, shell, editor, interfaceRoot);
            var settings = CreateSettings(lifecycle.ReloadAfterProjectDataClearedAsync);

            return new ProjectTabComposition(
                _project,
                environmentPanel,
                useCasesPanel,
                historyPanel,
                workspace,
                shell,
                editor,
                settings,
                catalog,
                interfaceRoot,
                import,
                workflow,
                quickRequestSave,
                httpCaseSave,
                summary,
                lifecycle,
                _hostContext);
        }

        private ProjectWorkspaceTabsViewModel CreateWorkspace()
        {
            return new ProjectWorkspaceTabsViewModel(
                () =>
                {
                    _shellViewModel?.SelectInterfaceManagementSection();
                },
                Messenger,
                _filePickerService);
        }

        private ProjectTabWorkspaceContext CreateWorkspaceContext(
            ProjectWorkspaceTabsViewModel workspace,
            EnvironmentPanelViewModel environmentPanel,
            RequestHistoryPanelViewModel historyPanel)
        {
            return new ProjectTabWorkspaceContext
            {
                GetActiveWorkspaceTab = _hostContext.GetActiveWorkspaceTab,
                GetFallbackWorkspaceTab = () => _fallbackWorkspaceTab,
                GetCurrentBaseUrl = () => environmentPanel.SelectedEnvironment?.BaseUrl ?? string.Empty,
                GetActiveVariables = () => environmentPanel.SelectedEnvironment is { } selected
                    ? selected.Variables
                        .Where(item => item.IsEnabled && !string.IsNullOrWhiteSpace(item.Key))
                        .GroupBy(item => item.Key.Trim(), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase)
                    : [],
                IsInterfaceRootWorkspaceActive = _hostContext.IsInterfaceRootWorkspaceActive,
                HasHistory = () => historyPanel.HistoryItems.Count > 0
            };
        }

        private ProjectWorkspaceShellViewModel CreateShell(ProjectTabWorkspaceContext workspaceContext)
        {
            var shell = new ProjectWorkspaceShellViewModel(
                workspaceContext,
                _hostContext,
                () => _ensureRequestHistoryLoadedAsync());
            _shellViewModel = shell;
            return shell;
        }

        private ProjectSettingsShellViewModel CreateSettings(Func<Task> reloadAfterProjectDataClearedAsync)
        {
            return new ProjectSettingsShellViewModel(
                () => _shellViewModel?.ShowProjectSettingsSection(),
                () => _importViewModel?.DismissDialog(),
                () => _shellViewModel?.IsProjectSettingsSection ?? false,
                _project.Id,
                () => _project.Name,
                () => _project.Description,
                () => _importViewModel?.EnsureImportedDocumentsLoadedAsync() ?? Task.CompletedTask,
                reloadAfterProjectDataClearedAsync,
                _handleProjectDeletedAsync,
                _systemDataService,
                _projectWorkspaceService,
                _clipboardService,
                _hostContext.DialogService,
                Messenger,
                (name, description) =>
                {
                    // 名称/简介弹窗保存成功后同步设置页行展示（首页瓦片/标题栏由 ShellState 广播刷新）
                    _project.Name = name;
                    _project.Description = description;
                });
        }

        private ProjectWorkspaceCatalogViewModel CreateCatalog(
            UseCasesPanelViewModel useCasesPanel,
            ProjectWorkspaceTabsViewModel workspace,
            Func<Task> openInterfaceRootWorkspaceAsync)
        {
            return new ProjectWorkspaceCatalogViewModel(
                _project.Id,
                _requestCaseService,
                _apiWorkspaceService,
                useCasesPanel,
                workspace,
                openInterfaceRootWorkspaceAsync,
                () => _shellViewModel?.SelectInterfaceManagementSection(),
                Messenger,
                () => _importViewModel?.LoadImportedDocumentsAsync(manageBusyState: false) ?? Task.CompletedTask,
                _hostContext.DialogService);
        }

        private ProjectInterfaceRootWorkspaceViewModel CreateInterfaceRoot(
            UseCasesPanelViewModel useCasesPanel,
            ProjectWorkspaceCatalogViewModel catalog)
        {
            return new ProjectInterfaceRootWorkspaceViewModel(
                _project.Id,
                useCasesPanel.RequestCases,
                _projectHttpSettingsService,
                catalog.OpenHttpInterfaceAsync,
                Messenger);
        }

        private ProjectImportViewModel CreateImport(ProjectWorkspaceCatalogViewModel catalog, EnvironmentPanelViewModel environmentPanel)
        {
            var import = new ProjectImportViewModel(
                _project.Id,
                _apiWorkspaceService,
                _filePickerService,
                _appNotificationService,
                _projectDataExportService,
                () => _project,
                catalog.SyncImportedInterfacesAsync,
                environmentPanel.ApplyImportedBaseUrlAsync,
                Messenger,
                _hostContext.DialogService);
            _importViewModel = import;
            return import;
        }

        private ProjectRequestWorkflowViewModel CreateWorkflow(
            ProjectWorkspaceTabsViewModel workspace,
            RequestHistoryPanelViewModel historyPanel,
            EnvironmentPanelViewModel environmentPanel,
            ProjectWorkspaceCatalogViewModel catalog,
            ProjectInterfaceRootWorkspaceViewModel interfaceRoot,
            ProjectTabWorkspaceContext workspaceContext)
        {
            var workflow = new ProjectRequestWorkflowViewModel(
                _project.Id,
                _requestExecutionService,
                _requestCaseService,
                _requestHistoryService,
                workspace,
                historyPanel,
                environmentPanel,
                catalog,
                interfaceRoot,
                workspaceContext,
                workspaceTab =>
                {
                    _quickRequestSaveViewModel?.OpenDialogFor(workspaceTab);
                },
                _hostContext);
            _workflowViewModel = workflow;
            return workflow;
        }

        private ProjectQuickRequestSaveViewModel CreateQuickRequestSave(ProjectTabWorkspaceContext workspaceContext)
        {
            var quickRequestSave = new ProjectQuickRequestSaveViewModel(
                workspaceContext,
                (workspaceTab, requestNameOverride, saveAsNew) => _workflowViewModel?.SaveQuickRequestAsync(workspaceTab, requestNameOverride, saveAsNew) ?? Task.FromResult(false),
                _hostContext);
            _quickRequestSaveViewModel = quickRequestSave;
            return quickRequestSave;
        }

        private ProjectHttpCaseSaveViewModel CreateHttpCaseSave(ProjectTabWorkspaceContext workspaceContext)
        {
            var httpCaseSave = new ProjectHttpCaseSaveViewModel(
                workspaceContext,
                (workspaceTab, caseName) => _workflowViewModel?.SaveHttpCaseNamedAsync(workspaceTab, caseName) ?? Task.FromResult(false),
                _hostContext);
            _httpCaseSaveViewModel = httpCaseSave;
            return httpCaseSave;
        }

        private ProjectTabSummaryViewModel CreateSummary(
            EnvironmentPanelViewModel environmentPanel,
            UseCasesPanelViewModel useCasesPanel,
            RequestHistoryPanelViewModel historyPanel,
            ProjectImportViewModel import)
        {
            return new ProjectTabSummaryViewModel(
                () => _project,
                () => environmentPanel.SelectedEnvironment,
                _hostContext.GetActiveWorkspaceTab,
                () => useCasesPanel.RequestCases,
                () => historyPanel.HistoryItems,
                () => environmentPanel.Environments.Count,
                () => import.ImportedApiDocuments.Count);
        }

        private ProjectTabLifecycleCoordinator CreateLifecycle(
            UseCasesPanelViewModel useCasesPanel,
            EnvironmentPanelViewModel environmentPanel,
            RequestHistoryPanelViewModel historyPanel,
            ProjectImportViewModel import,
            ProjectWorkspaceTabsViewModel workspace,
            ProjectQuickRequestSaveViewModel quickRequestSave,
            ProjectHttpCaseSaveViewModel httpCaseSave,
            ProjectWorkspaceShellViewModel shell,
            ProjectRequestEditorViewModel editor,
            ProjectInterfaceRootWorkspaceViewModel interfaceRoot)
        {
            return new ProjectTabLifecycleCoordinator(
                _project.Id,
                () => _project.Name,
                useCasesPanel,
                environmentPanel,
                historyPanel,
                import,
                workspace,
                quickRequestSave,
                httpCaseSave,
                shell,
                editor,
                interfaceRoot,
                _hostContext);
        }
    }

    private readonly ProjectWorkspaceItemViewModel _project;
    private readonly ProjectTabHostContext _hostContext;
    private bool _isAttached;
    private Action? _onStateChanged;
    private Action? _onEditorStateChanged;

    private ProjectTabComposition(
        ProjectWorkspaceItemViewModel project,
        EnvironmentPanelViewModel environmentPanel,
        UseCasesPanelViewModel useCasesPanel,
        RequestHistoryPanelViewModel historyPanel,
        ProjectWorkspaceTabsViewModel workspace,
        ProjectWorkspaceShellViewModel shell,
        ProjectRequestEditorViewModel editor,
        ProjectSettingsShellViewModel settings,
        ProjectWorkspaceCatalogViewModel catalog,
        ProjectInterfaceRootWorkspaceViewModel interfaceRoot,
        ProjectImportViewModel import,
        ProjectRequestWorkflowViewModel workflow,
        ProjectQuickRequestSaveViewModel quickRequestSave,
        ProjectHttpCaseSaveViewModel httpCaseSave,
        ProjectTabSummaryViewModel summary,
        ProjectTabLifecycleCoordinator lifecycle,
        ProjectTabHostContext hostContext)
    {
        _project = project;
        EnvironmentPanel = environmentPanel;
        UseCasesPanel = useCasesPanel;
        HistoryPanel = historyPanel;
        Workspace = workspace;
        Shell = shell;
        Editor = editor;
        Settings = settings;
        Catalog = catalog;
        InterfaceRoot = interfaceRoot;
        Import = import;
        Workflow = workflow;
        QuickRequestSave = quickRequestSave;
        HttpCaseSave = httpCaseSave;
        Summary = summary;
        Lifecycle = lifecycle;
        _hostContext = hostContext;
    }

    public EnvironmentPanelViewModel EnvironmentPanel { get; }
    public UseCasesPanelViewModel UseCasesPanel { get; }
    public RequestHistoryPanelViewModel HistoryPanel { get; }
    public ProjectWorkspaceTabsViewModel Workspace { get; }
    public ProjectWorkspaceShellViewModel Shell { get; }
    public ProjectRequestEditorViewModel Editor { get; }
    public ProjectSettingsShellViewModel Settings { get; }
    public ProjectWorkspaceCatalogViewModel Catalog { get; }
    public ProjectInterfaceRootWorkspaceViewModel InterfaceRoot { get; }
    public ProjectImportViewModel Import { get; }
    public ProjectRequestWorkflowViewModel Workflow { get; }
    public ProjectQuickRequestSaveViewModel QuickRequestSave { get; }
    public ProjectHttpCaseSaveViewModel HttpCaseSave { get; }
    public ProjectTabSummaryViewModel Summary { get; }
    public ProjectTabLifecycleCoordinator Lifecycle { get; }

    public static ProjectTabComposition Create(
        ProjectWorkspaceItemViewModel project,
        RequestWorkspaceTabViewModel fallbackWorkspaceTab,
        IRequestExecutionService requestExecutionService,
        IRequestCaseService requestCaseService,
        IRequestHistoryService requestHistoryService,
        ISystemDataService systemDataService,
        IProjectWorkspaceService projectWorkspaceService,
        IEnvironmentVariableService environmentVariableService,
        IApiWorkspaceService apiWorkspaceService,
        IFilePickerService filePickerService,
        IAppNotificationService appNotificationService,
        IProjectDataExportService projectDataExportService,
        IProjectHttpSettingsService projectHttpSettingsService,
        IClipboardService clipboardService,
        Func<string, Task> handleProjectDeletedAsync,
        ProjectTabHostContext hostContext)
    {
        return new Builder(
            project,
            fallbackWorkspaceTab,
            requestExecutionService,
            requestCaseService,
            requestHistoryService,
            systemDataService,
            projectWorkspaceService,
            environmentVariableService,
            apiWorkspaceService,
            filePickerService,
            appNotificationService,
            projectDataExportService,
            projectHttpSettingsService,
            clipboardService,
            handleProjectDeletedAsync,
            hostContext)
            .Build();
    }

    public void Attach()
    {
        ThrowIfDisposed();
        if (_isAttached)
        {
            return;
        }

        var messenger = _hostContext.Messenger;
        _onStateChanged = () => messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
        _onEditorStateChanged = () => messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.EditorState));
        _project.PropertyChanged += OnProjectPropertyChanged;
        EnvironmentPanel.SelectedEnvironmentChanged += Lifecycle.OnSelectedEnvironmentChanged;
        EnvironmentPanel.Environments.CollectionChanged += OnCollectionChanged;
        HistoryPanel.HistoryItems.CollectionChanged += OnCollectionChanged;
        Workspace.PropertyChanged += Lifecycle.OnWorkspacePropertyChanged;
        Workspace.StateChanged += _onStateChanged;
        Workspace.EditorStateChanged += _onEditorStateChanged;
        Workspace.ActiveWorkspaceTabChanged += Lifecycle.OnWorkspaceActiveWorkspaceTabChanged;
        Shell.PropertyChanged += OnShellPropertyChanged;
        Settings.PropertyChanged += OnChildPropertyChanged;
        Import.PropertyChanged += OnChildPropertyChanged;
        InterfaceRoot.PropertyChanged += OnChildPropertyChanged;
        QuickRequestSave.PropertyChanged += OnChildPropertyChanged;
        HttpCaseSave.PropertyChanged += OnChildPropertyChanged;
        Shell.AddProjectSettingsNavigation(Settings.OpenWorkspaceCommand);
        Workspace.EnsureLandingWorkspaceTab();
        _isAttached = true;
    }

    public void Detach()
    {
        if (!_isAttached)
        {
            return;
        }

        _project.PropertyChanged -= OnProjectPropertyChanged;
        EnvironmentPanel.SelectedEnvironmentChanged -= Lifecycle.OnSelectedEnvironmentChanged;
        EnvironmentPanel.Environments.CollectionChanged -= OnCollectionChanged;
        HistoryPanel.HistoryItems.CollectionChanged -= OnCollectionChanged;
        Workspace.PropertyChanged -= Lifecycle.OnWorkspacePropertyChanged;
        Workspace.StateChanged -= _onStateChanged;
        Workspace.EditorStateChanged -= _onEditorStateChanged;
        Workspace.ActiveWorkspaceTabChanged -= Lifecycle.OnWorkspaceActiveWorkspaceTabChanged;
        Shell.PropertyChanged -= OnShellPropertyChanged;
        Settings.PropertyChanged -= OnChildPropertyChanged;
        Import.PropertyChanged -= OnChildPropertyChanged;
        InterfaceRoot.PropertyChanged -= OnChildPropertyChanged;
        QuickRequestSave.PropertyChanged -= OnChildPropertyChanged;
        HttpCaseSave.PropertyChanged -= OnChildPropertyChanged;
        _isAttached = false;
    }

    public Task OpenInterfaceRootWorkspaceAsync()
    {
        return Catalog.OpenInterfaceRootCommand.ExecuteAsync(null);
    }

    public void ClearInterfaceRootWorkspace()
    {
        // Handled by ProjectTabViewModel.Receive(WorkspaceStateChangedMessage) with EditorState flag,
        // which sets IsInterfaceRootWorkspaceActive = false directly.
    }

    protected override void DisposeManaged()
    {
        Detach();
        // 子 VM 全量释放：漏掉的 Editor 会让关闭标签后防抖计时器仍回调一次无效重算，
        // 各子 VM 的 CTS 也不再被取消（飞行中的后台格式化跑完才结束）
        Catalog.Dispose();
        InterfaceRoot.Dispose();
        Import.Dispose();
        Workflow.Dispose();
        EnvironmentPanel.Dispose();
        UseCasesPanel.Dispose();
        HistoryPanel.Dispose();
        Workspace.Dispose();
        Editor.Dispose();
        Settings.Dispose();
        Shell.Dispose();
        QuickRequestSave.Dispose();
        HttpCaseSave.Dispose();
        Summary.Dispose();
    }

    private void OnProjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Settings.NotifyProjectChanged();
        _hostContext.Messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
    }

    private void OnCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        _hostContext.Messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
    }

    private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _hostContext.Messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectWorkspaceShellViewModel.SelectedSection)
            or nameof(ProjectWorkspaceShellViewModel.IsProjectSettingsSection))
        {
            Settings.NotifyWorkspaceSectionChanged();
        }

        if (e.PropertyName is nameof(ProjectWorkspaceShellViewModel.ShowInterfaceManagementLanding)
            or nameof(ProjectWorkspaceShellViewModel.ShowRequestEditorWorkspace)
            or nameof(ProjectWorkspaceShellViewModel.CurrentContentMode)
            or nameof(ProjectWorkspaceShellViewModel.IsInterfaceManagementSection)
            or nameof(ProjectWorkspaceShellViewModel.IsRequestHistorySection)
            or nameof(ProjectWorkspaceShellViewModel.IsProjectSettingsSection))
        {
            return;
        }

        _hostContext.Messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
    }
}
