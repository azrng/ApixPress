using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ApixPress.App.Messages;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel : ViewModelBase,
    IRecipient<StatusMessageRequest>,
    IRecipient<BusyStateChangedMessage>,
    IRecipient<NavigationRequestMessage>,
    IRecipient<WorkspaceStateChangedMessage>
{
    private readonly RequestWorkspaceTabViewModel _fallbackWorkspaceTab;
    private readonly ProjectTabComposition _composition;
    private readonly ProjectTabLifecycleCoordinator _lifecycle;
    private readonly IMessenger _messenger;
    private WorkspaceStateChangeFlags _pendingWorkspaceChanges;
    private bool _isApplyingWorkspaceChanges;

    public event Action<ProjectTabViewModel>? ShellStateChanged;

    public ProjectTabViewModel(
        ProjectWorkspaceItemViewModel project,
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
        IDialogService dialogService,
        Func<string, Task> handleProjectDeletedAsync)
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
        _messenger = new WeakReferenceMessenger();
        _messenger.RegisterAll(this);

        var hostContext = new ProjectTabHostContext
        {
            GetActiveWorkspaceTab = () => ActiveWorkspaceTab,
            IsInterfaceRootWorkspaceActive = () => IsInterfaceRootWorkspaceActive,
            Messenger = _messenger,
            DialogService = dialogService
        };
        _composition = ProjectTabComposition.Create(
            Project,
            _fallbackWorkspaceTab,
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
            hostContext);
        EnvironmentPanel = _composition.EnvironmentPanel;
        UseCasesPanel = _composition.UseCasesPanel;
        HistoryPanel = _composition.HistoryPanel;
        Workspace = _composition.Workspace;
        Shell = _composition.Shell;
        Editor = _composition.Editor;
        Settings = _composition.Settings;
        Catalog = _composition.Catalog;
        InterfaceRoot = _composition.InterfaceRoot;
        Import = _composition.Import;
        Workflow = _composition.Workflow;
        QuickRequestSave = _composition.QuickRequestSave;
        HttpCaseSave = _composition.HttpCaseSave;
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
    public ProjectInterfaceRootWorkspaceViewModel InterfaceRoot { get; }
    public ProjectRequestWorkflowViewModel Workflow { get; }
    public ProjectImportViewModel Import { get; }
    public ProjectQuickRequestSaveViewModel QuickRequestSave { get; }
    public ProjectHttpCaseSaveViewModel HttpCaseSave { get; }
    public ProjectTabSummaryViewModel Summary { get; }

    public ObservableCollection<RequestWorkspaceTabViewModel> WorkspaceTabs => Workspace.WorkspaceTabs;
    public ReadOnlyObservableCollection<RequestWorkspaceTabViewModel> VisibleWorkspaceTabs => Workspace.VisibleWorkspaceTabs;
    public ObservableCollection<RequestCaseItemViewModel> SavedRequests => UseCasesPanel.RequestCases;
    public ObservableCollection<RequestHistoryItemViewModel> RequestHistory => HistoryPanel.VisibleHistoryItems;
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
    private bool isWorkspaceLoading;

    [ObservableProperty]
    private string workspaceLoadingText = "正在打开项目工作区...";

    [ObservableProperty]
    private string statusMessage = "项目工作区已就绪。";

    [ObservableProperty]
    private bool isInterfaceRootWorkspaceActive;

    [ObservableProperty]
    private bool responseValidationEnabled = true;

    public async Task InitializeAsync()
    {
        WorkspaceLoadingText = $"正在打开项目：{Project.Name}";
        IsWorkspaceLoading = true;
        try
        {
            await _lifecycle.InitializeAsync();
        }
        finally
        {
            IsWorkspaceLoading = false;
        }
    }

    public async Task RefreshAsync()
    {
        WorkspaceLoadingText = $"正在刷新项目：{Project.Name}";
        IsWorkspaceLoading = true;
        try
        {
            await _lifecycle.RefreshAsync();
        }
        finally
        {
            IsWorkspaceLoading = false;
        }
    }

    public Task SaveCurrentEnvironmentAsync()
    {
        return _lifecycle.SaveCurrentEnvironmentAsync(Summary.CurrentEnvironmentLabel);
    }

    [RelayCommand]
    private async Task SendRequestAsync()
    {
        await Workflow.SendRequestAsync();
        NotifyShellState();
    }

    [RelayCommand]
    private void CancelRequest()
    {
        Workflow.CancelRequest();
        NotifyShellState();
    }

    [RelayCommand]
    private async Task SaveCaseAsync()
    {
        await Workflow.SaveCurrentEditorAsync();
        NotifyShellState();
    }

    [RelayCommand]
    private Task SaveHttpCaseAsync()
    {
        OpenHttpCaseSaveDialog();
        return Task.CompletedTask;
    }

    public void OpenHttpCaseSaveDialog()
    {
        if (ActiveWorkspaceTab is not null && ActiveWorkspaceTab.IsHttpInterfaceTab)
        {
            HttpCaseSave.OpenDialogFor(ActiveWorkspaceTab);
            NotifyShellState();
        }
    }

    public Task LoadHistoryRequestAsync(RequestHistoryItemViewModel? item)
    {
        return _lifecycle.LoadHistoryRequestAsync(item);
    }

    [RelayCommand]
    private async Task ResendHistoryRequestAsync(RequestHistoryItemViewModel? item)
    {
        // 对齐原型行内「重发」：载入历史请求为可编辑快捷请求后立即发送
        if (item is null)
        {
            return;
        }

        await LoadHistoryRequestAsync(item);
        await Workflow.SendRequestAsync();
        NotifyShellState();
    }

    public void Receive(StatusMessageRequest message)
    {
        StatusMessage = message.Message;
    }

    public void Receive(BusyStateChangedMessage message)
    {
        IsBusy = message.IsBusy;
    }

    public void Receive(NavigationRequestMessage message)
    {
        switch (message.Target)
        {
            case NavigationTarget.InterfaceRootWorkspace:
                IsInterfaceRootWorkspaceActive = true;
                break;
            case NavigationTarget.LandingWorkspaceTab:
                Workspace.EnsureLandingWorkspaceTab();
                break;
            case NavigationTarget.InterfaceManagementSection:
                Shell.SelectInterfaceManagementSection();
                break;
            case NavigationTarget.ProjectSettingsWorkspace:
                Shell.ShowProjectSettingsSection();
                break;
        }
    }

    public void Receive(WorkspaceStateChangedMessage message)
    {
        if (IsDisposed || message.Changes == WorkspaceStateChangeFlags.None)
        {
            return;
        }

        QueueWorkspaceChanges(message.Changes);
    }

    protected override void DisposeManaged()
    {
        _messenger.UnregisterAll(this);
        _pendingWorkspaceChanges = WorkspaceStateChangeFlags.None;
        _composition.Dispose();
        _fallbackWorkspaceTab.Dispose();
        ShellStateChanged = null;
    }

    private void NotifyShellState()
    {
        QueueWorkspaceChanges(WorkspaceStateChangeFlags.ShellState);
    }

    private void QueueWorkspaceChanges(WorkspaceStateChangeFlags changes)
    {
        if (IsDisposed || changes == WorkspaceStateChangeFlags.None)
        {
            return;
        }

        _pendingWorkspaceChanges |= changes;
        if (_isApplyingWorkspaceChanges)
        {
            return;
        }

        _isApplyingWorkspaceChanges = true;
        try
        {
            while (_pendingWorkspaceChanges != WorkspaceStateChangeFlags.None && !IsDisposed)
            {
                var batch = _pendingWorkspaceChanges;
                _pendingWorkspaceChanges = WorkspaceStateChangeFlags.None;
                ApplyWorkspaceChanges(batch);
            }
        }
        finally
        {
            _isApplyingWorkspaceChanges = false;
        }
    }

    private void ApplyWorkspaceChanges(WorkspaceStateChangeFlags changes)
    {
        if (changes.HasFlag(WorkspaceStateChangeFlags.ShellState))
        {
            Summary.NotifyStateChanged();
            ShellStateChanged?.Invoke(this);
        }

        if (changes.HasFlag(WorkspaceStateChangeFlags.EditorState))
        {
            SyncInterfaceRootWorkspaceStateWithActiveTab();
            OnPropertyChanged(nameof(ConfigTab));
            OnPropertyChanged(nameof(ResponseSection));
            Shell.NotifyWorkspaceStateChanged();
            if (!changes.HasFlag(WorkspaceStateChangeFlags.ActiveTabChanged))
            {
                // 每键编辑路径：setter 已即时刷新过，这里只做轻量通知+防抖重算，避免重复全量重算
                Editor.NotifyWorkspaceEditorChanged();
            }
            // 与 ActiveTabChanged 同时到达（切标签批次）时，编辑器刷新由下方 ActiveTabChanged 分支立即执行
        }

        if (changes.HasFlag(WorkspaceStateChangeFlags.BindingsChanged))
        {
            OnPropertyChanged(nameof(ConfigTab));
            OnPropertyChanged(nameof(ResponseSection));
        }

        if (changes.HasFlag(WorkspaceStateChangeFlags.ActiveTabChanged))
        {
            SyncInterfaceRootWorkspaceStateWithActiveTab();
            OnPropertyChanged(nameof(ActiveWorkspaceTab));
            // 切换标签属于结构变化：编辑器必须立即换内容，不依赖后续 EditorState 的防抖刷新
            Editor.NotifyStateChanged();
        }

        if (changes.HasFlag(WorkspaceStateChangeFlags.TabMenuChanged))
        {
            OnPropertyChanged(nameof(IsWorkspaceTabMenuOpen));
        }
    }

    partial void OnIsInterfaceRootWorkspaceActiveChanged(bool value)
    {
        Shell.NotifyWorkspaceStateChanged();
    }

    private void SyncInterfaceRootWorkspaceStateWithActiveTab()
    {
        IsInterfaceRootWorkspaceActive = ActiveWorkspaceTab?.IsInterfaceRootTab == true;
    }

    public async Task ShowInterfaceRootWorkspaceAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        await _composition.OpenInterfaceRootWorkspaceAsync();
    }
}
