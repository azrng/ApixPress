using System.Collections.ObjectModel;
using System.ComponentModel;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

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
        private readonly IEnvironmentVariableService _environmentVariableService;
        private readonly IApiWorkspaceService _apiWorkspaceService;
        private readonly IFilePickerService _filePickerService;
        private readonly IAppNotificationService _appNotificationService;
        private readonly ProjectTabHostContext _hostContext;

        private ProjectImportViewModel? _importViewModel;
        private ProjectQuickRequestSaveViewModel? _quickRequestSaveViewModel;
        private ProjectRequestWorkflowViewModel? _workflowViewModel;
        private ProjectWorkspaceShellViewModel? _shellViewModel;
        private Func<Task> _ensureRequestHistoryLoadedAsync = static () => Task.CompletedTask;

        public Builder(
            ProjectWorkspaceItemViewModel project,
            RequestWorkspaceTabViewModel fallbackWorkspaceTab,
            IRequestExecutionService requestExecutionService,
            IRequestCaseService requestCaseService,
            IRequestHistoryService requestHistoryService,
            IEnvironmentVariableService environmentVariableService,
            IApiWorkspaceService apiWorkspaceService,
            IFilePickerService filePickerService,
            IAppNotificationService appNotificationService,
            ProjectTabHostContext hostContext)
        {
            _project = project;
            _fallbackWorkspaceTab = fallbackWorkspaceTab;
            _requestExecutionService = requestExecutionService;
            _requestCaseService = requestCaseService;
            _requestHistoryService = requestHistoryService;
            _environmentVariableService = environmentVariableService;
            _apiWorkspaceService = apiWorkspaceService;
            _filePickerService = filePickerService;
            _appNotificationService = appNotificationService;
            _hostContext = hostContext;
        }

        public ProjectTabComposition Build()
        {
            var environmentPanel = new EnvironmentPanelViewModel(_environmentVariableService);
            var useCasesPanel = new UseCasesPanelViewModel(_requestCaseService);
            var historyPanel = new RequestHistoryPanelViewModel(_requestHistoryService);
            _ensureRequestHistoryLoadedAsync = historyPanel.EnsureHistoryLoadedAsync;
            var workspace = CreateWorkspace();
            var workspaceContext = CreateWorkspaceContext(workspace, environmentPanel, historyPanel);
            var shell = CreateShell(workspaceContext);
            var editor = new ProjectRequestEditorViewModel(workspaceContext);
            var settings = CreateSettings();
            var catalog = CreateCatalog(useCasesPanel, workspace);
            var import = CreateImport(catalog);
            var workflow = CreateWorkflow(workspace, historyPanel, environmentPanel, catalog, workspaceContext);
            var quickRequestSave = CreateQuickRequestSave(workspaceContext);
            var summary = CreateSummary(environmentPanel, useCasesPanel, historyPanel, import);
            var lifecycle = CreateLifecycle(useCasesPanel, environmentPanel, historyPanel, import, workspace, quickRequestSave, shell, editor);

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
                import,
                workflow,
                quickRequestSave,
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
                _hostContext.SetStatusMessage);
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
                EnsureLandingWorkspaceTab = workspace.EnsureLandingWorkspaceTab,
                SelectInterfaceManagementSection = () =>
                {
                    _shellViewModel?.SelectInterfaceManagementSection();
                },
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

        private ProjectSettingsShellViewModel CreateSettings()
        {
            return new ProjectSettingsShellViewModel(
                () => _shellViewModel?.ShowProjectSettingsSection(),
                () => _importViewModel?.DismissDialog(),
                () => _shellViewModel?.IsProjectSettingsSection ?? false,
                () => _project.Description,
                () => _importViewModel?.EnsureImportedDocumentsLoadedAsync() ?? Task.CompletedTask,
                _hostContext.SetStatusMessage,
                _hostContext.NotifyShellState);
        }

        private ProjectWorkspaceCatalogViewModel CreateCatalog(
            UseCasesPanelViewModel useCasesPanel,
            ProjectWorkspaceTabsViewModel workspace)
        {
            return new ProjectWorkspaceCatalogViewModel(
                _project.Id,
                _requestCaseService,
                _apiWorkspaceService,
                useCasesPanel,
                workspace,
                () => _shellViewModel?.SelectInterfaceManagementSection(),
                _hostContext.SetStatusMessage,
                _hostContext.NotifyShellState,
                () => _importViewModel?.LoadImportedDocumentsAsync(manageBusyState: false) ?? Task.CompletedTask);
        }

        private ProjectImportViewModel CreateImport(ProjectWorkspaceCatalogViewModel catalog)
        {
            var import = new ProjectImportViewModel(
                _project.Id,
                _apiWorkspaceService,
                _filePickerService,
                _appNotificationService,
                catalog.SyncImportedInterfacesAsync,
                _hostContext.SetStatusMessage);
            _importViewModel = import;
            return import;
        }

        private ProjectRequestWorkflowViewModel CreateWorkflow(
            ProjectWorkspaceTabsViewModel workspace,
            RequestHistoryPanelViewModel historyPanel,
            EnvironmentPanelViewModel environmentPanel,
            ProjectWorkspaceCatalogViewModel catalog,
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
                (workspaceTab, requestNameOverride) => _workflowViewModel?.SaveQuickRequestAsync(workspaceTab, requestNameOverride) ?? Task.FromResult(false),
                _hostContext);
            _quickRequestSaveViewModel = quickRequestSave;
            return quickRequestSave;
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
            ProjectWorkspaceShellViewModel shell,
            ProjectRequestEditorViewModel editor)
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
                shell,
                editor,
                _hostContext);
        }
    }

    private readonly ProjectWorkspaceItemViewModel _project;
    private readonly ProjectTabHostContext _hostContext;
    private bool _isAttached;

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
        ProjectImportViewModel import,
        ProjectRequestWorkflowViewModel workflow,
        ProjectQuickRequestSaveViewModel quickRequestSave,
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
        Import = import;
        Workflow = workflow;
        QuickRequestSave = quickRequestSave;
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
    public ProjectImportViewModel Import { get; }
    public ProjectRequestWorkflowViewModel Workflow { get; }
    public ProjectQuickRequestSaveViewModel QuickRequestSave { get; }
    public ProjectTabSummaryViewModel Summary { get; }
    public ProjectTabLifecycleCoordinator Lifecycle { get; }

    public static ProjectTabComposition Create(
        ProjectWorkspaceItemViewModel project,
        RequestWorkspaceTabViewModel fallbackWorkspaceTab,
        IRequestExecutionService requestExecutionService,
        IRequestCaseService requestCaseService,
        IRequestHistoryService requestHistoryService,
        IEnvironmentVariableService environmentVariableService,
        IApiWorkspaceService apiWorkspaceService,
        IFilePickerService filePickerService,
        IAppNotificationService appNotificationService,
        ProjectTabHostContext hostContext)
    {
        return new Builder(
            project,
            fallbackWorkspaceTab,
            requestExecutionService,
            requestCaseService,
            requestHistoryService,
            environmentVariableService,
            apiWorkspaceService,
            filePickerService,
            appNotificationService,
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

        _project.PropertyChanged += OnProjectPropertyChanged;
        EnvironmentPanel.SelectedEnvironmentChanged += Lifecycle.OnSelectedEnvironmentChanged;
        EnvironmentPanel.Environments.CollectionChanged += OnCollectionChanged;
        HistoryPanel.HistoryItems.CollectionChanged += OnCollectionChanged;
        Workspace.PropertyChanged += Lifecycle.OnWorkspacePropertyChanged;
        Workspace.StateChanged += _hostContext.NotifyShellState;
        Workspace.EditorStateChanged += _hostContext.NotifyWorkspaceEditorState;
        Workspace.ActiveWorkspaceTabChanged += Lifecycle.OnWorkspaceActiveWorkspaceTabChanged;
        Shell.PropertyChanged += OnShellPropertyChanged;
        Settings.PropertyChanged += OnChildPropertyChanged;
        Import.PropertyChanged += OnChildPropertyChanged;
        QuickRequestSave.PropertyChanged += OnChildPropertyChanged;
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
        Workspace.StateChanged -= _hostContext.NotifyShellState;
        Workspace.EditorStateChanged -= _hostContext.NotifyWorkspaceEditorState;
        Workspace.ActiveWorkspaceTabChanged -= Lifecycle.OnWorkspaceActiveWorkspaceTabChanged;
        Shell.PropertyChanged -= OnShellPropertyChanged;
        Settings.PropertyChanged -= OnChildPropertyChanged;
        Import.PropertyChanged -= OnChildPropertyChanged;
        QuickRequestSave.PropertyChanged -= OnChildPropertyChanged;
        _isAttached = false;
    }

    protected override void DisposeManaged()
    {
        Detach();
        Catalog.Dispose();
        Import.Dispose();
        Workflow.Dispose();
        EnvironmentPanel.Dispose();
        UseCasesPanel.Dispose();
        HistoryPanel.Dispose();
        Workspace.Dispose();
    }

    private void OnProjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Settings.NotifyProjectChanged();
        _hostContext.NotifyShellState();
    }

    private void OnCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        _hostContext.NotifyShellState();
    }

    private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _hostContext.NotifyShellState();
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectWorkspaceShellViewModel.SelectedSection)
            or nameof(ProjectWorkspaceShellViewModel.IsProjectSettingsSection))
        {
            Settings.NotifyWorkspaceSectionChanged();
        }

        _hostContext.NotifyShellState();
    }
}
