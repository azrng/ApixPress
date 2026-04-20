using System.Collections.ObjectModel;
using System.ComponentModel;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;

namespace ApixPress.App.ViewModels;

internal sealed class ProjectTabComposition
{
    private readonly ProjectWorkspaceItemViewModel _project;
    private readonly Action _notifyShellState;
    private readonly Action _notifyWorkspaceEditorState;

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
        Action notifyShellState,
        Action notifyWorkspaceEditorState)
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
        _notifyShellState = notifyShellState;
        _notifyWorkspaceEditorState = notifyWorkspaceEditorState;
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
        Func<RequestWorkspaceTabViewModel?> getActiveWorkspaceTab,
        Action<string> setStatusMessage,
        Action notifyShellState,
        Action notifyWorkspaceEditorState,
        Action notifyWorkspaceBindingsChanged,
        Action notifyActiveWorkspaceTabChanged,
        Action notifyWorkspaceTabMenuChanged,
        Action<bool> setBusyState)
    {
        var environmentPanel = new EnvironmentPanelViewModel(environmentVariableService);
        var useCasesPanel = new UseCasesPanelViewModel(requestCaseService);
        var historyPanel = new RequestHistoryPanelViewModel(requestHistoryService);

        ProjectImportViewModel? importViewModel = null;
        ProjectQuickRequestSaveViewModel? quickRequestSaveViewModel = null;
        ProjectRequestWorkflowViewModel? workflowViewModel = null;
        ProjectWorkspaceShellViewModel? shellViewModel = null;

        var workspace = new ProjectWorkspaceTabsViewModel(
            () => shellViewModel?.SelectInterfaceManagementSection(),
            setStatusMessage);
        var shell = shellViewModel = new ProjectWorkspaceShellViewModel(
            workspace.EnsureLandingWorkspaceTab,
            getActiveWorkspaceTab,
            () => historyPanel.HistoryItems.Count > 0,
            setStatusMessage,
            notifyShellState);
        var editor = new ProjectRequestEditorViewModel(
            getActiveWorkspaceTab,
            () => fallbackWorkspaceTab,
            () => environmentPanel.SelectedEnvironment?.BaseUrl ?? string.Empty);
        var settings = new ProjectSettingsShellViewModel(
            () => shellViewModel?.ShowProjectSettingsSection(),
            () => importViewModel?.DismissDialog(),
            () => shellViewModel?.IsProjectSettingsSection ?? false,
            () => project.Description,
            setStatusMessage,
            notifyShellState);
        var catalog = new ProjectWorkspaceCatalogViewModel(
            project.Id,
            requestCaseService,
            apiWorkspaceService,
            useCasesPanel,
            workspace,
            () => shellViewModel?.SelectInterfaceManagementSection(),
            setStatusMessage,
            notifyShellState,
            () => importViewModel?.LoadImportedDocumentsAsync(manageBusyState: false) ?? Task.CompletedTask);
        var import = importViewModel = new ProjectImportViewModel(
            project.Id,
            apiWorkspaceService,
            filePickerService,
            catalog.SyncImportedInterfacesAsync,
            setStatusMessage);
        var workflow = workflowViewModel = new ProjectRequestWorkflowViewModel(
            project.Id,
            requestExecutionService,
            requestCaseService,
            requestHistoryService,
            workspace,
            historyPanel,
            environmentPanel,
            catalog,
            getActiveWorkspaceTab,
            workspaceTab => quickRequestSaveViewModel?.OpenDialogFor(workspaceTab),
            () => shellViewModel?.SelectInterfaceManagementSection(),
            setStatusMessage,
            setBusyState,
            notifyShellState);
        var quickRequestSave = quickRequestSaveViewModel = new ProjectQuickRequestSaveViewModel(
            getActiveWorkspaceTab,
            (workspaceTab, requestNameOverride) => workflowViewModel?.SaveQuickRequestAsync(workspaceTab, requestNameOverride) ?? Task.FromResult(false),
            message =>
            {
                setStatusMessage(message);
                notifyShellState();
            });
        var summary = new ProjectTabSummaryViewModel(
            () => project,
            () => environmentPanel.SelectedEnvironment,
            getActiveWorkspaceTab,
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
            getActiveWorkspaceTab,
            setStatusMessage,
            notifyShellState,
            notifyWorkspaceBindingsChanged,
            notifyActiveWorkspaceTabChanged,
            notifyWorkspaceTabMenuChanged);

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
            notifyShellState,
            notifyWorkspaceEditorState);
    }

    public void Attach()
    {
        _project.PropertyChanged += OnProjectPropertyChanged;
        EnvironmentPanel.SelectedEnvironmentChanged += Lifecycle.OnSelectedEnvironmentChanged;
        EnvironmentPanel.Environments.CollectionChanged += OnCollectionChanged;
        HistoryPanel.HistoryItems.CollectionChanged += OnCollectionChanged;
        Workspace.PropertyChanged += Lifecycle.OnWorkspacePropertyChanged;
        Workspace.StateChanged += _notifyShellState;
        Workspace.EditorStateChanged += _notifyWorkspaceEditorState;
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
        _notifyShellState();
    }

    private void OnCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        _notifyShellState();
    }

    private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _notifyShellState();
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectWorkspaceShellViewModel.SelectedSection)
            or nameof(ProjectWorkspaceShellViewModel.IsProjectSettingsSection))
        {
            Settings.NotifyWorkspaceSectionChanged();
        }

        _notifyShellState();
    }
}
