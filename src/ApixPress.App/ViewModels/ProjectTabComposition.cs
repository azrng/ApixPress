using System.Collections.ObjectModel;
using System.ComponentModel;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;

namespace ApixPress.App.ViewModels;

internal sealed class ProjectTabComposition
{
    private readonly ProjectWorkspaceItemViewModel _project;
    private readonly ProjectTabHostContext _hostContext;

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
        ProjectTabHostContext hostContext)
    {
        var environmentPanel = new EnvironmentPanelViewModel(environmentVariableService);
        var useCasesPanel = new UseCasesPanelViewModel(requestCaseService);
        var historyPanel = new RequestHistoryPanelViewModel(requestHistoryService);

        ProjectImportViewModel? importViewModel = null;
        ProjectQuickRequestSaveViewModel? quickRequestSaveViewModel = null;
        ProjectRequestWorkflowViewModel? workflowViewModel = null;
        ProjectWorkspaceShellViewModel? shellViewModel = null;

        var workspace = new ProjectWorkspaceTabsViewModel(
            () =>
            {
                shellViewModel?.SelectInterfaceManagementSection();
            },
            hostContext.SetStatusMessage);
        var workspaceContext = new ProjectTabWorkspaceContext
        {
            GetActiveWorkspaceTab = hostContext.GetActiveWorkspaceTab,
            GetFallbackWorkspaceTab = () => fallbackWorkspaceTab,
            GetCurrentBaseUrl = () => environmentPanel.SelectedEnvironment?.BaseUrl ?? string.Empty,
            EnsureLandingWorkspaceTab = workspace.EnsureLandingWorkspaceTab,
            SelectInterfaceManagementSection = () =>
            {
                shellViewModel?.SelectInterfaceManagementSection();
            },
            HasHistory = () => historyPanel.HistoryItems.Count > 0
        };
        var shell = shellViewModel = new ProjectWorkspaceShellViewModel(
            workspaceContext,
            hostContext);
        var editor = new ProjectRequestEditorViewModel(workspaceContext);
        var settings = new ProjectSettingsShellViewModel(
            () => shellViewModel?.ShowProjectSettingsSection(),
            () => importViewModel?.DismissDialog(),
            () => shellViewModel?.IsProjectSettingsSection ?? false,
            () => project.Description,
            hostContext.SetStatusMessage,
            hostContext.NotifyShellState);
        var catalog = new ProjectWorkspaceCatalogViewModel(
            project.Id,
            requestCaseService,
            apiWorkspaceService,
            useCasesPanel,
            workspace,
            () => shellViewModel?.SelectInterfaceManagementSection(),
            hostContext.SetStatusMessage,
            hostContext.NotifyShellState,
            () => importViewModel?.LoadImportedDocumentsAsync(manageBusyState: false) ?? Task.CompletedTask);
        var import = importViewModel = new ProjectImportViewModel(
            project.Id,
            apiWorkspaceService,
            filePickerService,
            catalog.SyncImportedInterfacesAsync,
            hostContext.SetStatusMessage);
        var workflow = workflowViewModel = new ProjectRequestWorkflowViewModel(
            project.Id,
            requestExecutionService,
            requestCaseService,
            requestHistoryService,
            workspace,
            historyPanel,
            environmentPanel,
            catalog,
            workspaceContext,
            workspaceTab =>
            {
                quickRequestSaveViewModel?.OpenDialogFor(workspaceTab);
            },
            hostContext);
        var quickRequestSave = quickRequestSaveViewModel = new ProjectQuickRequestSaveViewModel(
            workspaceContext,
            (workspaceTab, requestNameOverride) => workflowViewModel?.SaveQuickRequestAsync(workspaceTab, requestNameOverride) ?? Task.FromResult(false),
            hostContext);
        var summary = new ProjectTabSummaryViewModel(
            () => project,
            () => environmentPanel.SelectedEnvironment,
            hostContext.GetActiveWorkspaceTab,
            () => useCasesPanel.RequestCases,
            () => historyPanel.HistoryItems,
            () => environmentPanel.Environments.Count,
            () => import.ImportedApiDocuments.Count);
        var lifecycle = new ProjectTabLifecycleCoordinator(
            project.Id,
            () => project.Name,
            useCasesPanel,
            environmentPanel,
            historyPanel,
            import,
            workspace,
            quickRequestSave,
            shell,
            editor,
            hostContext);

        return new ProjectTabComposition(
            project,
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
            hostContext);
    }

    public void Attach()
    {
        _project.PropertyChanged += OnProjectPropertyChanged;
        EnvironmentPanel.SelectedEnvironmentChanged += Lifecycle.OnSelectedEnvironmentChanged;
        EnvironmentPanel.Environments.CollectionChanged += OnCollectionChanged;
        HistoryPanel.HistoryItems.CollectionChanged += OnCollectionChanged;
        Workspace.PropertyChanged += Lifecycle.OnWorkspacePropertyChanged;
        Workspace.StateChanged += _hostContext.NotifyShellState;
        Workspace.EditorStateChanged += _hostContext.NotifyWorkspaceEditorState;
        Workspace.ActiveWorkspaceTabChanged += Lifecycle.OnWorkspaceActiveWorkspaceTabChanged;
        Editor.PropertyChanged += OnChildPropertyChanged;
        Shell.PropertyChanged += OnShellPropertyChanged;
        Settings.PropertyChanged += OnChildPropertyChanged;
        Import.PropertyChanged += OnChildPropertyChanged;
        QuickRequestSave.PropertyChanged += OnChildPropertyChanged;
        Shell.AddProjectSettingsNavigation(Settings.OpenWorkspaceCommand);
        Workspace.EnsureLandingWorkspaceTab();
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
