using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel : ViewModelBase
{
    private readonly RequestWorkspaceTabViewModel _fallbackWorkspaceTab;
    private bool _initialized;

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

        EnvironmentPanel = new EnvironmentPanelViewModel(environmentVariableService);
        UseCasesPanel = new UseCasesPanelViewModel(requestCaseService);
        HistoryPanel = new RequestHistoryPanelViewModel(requestHistoryService);
        ProjectImportViewModel? importViewModel = null;
        ProjectQuickRequestSaveViewModel? quickRequestSaveViewModel = null;
        ProjectRequestWorkflowViewModel? workflowViewModel = null;
        ProjectWorkspaceShellViewModel? shellViewModel = null;
        Workspace = new ProjectWorkspaceTabsViewModel(
            () => shellViewModel?.SelectInterfaceManagementSection(),
            message => StatusMessage = message);
        Shell = shellViewModel = new ProjectWorkspaceShellViewModel(
            Workspace.EnsureLandingWorkspaceTab,
            () => ActiveWorkspaceTab,
            () => HistoryPanel.HistoryItems.Count > 0,
            message => StatusMessage = message,
            NotifyShellState);
        Editor = new ProjectRequestEditorViewModel(
            () => ActiveWorkspaceTab,
            () => _fallbackWorkspaceTab,
            () => EnvironmentPanel.SelectedEnvironment?.BaseUrl ?? string.Empty);
        Settings = new ProjectSettingsShellViewModel(
            () => shellViewModel?.ShowProjectSettingsSection(),
            () => importViewModel?.DismissDialog(),
            () => shellViewModel?.IsProjectSettingsSection ?? false,
            () => Project.Description,
            message => StatusMessage = message,
            NotifyShellState);
        Catalog = new ProjectWorkspaceCatalogViewModel(
            Project.Id,
            requestCaseService,
            apiWorkspaceService,
            UseCasesPanel,
            Workspace,
            () => shellViewModel?.SelectInterfaceManagementSection(),
            message => StatusMessage = message,
            NotifyShellState,
            () => importViewModel?.LoadImportedDocumentsAsync(manageBusyState: false) ?? Task.CompletedTask);
        Import = importViewModel = new ProjectImportViewModel(
            Project.Id,
            apiWorkspaceService,
            filePickerService,
            Catalog.SyncImportedInterfacesAsync,
            message => StatusMessage = message);
        Workflow = workflowViewModel = new ProjectRequestWorkflowViewModel(
            Project.Id,
            requestExecutionService,
            requestCaseService,
            requestHistoryService,
            Workspace,
            HistoryPanel,
            EnvironmentPanel,
            Catalog,
            () => ActiveWorkspaceTab,
            workspaceTab => quickRequestSaveViewModel?.OpenDialogFor(workspaceTab),
            () => shellViewModel?.SelectInterfaceManagementSection(),
            message => StatusMessage = message,
            value => IsBusy = value,
            NotifyShellState);
        QuickRequestSave = quickRequestSaveViewModel = new ProjectQuickRequestSaveViewModel(
            () => ActiveWorkspaceTab,
            (workspaceTab, requestNameOverride) => workflowViewModel?.SaveQuickRequestAsync(workspaceTab, requestNameOverride) ?? Task.FromResult(false),
            message =>
            {
                StatusMessage = message;
                NotifyShellState();
            });
        Summary = new ProjectTabSummaryViewModel(
            () => Project,
            () => EnvironmentPanel.SelectedEnvironment,
            () => ActiveWorkspaceTab,
            () => SavedRequests,
            () => RequestHistory,
            () => EnvironmentPanel.Environments.Count,
            () => Import.ImportedApiDocuments.Count);

        Project.PropertyChanged += (_, _) =>
        {
            Settings.NotifyProjectChanged();
            NotifyShellState();
        };
        EnvironmentPanel.SelectedEnvironmentChanged += OnSelectedEnvironmentChanged;
        EnvironmentPanel.Environments.CollectionChanged += (_, _) => NotifyShellState();
        HistoryPanel.HistoryItems.CollectionChanged += (_, _) => NotifyShellState();
        Workspace.PropertyChanged += OnWorkspacePropertyChanged;
        Workspace.StateChanged += NotifyShellState;
        Workspace.EditorStateChanged += NotifyWorkspaceEditorState;
        Workspace.ActiveWorkspaceTabChanged += OnWorkspaceActiveWorkspaceTabChanged;
        Editor.PropertyChanged += (_, _) => NotifyShellState();
        Shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ProjectWorkspaceShellViewModel.SelectedSection)
                or nameof(ProjectWorkspaceShellViewModel.IsProjectSettingsSection))
            {
                Settings.NotifyWorkspaceSectionChanged();
            }

            NotifyShellState();
        };
        Settings.PropertyChanged += (_, _) => NotifyShellState();
        Import.PropertyChanged += (_, _) => NotifyShellState();
        QuickRequestSave.PropertyChanged += (_, _) => NotifyShellState();
        Shell.AddProjectSettingsNavigation(Settings.OpenWorkspaceCommand);

        Workspace.EnsureLandingWorkspaceTab();
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

    public void LoadHistoryRequest(RequestHistoryItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var targetTab = ActiveWorkspaceTab?.IsLandingTab == true
            ? ActiveWorkspaceTab
            : Workspace.FindFirstQuickRequestTab() ?? Workspace.CreateWorkspaceTab(activate: false);

        targetTab ??= Workspace.CreateWorkspaceTab(activate: false);
        targetTab.ConfigureAsQuickRequest();
        targetTab.ApplySnapshot(item.RequestSnapshot);
        if (item.ResponseSnapshot is not null)
        {
            targetTab.ResponseSection.ApplyResult(Azrng.Core.Results.ResultModel<ResponseSnapshotDto>.Success(item.ResponseSnapshot), item.RequestSnapshot);
        }

        Workspace.ActivateWorkspaceTab(targetTab);
        Shell.SelectRequestHistorySection();
        StatusMessage = $"已加载历史请求：{item.Method} {item.Url}";
        NotifyShellState();
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjectWorkspaceTabsViewModel.ActiveWorkspaceTab))
        {
            OnPropertyChanged(nameof(ActiveWorkspaceTab));
            Shell.NotifyWorkspaceStateChanged();
        }
        else if (e.PropertyName == nameof(ProjectWorkspaceTabsViewModel.IsWorkspaceTabMenuOpen))
        {
            OnPropertyChanged(nameof(IsWorkspaceTabMenuOpen));
        }
    }

    private void NotifyShellState()
    {
        Summary.NotifyStateChanged();
        OnPropertyChanged(nameof(VisibleWorkspaceTabs));
        ShellStateChanged?.Invoke(this);
    }
}
