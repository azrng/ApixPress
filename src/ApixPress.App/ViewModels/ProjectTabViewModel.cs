using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel : ViewModelBase
{
    private readonly RequestWorkspaceTabViewModel _fallbackWorkspaceTab;
    private readonly ProjectTabComposition _composition;
    private readonly ProjectTabLifecycleCoordinator _lifecycle;

    public event Action<ProjectTabViewModel>? ShellStateChanged;

    public ProjectTabViewModel(
        ProjectWorkspaceItemViewModel project,
        IRequestExecutionService requestExecutionService,
        IRequestCaseService requestCaseService,
        IRequestHistoryService requestHistoryService,
        IEnvironmentVariableService environmentVariableService,
        IApiWorkspaceService apiWorkspaceService,
        IFilePickerService filePickerService)
    {
        Project = new ProjectWorkspaceItemViewModel
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            IsDefault = project.IsDefault
        };

        _fallbackWorkspaceTab = new RequestWorkspaceTabViewModel();
        _fallbackWorkspaceTab.ConfigureAsLanding();
        var hostContext = new ProjectTabHostContext
        {
            GetActiveWorkspaceTab = () => ActiveWorkspaceTab,
            SetStatusMessage = message => StatusMessage = message,
            NotifyShellState = NotifyShellState,
            NotifyWorkspaceEditorState = NotifyWorkspaceEditorState,
            NotifyWorkspaceBindingsChanged = NotifyWorkspaceBindingsChanged,
            NotifyActiveWorkspaceTabChanged = () => OnPropertyChanged(nameof(ActiveWorkspaceTab)),
            NotifyWorkspaceTabMenuChanged = () => OnPropertyChanged(nameof(IsWorkspaceTabMenuOpen)),
            SetBusyState = value => IsBusy = value
        };
        _composition = ProjectTabComposition.Create(
            Project,
            _fallbackWorkspaceTab,
            requestExecutionService,
            requestCaseService,
            requestHistoryService,
            environmentVariableService,
            apiWorkspaceService,
            filePickerService,
            hostContext);
        EnvironmentPanel = _composition.EnvironmentPanel;
        UseCasesPanel = _composition.UseCasesPanel;
        HistoryPanel = _composition.HistoryPanel;
        Workspace = _composition.Workspace;
        Shell = _composition.Shell;
        Editor = _composition.Editor;
        Settings = _composition.Settings;
        Catalog = _composition.Catalog;
        Import = _composition.Import;
        Workflow = _composition.Workflow;
        QuickRequestSave = _composition.QuickRequestSave;
        Summary = _composition.Summary;
        _lifecycle = _composition.Lifecycle;
        _composition.Attach();
    }

    public ProjectWorkspaceItemViewModel Project { get; }
    public EnvironmentPanelViewModel EnvironmentPanel { get; }
    public UseCasesPanelViewModel UseCasesPanel { get; }
    public RequestHistoryPanelViewModel HistoryPanel { get; }
    public ProjectWorkspaceTabsViewModel Workspace { get; }
    public ProjectWorkspaceShellViewModel Shell { get; }
    public ProjectRequestEditorViewModel Editor { get; }
    public ProjectSettingsShellViewModel Settings { get; }
    public ProjectWorkspaceCatalogViewModel Catalog { get; }
    public ProjectRequestWorkflowViewModel Workflow { get; }
    public ProjectImportViewModel Import { get; }
    public ProjectQuickRequestSaveViewModel QuickRequestSave { get; }
    public ProjectTabSummaryViewModel Summary { get; }

    public ObservableCollection<RequestWorkspaceTabViewModel> WorkspaceTabs => Workspace.WorkspaceTabs;
    public ReadOnlyObservableCollection<RequestWorkspaceTabViewModel> VisibleWorkspaceTabs => Workspace.VisibleWorkspaceTabs;
    public ObservableCollection<RequestCaseItemViewModel> SavedRequests => UseCasesPanel.RequestCases;
    public ObservableCollection<RequestHistoryItemViewModel> RequestHistory => HistoryPanel.HistoryItems;
    public RequestWorkspaceTabViewModel? ActiveWorkspaceTab
    {
        get => Workspace.ActiveWorkspaceTab;
        set => Workspace.ActiveWorkspaceTab = value;
    }

    public bool IsWorkspaceTabMenuOpen
    {
        get => Workspace.IsWorkspaceTabMenuOpen;
        set => Workspace.IsWorkspaceTabMenuOpen = value;
    }

    public RequestConfigTabViewModel ConfigTab => ActiveWorkspaceTab?.ConfigTab ?? _fallbackWorkspaceTab.ConfigTab;
    public ResponseSectionViewModel ResponseSection => ActiveWorkspaceTab?.ResponseSection ?? _fallbackWorkspaceTab.ResponseSection;

    public string ProjectId => Project.Id;
    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "项目工作区已就绪。";

    [ObservableProperty]
    private bool responseValidationEnabled = true;

    public Task InitializeAsync()
    {
        return _lifecycle.InitializeAsync();
    }

    public Task RefreshAsync()
    {
        return _lifecycle.RefreshAsync();
    }

    public Task SaveCurrentEnvironmentAsync()
    {
        return _lifecycle.SaveCurrentEnvironmentAsync(Summary.CurrentEnvironmentLabel);
    }

    public void LoadHistoryRequest(RequestHistoryItemViewModel? item)
    {
        _lifecycle.LoadHistoryRequest(item);
    }

    protected override void DisposeManaged()
    {
        _composition.Dispose();
        _fallbackWorkspaceTab.Dispose();
        ShellStateChanged = null;
    }

    private void NotifyShellState()
    {
        if (IsDisposed)
        {
            return;
        }

        Summary.NotifyStateChanged();
        OnPropertyChanged(nameof(VisibleWorkspaceTabs));
        ShellStateChanged?.Invoke(this);
    }

    private void NotifyWorkspaceBindingsChanged()
    {
        if (IsDisposed)
        {
            return;
        }

        OnPropertyChanged(nameof(ConfigTab));
        OnPropertyChanged(nameof(ResponseSection));
    }

    private void NotifyWorkspaceEditorState()
    {
        if (IsDisposed)
        {
            return;
        }

        NotifyWorkspaceBindingsChanged();
        Shell.NotifyWorkspaceStateChanged();
        Editor.NotifyStateChanged();
        NotifyShellState();
    }
}
