using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;
using Azrng.Core.Results;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel : ViewModelBase
{
    private static class WorkspaceSections
    {
        public const string InterfaceManagement = "interface-management";
        public const string RequestHistory = "request-history";
        public const string ProjectSettings = "project-settings";
    }

    private static class ProjectSettingsSections
    {
        public const string Overview = "overview";
        public const string ImportData = "import-data";
    }

    private static class ImportDataModes
    {
        public const string File = "file";
        public const string Url = "url";
    }

    private static class ImportStatusStates
    {
        public const string Info = "info";
        public const string Success = "success";
        public const string Error = "error";
    }

    private static class RequestEntryTypes
    {
        public const string QuickRequest = "quick-request";
        public const string HttpInterface = "http-interface";
        public const string HttpCase = "http-case";
    }

    private readonly IRequestCaseService _requestCaseService;
    private readonly IRequestExecutionService _requestExecutionService;
    private readonly IRequestHistoryService _requestHistoryService;
    private readonly IApiWorkspaceService _apiWorkspaceService;
    private readonly IFilePickerService _filePickerService;
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

        _requestExecutionService = requestExecutionService;
        _requestCaseService = requestCaseService;
        _requestHistoryService = requestHistoryService;
        _apiWorkspaceService = apiWorkspaceService;
        _filePickerService = filePickerService;

        _fallbackWorkspaceTab = new RequestWorkspaceTabViewModel();
        _fallbackWorkspaceTab.ConfigureAsLanding();

        EnvironmentPanel = new EnvironmentPanelViewModel(environmentVariableService);
        UseCasesPanel = new UseCasesPanelViewModel(requestCaseService);
        HistoryPanel = new RequestHistoryPanelViewModel(requestHistoryService);

        Project.PropertyChanged += (_, _) => NotifyShellState();
        EnvironmentPanel.SelectedEnvironmentChanged += OnSelectedEnvironmentChanged;
        EnvironmentPanel.Environments.CollectionChanged += (_, _) => NotifyShellState();
        UseCasesPanel.RequestCases.CollectionChanged += (_, _) =>
        {
            RebuildWorkspaceNavigation();
            NotifyShellState();
        };
        HistoryPanel.HistoryItems.CollectionChanged += (_, _) => NotifyShellState();
        WorkspaceTabs.CollectionChanged += OnWorkspaceTabsCollectionChanged;
        ImportedApiDocuments.CollectionChanged += (_, _) => NotifyShellState();

        WorkspaceNavigationItems.Add(new ProjectWorkspaceNavItemViewModel(
            WorkspaceSections.InterfaceManagement,
            "接口管理",
            "M4,5 L20,5 L20,7 L4,7 Z M4,10 L20,10 L20,12 L4,12 Z M4,15 L20,15 L20,17 L4,17 Z",
            ShowInterfaceManagementCommand));
        WorkspaceNavigationItems.Add(new ProjectWorkspaceNavItemViewModel(
            WorkspaceSections.RequestHistory,
            "请求历史",
            "M12,4 A8,8 0 1 0 20,12 A8,8 0 1 0 12,4 M12,7 L12,12 L15.5,14",
            ShowRequestHistoryCommand));
        WorkspaceNavigationItems.Add(new ProjectWorkspaceNavItemViewModel(
            WorkspaceSections.ProjectSettings,
            "项目设置",
            "M12,8.5 A3.5,3.5 0 1 0 12,15.5 A3.5,3.5 0 1 0 12,8.5 M12,3 L13.2,3.3 L13.8,5 L15.5,5.5 L17,4.7 L18.3,6 L17.5,7.5 L18,9.2 L19.7,9.8 L20,11 L18.3,12.2 L18,13.8 L19.5,15 L18.3,16.3 L16.8,15.5 L15.2,16 L14.5,17.7 L13.3,18 L12,16.7 L10.7,18 L9.5,17.7 L8.8,16 L7.2,15.5 L5.7,16.3 L4.5,15 L6,13.8 L5.7,12.2 L4,11 L4.3,9.8 L6,9.2 L6.5,7.5 L5.7,6 L7,4.7 L8.5,5.5 L10.2,5 L10.8,3.3 Z",
            ShowProjectSettingsCommand));
        SyncWorkspaceNavigationSelection();

        EnsureLandingWorkspaceTab();
    }

    public ProjectWorkspaceItemViewModel Project { get; }
    public EnvironmentPanelViewModel EnvironmentPanel { get; }
    public UseCasesPanelViewModel UseCasesPanel { get; }
    public RequestHistoryPanelViewModel HistoryPanel { get; }

    public ObservableCollection<RequestWorkspaceTabViewModel> WorkspaceTabs { get; } = [];
    public ObservableCollection<ExplorerItemViewModel> InterfaceTreeItems { get; } = [];
    public ObservableCollection<ExplorerItemViewModel> QuickRequestTreeItems { get; } = [];
    public ObservableCollection<ProjectWorkspaceNavItemViewModel> WorkspaceNavigationItems { get; } = [];
    public ObservableCollection<ProjectImportedDocumentItemViewModel> ImportedApiDocuments { get; } = [];
    public IReadOnlyList<ExplorerItemViewModel> InterfaceCatalogItems => InterfaceTreeItems.FirstOrDefault()?.Children ?? [];
    public IReadOnlyList<RequestWorkspaceTabViewModel> VisibleWorkspaceTabs => WorkspaceTabs
        .Where(item => !item.IsLandingTab || item.ShowInTabStrip)
        .ToList();
    public ObservableCollection<RequestCaseItemViewModel> SavedRequests => UseCasesPanel.RequestCases;
    public ObservableCollection<RequestHistoryItemViewModel> RequestHistory => HistoryPanel.HistoryItems;

    public RequestConfigTabViewModel ConfigTab => ActiveWorkspaceTab?.ConfigTab ?? _fallbackWorkspaceTab.ConfigTab;
    public ResponseSectionViewModel ResponseSection => ActiveWorkspaceTab?.ResponseSection ?? _fallbackWorkspaceTab.ResponseSection;

    public string ProjectId => Project.Id;
    public string TabTitle => Project.Name;
    public string ProjectSummary => string.IsNullOrWhiteSpace(Project.Description) ? "暂无项目备注" : Project.Description;
    public string CurrentEnvironmentLabel => EnvironmentPanel.SelectedEnvironment?.Name ?? "未选择环境";
    public string CurrentBaseUrlText => string.IsNullOrWhiteSpace(EnvironmentPanel.SelectedEnvironment?.BaseUrl)
        ? "当前环境暂未配置 BaseUrl"
        : EnvironmentPanel.SelectedEnvironment.BaseUrl;
    public bool HasEnvironmentContext => ActiveWorkspaceTab is not null && !ActiveWorkspaceTab.IsLandingTab;
    public bool HasSavedRequests => SavedRequests.Count > 0;
    public bool HasHistory => RequestHistory.Count > 0;
    public bool HasQuickRequestEntries => SavedRequests.Any(item => string.Equals(item.SourceCase.EntryType, RequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase));
    public bool HasInterfaceEntries => SavedRequests.Any(item => string.Equals(item.SourceCase.EntryType, RequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase));
    public bool ShowInterfaceEntriesEmptyState => !HasInterfaceEntries;
    public bool ShowQuickRequestEntriesEmptyState => !HasQuickRequestEntries;
    public bool ShowSavedRequestsEmptyState => !HasQuickRequestEntries && !HasInterfaceEntries;
    public bool ShowHistoryEmptyState => !HasHistory;
    public bool IsInterfaceManagementSection => SelectedWorkspaceSection == WorkspaceSections.InterfaceManagement;
    public bool IsRequestHistorySection => SelectedWorkspaceSection == WorkspaceSections.RequestHistory;
    public bool IsProjectSettingsSection => SelectedWorkspaceSection == WorkspaceSections.ProjectSettings;
    public bool IsProjectSettingsOverviewSelected => SelectedProjectSettingsSection == ProjectSettingsSections.Overview;
    public bool IsProjectSettingsImportDataSelected => SelectedProjectSettingsSection == ProjectSettingsSections.ImportData;
    public bool ShowProjectSettingsOverviewSection => IsProjectSettingsSection && IsProjectSettingsOverviewSelected;
    public bool ShowProjectSettingsImportDataSection => IsProjectSettingsSection && IsProjectSettingsImportDataSelected;
    public bool IsImportFileMode => SelectedImportDataMode == ImportDataModes.File;
    public bool IsImportUrlMode => SelectedImportDataMode == ImportDataModes.Url;
    public bool ShowProjectImportDialogStatus => ShowImportStatusInfo || ShowImportStatusSuccess || ShowImportStatusError;
    public bool HasSelectedImportFile => !string.IsNullOrWhiteSpace(SelectedImportFilePath);
    public string SelectedImportFileName => HasSelectedImportFile ? Path.GetFileName(SelectedImportFilePath) : "尚未选择 Swagger 文件";
    public string SelectedImportFileSummary => HasSelectedImportFile
        ? SelectedImportFilePath
        : "请选择本地 Swagger/OpenAPI JSON 文件后再执行导入。";
    public bool HasImportedApiDocuments => ImportedApiDocuments.Count > 0;
    public bool ShowImportedApiDocumentsEmptyState => !IsImportDataBusy && !HasImportedApiDocuments;
    public bool ShowImportStatusInfo => ImportDataStatusState == ImportStatusStates.Info;
    public bool ShowImportStatusSuccess => ImportDataStatusState == ImportStatusStates.Success;
    public bool ShowImportStatusError => ImportDataStatusState == ImportStatusStates.Error;
    public bool IsQuickRequestEditor => ActiveWorkspaceTab?.IsQuickRequestTab ?? false;
    public bool IsHttpInterfaceEditor => ActiveWorkspaceTab?.IsHttpInterfaceTab ?? false;
    public bool IsRequestEditorOpen => ActiveWorkspaceTab is not null && !ActiveWorkspaceTab.IsLandingTab;
    public bool ShowInterfaceManagementLanding => IsInterfaceManagementSection && (ActiveWorkspaceTab?.IsLandingTab ?? true);
    public bool ShowRequestEditorWorkspace => IsInterfaceManagementSection && IsRequestEditorOpen;
    public string SavedRequestCountText => SavedRequests.Count(item =>
        string.Equals(item.SourceCase.EntryType, RequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.SourceCase.EntryType, RequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase)).ToString();
    public string HistoryCountText => RequestHistory.Count.ToString();
    public string EnvironmentCountText => EnvironmentPanel.Environments.Count.ToString();
    public string ImportedApiDocumentCountText => ImportedApiDocuments.Count.ToString();
    public string ProjectSettingsDescription => string.IsNullOrWhiteSpace(Project.Description)
        ? "当前项目还没有补充备注，可在这里继续维护环境与工作区说明。"
        : Project.Description;
    public string CurrentProjectSettingsTitle => IsProjectSettingsImportDataSelected ? "导入数据" : "项目设置";
    public string CurrentProjectSettingsSubtitle => IsProjectSettingsImportDataSelected
        ? "支持 Swagger 文件上传和 URL 导入，导入结果会持久化保存到当前项目。"
        : "保持项目说明、环境切换和快捷请求工作区之间的关系清晰可见。";
    public string InterfaceSectionHint => HasInterfaceEntries ? "默认模块 / 接口" : "默认模块下还没有保存的 HTTP 接口";
    public string QuickRequestSectionHint => HasQuickRequestEntries ? "保存到左侧快捷请求目录" : "左侧快捷请求目录还是空的";
    public string CurrentEditorTitle => ActiveWorkspaceTab?.EditorTitle ?? "新建...";
    public string CurrentEditorDescription => ActiveWorkspaceTab?.EditorDescription ?? "从下方卡片中选择要创建的工作内容。";
    public string CurrentEditorPrimaryActionText => ActiveWorkspaceTab?.PrimaryActionText ?? "保存";
    public string CurrentEditorUrlWatermark => ActiveWorkspaceTab?.UrlWatermark ?? "输入请求地址";
    public bool ShowEditorBaseUrlPrefix => IsHttpInterfaceEditor;
    public string CurrentEditorBaseUrlPrefix => IsHttpInterfaceEditor ? EnvironmentPanel.SelectedEnvironment?.BaseUrl ?? string.Empty : string.Empty;
    public string CurrentHttpInterfaceBaseUrl => IsHttpInterfaceEditor ? EnvironmentPanel.SelectedEnvironment?.BaseUrl ?? string.Empty : string.Empty;
    public string CurrentHttpInterfaceName
    {
        get
        {
            if (ActiveWorkspaceTab is null)
            {
                return string.Empty;
            }

            var currentName = ActiveWorkspaceTab.ConfigTab.RequestName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(currentName))
            {
                return "未命名接口";
            }

            return ActiveWorkspaceTab.IsHttpInterfaceTab
                && string.Equals(currentName, ActiveWorkspaceTab.ResolveGeneratedRequestName(), StringComparison.Ordinal)
                ? "未命名接口"
                : currentName;
        }
        set
        {
            if (ActiveWorkspaceTab is null)
            {
                return;
            }

            var normalizedValue = string.Equals(value?.Trim(), "未命名接口", StringComparison.Ordinal)
                ? string.Empty
                : value?.Trim() ?? string.Empty;
            if (ActiveWorkspaceTab.ConfigTab.RequestName == normalizedValue)
            {
                return;
            }

            ActiveWorkspaceTab.ConfigTab.RequestName = normalizedValue;
            NotifyWorkspaceEditorState();
        }
    }
    public string CurrentHttpInterfaceDisplayName => string.IsNullOrWhiteSpace(CurrentHttpInterfaceName)
        ? "未命名接口"
        : CurrentHttpInterfaceName.Trim();
    public bool IsHttpDebugEditorMode => ActiveWorkspaceTab?.IsHttpDebugView ?? false;
    public bool IsHttpDesignEditorMode => ActiveWorkspaceTab?.IsHttpDesignView ?? false;
    public bool IsHttpDocumentPreviewMode => ActiveWorkspaceTab?.IsHttpDocumentPreviewView ?? false;
    public bool ShowHttpWorkbenchContent => IsHttpInterfaceEditor && !IsHttpDocumentPreviewMode;
    public bool ShowHttpDocumentPreviewContent => IsHttpInterfaceEditor && IsHttpDocumentPreviewMode;
    public bool ShowSaveHttpCaseAction => IsHttpInterfaceEditor;
    public string CurrentEditorBaseUrlCaption => IsHttpInterfaceEditor
        ? (string.IsNullOrWhiteSpace(EnvironmentPanel.SelectedEnvironment?.BaseUrl) ? "当前环境未配置 BaseUrl" : EnvironmentPanel.SelectedEnvironment.BaseUrl)
        : "快捷请求不固定 BaseUrl";
    public bool HasHttpDocumentParameters => ConfigTab.QueryParameters.Count > 0;
    public bool HasHttpDocumentHeaders => ConfigTab.Headers.Count > 0;
    public bool HasHttpDocumentRequestDetails => HasHttpDocumentParameters || HasHttpDocumentHeaders || ConfigTab.HasBodyContent;
    public bool ShowHttpDocumentRequestEmpty => !HasHttpDocumentRequestDetails;
    public string CurrentHttpDocumentBodyModeText => ConfigTab.HasBodyContent
        ? (ConfigTab.SelectedBodyModeOption?.DisplayName ?? ConfigTab.SelectedBodyMode)
        : "无";
    public string CurrentHttpDocumentUrl => BuildHttpDocumentUrl();
    public string CurrentHttpDocumentResponseSummary => ResponseSection.HasResponse
        ? CurrentResponseValidationResultText
        : "等待调试后生成响应示例";
    public string CurrentHttpDocumentBodyPreview => ResponseSection.HasResponse && !string.IsNullOrWhiteSpace(ResponseSection.BodyText)
        ? ResponseSection.BodyText
        : "{ }";
    public string CurrentHttpDocumentCurlSnippet => BuildHttpDocumentCurlSnippet();
    public string CurrentResponseValidationResultText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ResponseSection.StatusText))
            {
                return "等待响应";
            }

            if (string.Equals(ResponseSection.StatusText, "请求失败", StringComparison.OrdinalIgnoreCase))
            {
                return "请求失败";
            }

            if (ResponseSection.StatusText.StartsWith("HTTP ", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(ResponseSection.StatusText["HTTP ".Length..], out var code))
            {
                return code is >= 200 and < 300 ? $"成功 ({code})" : $"HTTP {code}";
            }

            return ResponseSection.StatusText;
        }
    }
    public string SelectedMethod
    {
        get => ActiveWorkspaceTab?.SelectedMethod ?? "GET";
        set
        {
            if (ActiveWorkspaceTab is null || ActiveWorkspaceTab.SelectedMethod == value)
            {
                return;
            }

            ActiveWorkspaceTab.SelectedMethod = value;
            NotifyWorkspaceEditorState();
        }
    }

    public string RequestUrl
    {
        get => ActiveWorkspaceTab?.RequestUrl ?? string.Empty;
        set
        {
            if (ActiveWorkspaceTab is null || ActiveWorkspaceTab.RequestUrl == value)
            {
                return;
            }

            ActiveWorkspaceTab.RequestUrl = value;
            NotifyWorkspaceEditorState();
        }
    }

    public string CurrentInterfaceFolderPath
    {
        get => ActiveWorkspaceTab?.InterfaceFolderPath ?? "用户";
        set
        {
            if (ActiveWorkspaceTab is null || ActiveWorkspaceTab.InterfaceFolderPath == value)
            {
                return;
            }

            ActiveWorkspaceTab.InterfaceFolderPath = value;
            NotifyWorkspaceEditorState();
        }
    }

    public string CurrentHttpCaseName
    {
        get => ActiveWorkspaceTab?.HttpCaseName ?? "成功";
        set
        {
            if (ActiveWorkspaceTab is null || ActiveWorkspaceTab.HttpCaseName == value)
            {
                return;
            }

            ActiveWorkspaceTab.HttpCaseName = value;
            NotifyWorkspaceEditorState();
        }
    }

    public IReadOnlyList<string> HttpMethods { get; } = ["GET", "POST", "PUT", "DELETE", "PATCH"];

    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "项目工作区已就绪。";

    [ObservableProperty]
    private string selectedWorkspaceSection = WorkspaceSections.InterfaceManagement;

    [ObservableProperty]
    private ProjectWorkspaceNavItemViewModel? selectedWorkspaceNavigationItem;

    [ObservableProperty]
    private string selectedProjectSettingsSection = ProjectSettingsSections.Overview;

    [ObservableProperty]
    private string selectedImportDataMode = ImportDataModes.File;

    [ObservableProperty]
    private string selectedImportFilePath = string.Empty;

    [ObservableProperty]
    private string importUrl = string.Empty;

    [ObservableProperty]
    private bool isImportDataBusy;

    [ObservableProperty]
    private string importDataStatusText = "请选择 Swagger/OpenAPI JSON 文件，或输入可访问的文档 URL。";

    [ObservableProperty]
    private string importDataStatusState = ImportStatusStates.Info;

    [ObservableProperty]
    private bool isInterfaceCatalogExpanded = true;

    [ObservableProperty]
    private bool isDataModelCatalogExpanded;

    [ObservableProperty]
    private bool isComponentLibraryCatalogExpanded;

    [ObservableProperty]
    private bool isQuickRequestCatalogExpanded = true;

    [ObservableProperty]
    private RequestWorkspaceTabViewModel? activeWorkspaceTab;

    [ObservableProperty]
    private bool isQuickRequestSaveDialogOpen;

    [ObservableProperty]
    private bool isProjectImportDialogOpen;

    [ObservableProperty]
    private string quickRequestSaveName = string.Empty;

    [ObservableProperty]
    private string quickRequestSaveDescription = string.Empty;

    [ObservableProperty]
    private bool responseValidationEnabled = true;

    [ObservableProperty]
    private bool isWorkspaceTabMenuOpen;

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
        await LoadWorkspaceAsync(EnvironmentPanel.SelectedEnvironment?.Id);
        StatusMessage = $"项目 {Project.Name} 已刷新。";
        NotifyShellState();
    }

    public async Task SaveCurrentEnvironmentAsync()
    {
        if (!EnvironmentPanel.HasSelectedEnvironment)
        {
            StatusMessage = "请先选择环境后再保存。";
            NotifyShellState();
            return;
        }

        await EnvironmentPanel.SaveEnvironmentCommand.ExecuteAsync(null);
        StatusMessage = $"环境 {CurrentEnvironmentLabel} 已保存。";
        NotifyShellState();
    }

    public async Task SendQuickRequestAsync()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var workspaceTab = ActiveWorkspaceTab;
        if (workspaceTab is null || workspaceTab.IsLandingTab)
        {
            StatusMessage = "请先打开一个 HTTP 接口或快捷请求标签。";
            NotifyShellState();
            return;
        }

        if (string.IsNullOrWhiteSpace(workspaceTab.RequestUrl))
        {
            StatusMessage = "请输入请求地址。";
            NotifyShellState();
            return;
        }

        if (workspaceTab.IsQuickRequestTab && !HasAbsoluteHttpUrl(workspaceTab.RequestUrl))
        {
            StatusMessage = "快捷请求仅支持完整地址，请输入 http:// 或 https:// 开头的 URL。";
            NotifyShellState();
            return;
        }

        IsBusy = true;
        var snapshot = workspaceTab.BuildSnapshot();
        var environment = BuildExecutionEnvironment();
        var result = await _requestExecutionService.SendAsync(snapshot, environment, CancellationToken.None);
        workspaceTab.ResponseSection.ApplyResult(result, snapshot);

        if (result.IsSuccess || result.Data is not null)
        {
            await _requestHistoryService.AddAsync(ProjectId, snapshot, result.Data, CancellationToken.None);
            await HistoryPanel.LoadHistoryAsync();
        }

        IsBusy = false;
        StatusMessage = result.IsSuccess
            ? (workspaceTab.IsHttpInterfaceTab ? "HTTP 接口请求发送完成。" : "快捷请求发送完成。")
            : result.Message;
        NotifyShellState();
    }

    public async Task SaveCurrentEditorAsync()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var workspaceTab = ActiveWorkspaceTab;
        if (workspaceTab is null || workspaceTab.IsLandingTab)
        {
            StatusMessage = "请先打开一个请求标签。";
            NotifyShellState();
            return;
        }

        if (workspaceTab.IsHttpInterfaceTab)
        {
            await SaveHttpInterfaceAsync(workspaceTab);
            return;
        }

        if (!HasAbsoluteHttpUrl(workspaceTab.RequestUrl))
        {
            StatusMessage = "快捷请求仅支持完整地址，请输入 http:// 或 https:// 开头的 URL。";
            NotifyShellState();
            return;
        }

        OpenQuickRequestSaveDialog(workspaceTab);
    }

    public async Task SaveHistoryAsQuickRequestAsync(RequestHistoryItemViewModel item)
    {
        var snapshot = item.RequestSnapshot;
        var result = await _requestCaseService.SaveAsync(new RequestCaseDto
        {
            ProjectId = ProjectId,
            EntryType = RequestEntryTypes.QuickRequest,
            Name = $"{snapshot.Method} {snapshot.Url}",
            GroupName = "快捷请求",
            Description = $"从 {item.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm} 的历史记录创建",
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        if (result.IsSuccess)
        {
            await ReloadSavedRequestsAsync();
            StatusMessage = "已从历史记录生成快捷请求。";
        }
        else
        {
            StatusMessage = result.Message;
        }

        NotifyShellState();
    }

    [RelayCommand]
    public async Task SaveHttpCaseAsync()
    {
        var workspaceTab = ActiveWorkspaceTab;
        if (workspaceTab is null || !workspaceTab.IsHttpInterfaceTab)
        {
            return;
        }

        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var interfaceId = await EnsureHttpInterfaceSavedAsync(workspaceTab, reloadAfterSave: false);
        if (string.IsNullOrWhiteSpace(interfaceId))
        {
            return;
        }

        var snapshot = workspaceTab.BuildSnapshot();
        var result = await _requestCaseService.SaveAsync(new RequestCaseDto
        {
            Id = workspaceTab.EditingCaseId,
            ProjectId = ProjectId,
            EntryType = RequestEntryTypes.HttpCase,
            Name = BuildHttpCaseName(workspaceTab),
            GroupName = "用例",
            FolderPath = NormalizeFolderPath(workspaceTab.InterfaceFolderPath),
            ParentId = interfaceId,
            Description = $"{workspaceTab.ResolveRequestName()} 的请求用例",
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        if (result.IsSuccess && result.Data is not null)
        {
            workspaceTab.EditingCaseId = result.Data.Id;
            await ReloadSavedRequestsAsync();
            StatusMessage = "HTTP 接口用例已保存。";
        }
        else
        {
            StatusMessage = result.Message;
        }

        NotifyShellState();
    }

    [RelayCommand]
    private void CloseQuickRequestSaveDialog()
    {
        IsQuickRequestSaveDialogOpen = false;
        StatusMessage = "已取消保存快捷请求。";
        NotifyShellState();
    }

    [RelayCommand]
    private async Task ConfirmQuickRequestSaveAsync()
    {
        var workspaceTab = ActiveWorkspaceTab;
        if (workspaceTab is null || !workspaceTab.IsQuickRequestTab)
        {
            IsQuickRequestSaveDialogOpen = false;
            NotifyShellState();
            return;
        }

        if (string.IsNullOrWhiteSpace(QuickRequestSaveName))
        {
            StatusMessage = "请输入快捷请求名称。";
            NotifyShellState();
            return;
        }

        workspaceTab.ConfigTab.RequestName = QuickRequestSaveName.Trim();
        workspaceTab.ConfigTab.RequestDescription = QuickRequestSaveDescription.Trim();
        await SaveQuickRequestAsync(workspaceTab, workspaceTab.ConfigTab.RequestName);
        if (!string.IsNullOrWhiteSpace(workspaceTab.EditingQuickRequestId))
        {
            IsQuickRequestSaveDialogOpen = false;
        }

        NotifyShellState();
    }

    public void LoadWorkspaceItem(ExplorerItemViewModel? item)
    {
        if (item?.SourceCase is null)
        {
            return;
        }

        var source = item.SourceCase;
        var parentInterface = string.Equals(source.EntryType, RequestEntryTypes.HttpCase, StringComparison.OrdinalIgnoreCase)
            ? FindRequestById(source.ParentId)
            : null;

        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var targetTab = FindWorkspaceTabForSource(source) ?? ReuseActiveLandingOrCreateWorkspace();
        targetTab.ApplySavedRequest(source, parentInterface);

        if (string.Equals(source.EntryType, RequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase))
        {
            targetTab.HttpCaseName = ResolveLatestCaseName(source.Id);
        }

        ActivateWorkspaceTabCore(targetTab);
        StatusMessage = source.EntryType switch
        {
            RequestEntryTypes.HttpInterface => $"已加载 HTTP 接口：{source.Name}",
            RequestEntryTypes.HttpCase => $"已加载接口用例：{source.Name}",
            _ => $"已加载快捷请求：{source.Name}"
        };
        NotifyShellState();
    }

    public void LoadHistoryRequest(RequestHistoryItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var targetTab = ActiveWorkspaceTab?.IsLandingTab == true
            ? ActiveWorkspaceTab
            : FindFirstQuickRequestTab() ?? CreateWorkspaceTab(activate: false);

        targetTab ??= CreateWorkspaceTab(activate: false);
        targetTab.ConfigureAsQuickRequest();
        targetTab.ApplySnapshot(item.RequestSnapshot);
        if (item.ResponseSnapshot is not null)
        {
            targetTab.ResponseSection.ApplyResult(ResultModel<ResponseSnapshotDto>.Success(item.ResponseSnapshot), item.RequestSnapshot);
        }

        ActivateWorkspaceTabCore(targetTab);
        SelectedWorkspaceSection = WorkspaceSections.RequestHistory;
        StatusMessage = $"已加载历史请求：{item.Method} {item.Url}";
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowHttpDebugEditorMode()
    {
        if (ActiveWorkspaceTab is null || !ActiveWorkspaceTab.IsHttpInterfaceTab)
        {
            return;
        }

        ActiveWorkspaceTab.HttpEditorViewIndex = 0;
        NotifyWorkspaceEditorState();
    }

    [RelayCommand]
    private void ShowHttpDesignEditorMode()
    {
        if (ActiveWorkspaceTab is null || !ActiveWorkspaceTab.IsHttpInterfaceTab)
        {
            return;
        }

        ActiveWorkspaceTab.HttpEditorViewIndex = 1;
        NotifyWorkspaceEditorState();
    }

    [RelayCommand]
    private void ShowHttpDocumentPreviewMode()
    {
        if (ActiveWorkspaceTab is null || !ActiveWorkspaceTab.IsHttpInterfaceTab)
        {
            return;
        }

        ActiveWorkspaceTab.HttpEditorViewIndex = 2;
        NotifyWorkspaceEditorState();
    }

    [RelayCommand]
    private void ShowInterfaceManagement()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        EnsureLandingWorkspaceTab();
        StatusMessage = ActiveWorkspaceTab?.IsLandingTab == true
            ? "接口管理已就绪，可在中间新建 HTTP 接口或快捷请求。"
            : "接口管理已打开。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowRequestHistory()
    {
        SelectedWorkspaceSection = WorkspaceSections.RequestHistory;
        StatusMessage = HasHistory ? "这里展示当前项目的请求历史。" : "当前项目还没有请求历史。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowProjectSettings()
    {
        SelectedWorkspaceSection = WorkspaceSections.ProjectSettings;
        StatusMessage = ShowProjectSettingsImportDataSection
            ? "这里可以导入 Swagger 文档并查看已导入数据。"
            : "这里可以查看项目说明并进入环境设置。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowProjectOverviewSettings()
    {
        SelectedWorkspaceSection = WorkspaceSections.ProjectSettings;
        SelectedProjectSettingsSection = ProjectSettingsSections.Overview;
        StatusMessage = "这里可以查看项目说明、环境摘要和设置入口。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowProjectImportDataSettings()
    {
        SelectedWorkspaceSection = WorkspaceSections.ProjectSettings;
        SelectedProjectSettingsSection = ProjectSettingsSections.ImportData;
        StatusMessage = "这里可以导入 Swagger 文档并查看已导入数据。";
        NotifyShellState();
    }

    [RelayCommand]
    private void OpenProjectImportDialog()
    {
        SelectedWorkspaceSection = WorkspaceSections.ProjectSettings;
        SelectedProjectSettingsSection = ProjectSettingsSections.ImportData;
        SelectedImportDataMode = ImportDataModes.File;
        IsProjectImportDialogOpen = true;
        StatusMessage = "请选择 OpenAPI / Swagger 导入方式。";
        NotifyShellState();
    }

    [RelayCommand]
    private void CloseProjectImportDialog()
    {
        IsProjectImportDialogOpen = false;
        StatusMessage = "已返回导入数据页面。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowImportFileMode()
    {
        SelectedImportDataMode = ImportDataModes.File;
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowImportUrlMode()
    {
        SelectedImportDataMode = ImportDataModes.Url;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task PickSwaggerImportFileAsync()
    {
        if (IsImportDataBusy)
        {
            return;
        }

        var filePath = await _filePickerService.PickSwaggerJsonFileAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            SetImportDataStatus("未选择文件，当前保持原有导入配置。", ImportStatusStates.Info);
            NotifyShellState();
            return;
        }

        SelectedImportFilePath = filePath;
        SetImportDataStatus($"已选择 Swagger 文件：{Path.GetFileName(filePath)}", ImportStatusStates.Info);
        StatusMessage = $"已选择 Swagger 文件：{Path.GetFileName(filePath)}";
        NotifyShellState();
    }

    [RelayCommand]
    private async Task ImportSwaggerFileAsync()
    {
        if (!HasSelectedImportFile)
        {
            SetImportDataStatus("请先选择要导入的 Swagger/OpenAPI JSON 文件。", ImportStatusStates.Error);
            StatusMessage = "请先选择要导入的 Swagger 文件。";
            NotifyShellState();
            return;
        }

        await ImportSwaggerAsync(
            () => _apiWorkspaceService.ImportFromFileAsync(ProjectId, SelectedImportFilePath.Trim(), CancellationToken.None),
            document => $"Swagger 文件导入成功：{document.Name}");
    }

    [RelayCommand]
    private async Task ImportSwaggerUrlAsync()
    {
        var importTargetUrl = ImportUrl.Trim();
        if (string.IsNullOrWhiteSpace(importTargetUrl))
        {
            SetImportDataStatus("请输入 Swagger/OpenAPI 文档 URL。", ImportStatusStates.Error);
            StatusMessage = "请输入 Swagger 文档 URL。";
            NotifyShellState();
            return;
        }

        await ImportSwaggerAsync(
            () => _apiWorkspaceService.ImportFromUrlAsync(ProjectId, importTargetUrl, CancellationToken.None),
            document => $"Swagger URL 导入成功：{document.Name}");
    }

    [RelayCommand]
    private async Task RefreshImportedApiDocumentsAsync()
    {
        await LoadImportedDocumentsAsync();
        StatusMessage = HasImportedApiDocuments
            ? $"已刷新已导入数据，共 {ImportedApiDocuments.Count} 份文档。"
            : "已刷新导入数据，当前项目还没有 Swagger 文档。";
        NotifyShellState();
    }

    [RelayCommand]
    private void OpenQuickRequestWorkspace()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var tab = ReuseActiveLandingOrCreateWorkspace();
        tab.ConfigureAsQuickRequest();
        IsWorkspaceTabMenuOpen = false;
        ActivateWorkspaceTabCore(tab);
        StatusMessage = "快捷请求标签已打开。";
        NotifyShellState();
    }

    [RelayCommand]
    private void OpenHttpInterfaceWorkspace()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var tab = ReuseActiveLandingOrCreateWorkspace();
        tab.ConfigureAsHttpInterface();
        IsWorkspaceTabMenuOpen = false;
        ActivateWorkspaceTabCore(tab);
        StatusMessage = "HTTP 接口标签已打开。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ReturnToInterfaceHome()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var landingTab = FindLandingWorkspaceTab() ?? CreateWorkspaceTab(activate: false);
        landingTab.ConfigureAsLanding();
        landingTab.ShowInTabStrip = true;
        ActivateWorkspaceTabCore(landingTab);
        IsWorkspaceTabMenuOpen = false;
        StatusMessage = "已返回新建页。";
        NotifyShellState();
    }

    [RelayCommand]
    private void CreateWorkspaceTab()
    {
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        var tab = CreateWorkspaceTab(activate: true, showInTabStrip: true);
        tab.ConfigureAsLanding();
        IsWorkspaceTabMenuOpen = false;
        StatusMessage = "已新建一个工作标签。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ToggleWorkspaceTabMenu()
    {
        IsWorkspaceTabMenuOpen = !IsWorkspaceTabMenuOpen;
    }

    [RelayCommand]
    private void CloseCurrentWorkspaceFromMenu()
    {
        IsWorkspaceTabMenuOpen = false;
        CloseWorkspaceTab(ActiveWorkspaceTab);
    }

    [RelayCommand]
    private void CloseOtherWorkspaceTabs()
    {
        IsWorkspaceTabMenuOpen = false;
        if (ActiveWorkspaceTab is null)
        {
            return;
        }

        var tabsToRemove = WorkspaceTabs
            .Where(item => !ReferenceEquals(item, ActiveWorkspaceTab))
            .ToList();
        foreach (var tab in tabsToRemove)
        {
            DetachWorkspaceTab(tab);
            WorkspaceTabs.Remove(tab);
        }

        if (!WorkspaceTabs.Contains(ActiveWorkspaceTab))
        {
            EnsureLandingWorkspaceTab();
        }
        else
        {
            ActivateWorkspaceTabCore(ActiveWorkspaceTab);
        }

        StatusMessage = tabsToRemove.Count == 0 ? "当前没有其它标签页可关闭。" : "已关闭其它标签页。";
        NotifyShellState();
    }

    [RelayCommand]
    private void CloseAllWorkspaceTabs()
    {
        IsWorkspaceTabMenuOpen = false;
        if (WorkspaceTabs.Count == 0)
        {
            return;
        }

        foreach (var tab in WorkspaceTabs.ToList())
        {
            DetachWorkspaceTab(tab);
        }

        WorkspaceTabs.Clear();
        ActiveWorkspaceTab = null;
        EnsureLandingWorkspaceTab();
        StatusMessage = "已关闭全部标签页。";
        NotifyShellState();
    }

    [RelayCommand]
    private void ActivateWorkspaceTab(RequestWorkspaceTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        IsWorkspaceTabMenuOpen = false;
        ActivateWorkspaceTabCore(tab);
        SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
        StatusMessage = tab.IsLandingTab ? "已切换到新建页。" : $"已切换到标签：{tab.HeaderText}";
        NotifyShellState();
    }

    [RelayCommand]
    private void CloseWorkspaceTab(RequestWorkspaceTabViewModel? tab)
    {
        if (tab is null || !WorkspaceTabs.Contains(tab))
        {
            return;
        }

        IsWorkspaceTabMenuOpen = false;
        var removedIndex = WorkspaceTabs.IndexOf(tab);
        DetachWorkspaceTab(tab);
        WorkspaceTabs.Remove(tab);

        if (WorkspaceTabs.Count == 0)
        {
            EnsureLandingWorkspaceTab();
        }
        else if (ReferenceEquals(ActiveWorkspaceTab, tab))
        {
            var nextIndex = Math.Clamp(removedIndex - 1, 0, WorkspaceTabs.Count - 1);
            ActivateWorkspaceTabCore(WorkspaceTabs[nextIndex]);
        }

        StatusMessage = "工作标签已关闭。";
        NotifyShellState();
    }

    private async Task LoadWorkspaceAsync(string? preferredEnvironmentId = null)
    {
        UseCasesPanel.SetProjectContext(ProjectId);
        HistoryPanel.SetProjectContext(ProjectId);
        await EnvironmentPanel.LoadProjectAsync(ProjectId, preferredEnvironmentId);
        await ReloadSavedRequestsAsync();
        await HistoryPanel.LoadHistoryAsync();
        await LoadImportedDocumentsAsync(manageBusyState: false);
        EnsureLandingWorkspaceTab();
        NotifyShellState();
    }

    private async Task ImportSwaggerAsync(
        Func<Task<IResultModel<ApiDocumentDto>>> importAction,
        Func<ApiDocumentDto, string> buildSuccessMessage)
    {
        if (IsImportDataBusy)
        {
            return;
        }

        IsImportDataBusy = true;
        try
        {
            var result = await importAction();
            if (!result.IsSuccess || result.Data is null)
            {
                var failureMessage = string.IsNullOrWhiteSpace(result.Message)
                    ? "Swagger 导入失败，请检查文档格式后重试。"
                    : result.Message;
                SetImportDataStatus(failureMessage, ImportStatusStates.Error);
                StatusMessage = failureMessage;
                return;
            }

            await LoadImportedDocumentsAsync(manageBusyState: false);
            var successMessage = buildSuccessMessage(result.Data);
            SetImportDataStatus(successMessage, ImportStatusStates.Success);
            IsProjectImportDialogOpen = false;
            StatusMessage = successMessage;
        }
        finally
        {
            IsImportDataBusy = false;
            NotifyShellState();
        }
    }

    private async Task LoadImportedDocumentsAsync(bool manageBusyState = true)
    {
        if (manageBusyState)
        {
            IsImportDataBusy = true;
        }

        try
        {
            var documents = await _apiWorkspaceService.GetDocumentsAsync(ProjectId, CancellationToken.None);
            var documentTasks = documents.Select(async document =>
            {
                var endpoints = await _apiWorkspaceService.GetEndpointsAsync(document.Id, CancellationToken.None);
                return new ProjectImportedDocumentItemViewModel
                {
                    Id = document.Id,
                    Name = document.Name,
                    SourceTypeText = ResolveImportSourceTypeText(document.SourceType),
                    SourceValueText = string.IsNullOrWhiteSpace(document.SourceValue) ? "-" : document.SourceValue,
                    BaseUrlText = string.IsNullOrWhiteSpace(document.BaseUrl) ? "未解析出 BaseUrl" : document.BaseUrl,
                    ImportedAtText = document.ImportedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    EndpointCount = endpoints.Count
                };
            });

            var items = await Task.WhenAll(documentTasks);
            ImportedApiDocuments.Clear();
            foreach (var item in items)
            {
                ImportedApiDocuments.Add(item);
            }

            if (!HasImportedApiDocuments && ImportDataStatusState == ImportStatusStates.Info)
            {
                SetImportDataStatus("当前项目还没有导入 Swagger 数据，可先从文件或 URL 开始导入。", ImportStatusStates.Info);
            }
        }
        finally
        {
            if (manageBusyState)
            {
                IsImportDataBusy = false;
            }
        }
    }

    private async Task SaveQuickRequestAsync(RequestWorkspaceTabViewModel workspaceTab, string? requestNameOverride = null)
    {
        if (!HasAbsoluteHttpUrl(workspaceTab.RequestUrl))
        {
            StatusMessage = "快捷请求仅支持完整地址，请输入 http:// 或 https:// 开头的 URL。";
            NotifyShellState();
            return;
        }

        var requestName = string.IsNullOrWhiteSpace(requestNameOverride)
            ? workspaceTab.ResolveRequestName()
            : requestNameOverride.Trim();
        var snapshot = workspaceTab.BuildSnapshot(requestName);
        var result = await _requestCaseService.SaveAsync(new RequestCaseDto
        {
            Id = workspaceTab.EditingQuickRequestId,
            ProjectId = ProjectId,
            EntryType = RequestEntryTypes.QuickRequest,
            Name = requestName,
            GroupName = "快捷请求",
            Description = workspaceTab.ConfigTab.RequestDescription,
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        if (result.IsSuccess && result.Data is not null)
        {
            workspaceTab.EditingQuickRequestId = result.Data.Id;
            await ReloadSavedRequestsAsync();
            StatusMessage = "快捷请求已保存到左侧目录。";
        }
        else
        {
            StatusMessage = result.Message;
        }

        NotifyShellState();
    }

    private async Task SaveHttpInterfaceAsync(RequestWorkspaceTabViewModel workspaceTab)
    {
        var interfaceId = await EnsureHttpInterfaceSavedAsync(workspaceTab, reloadAfterSave: true);
        if (!string.IsNullOrWhiteSpace(interfaceId))
        {
            StatusMessage = "HTTP 接口已保存到默认模块。";
            NotifyShellState();
        }
    }

    private async Task<string?> EnsureHttpInterfaceSavedAsync(RequestWorkspaceTabViewModel workspaceTab, bool reloadAfterSave)
    {
        var snapshot = workspaceTab.BuildSnapshot();
        var result = await _requestCaseService.SaveAsync(new RequestCaseDto
        {
            Id = workspaceTab.EditingInterfaceId,
            ProjectId = ProjectId,
            EntryType = RequestEntryTypes.HttpInterface,
            Name = workspaceTab.ResolveRequestName(),
            GroupName = "接口",
            FolderPath = NormalizeFolderPath(workspaceTab.InterfaceFolderPath),
            Description = workspaceTab.ConfigTab.RequestDescription,
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        if (!result.IsSuccess || result.Data is null)
        {
            StatusMessage = result.Message;
            NotifyShellState();
            return null;
        }

        workspaceTab.EditingInterfaceId = result.Data.Id;
        if (reloadAfterSave)
        {
            await ReloadSavedRequestsAsync();
        }

        return result.Data.Id;
    }

    private async Task ReloadSavedRequestsAsync()
    {
        await UseCasesPanel.LoadCasesAsync();
        RebuildWorkspaceNavigation();
    }

    private void RebuildWorkspaceNavigation()
    {
        InterfaceTreeItems.Clear();
        QuickRequestTreeItems.Clear();

        var httpInterfaces = SavedRequests
            .Where(item => string.Equals(item.SourceCase.EntryType, RequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.SourceCase.FolderPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var httpCases = SavedRequests
            .Where(item => string.Equals(item.SourceCase.EntryType, RequestEntryTypes.HttpCase, StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.SourceCase.ParentId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAt).ToList(), StringComparer.OrdinalIgnoreCase);
        var quickRequests = SavedRequests
            .Where(item => string.Equals(item.SourceCase.EntryType, RequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.UpdatedAt)
            .ToList();
        var folderCounts = BuildFolderDescendantCounts(httpInterfaces);

        var interfaceRoot = new ExplorerItemViewModel
        {
            Title = "接口",
            Subtitle = string.Empty,
            IsGroup = true,
            NodeType = "interface-root"
        };
        InterfaceTreeItems.Add(interfaceRoot);

        var folderNodes = new Dictionary<string, ExplorerItemViewModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in httpInterfaces)
        {
            var parentNode = interfaceRoot;
            var folderPath = NormalizeFolderPath(item.SourceCase.FolderPath);
            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                var segments = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var currentPath = string.Empty;
                foreach (var segment in segments)
                {
                    currentPath = string.IsNullOrWhiteSpace(currentPath) ? segment : $"{currentPath}/{segment}";
                    if (!folderNodes.TryGetValue(currentPath, out var folderNode))
                    {
                        folderNode = new ExplorerItemViewModel
                        {
                            Title = BuildFolderTitle(segment, currentPath, folderCounts),
                            Subtitle = string.Empty,
                            IsGroup = true,
                            NodeType = "folder"
                        };
                        folderNodes[currentPath] = folderNode;
                        parentNode.Children.Add(folderNode);
                    }

                    parentNode = folderNode;
                }
            }

            var interfaceNode = new ExplorerItemViewModel
            {
                Title = BuildInterfaceTitle(item.Name, httpCases.TryGetValue(item.Id, out var interfaceCases) ? interfaceCases.Count : 0),
                Subtitle = string.Empty,
                NodeType = RequestEntryTypes.HttpInterface,
                CanLoad = true,
                SourceCase = item.SourceCase
            };
            parentNode.Children.Add(interfaceNode);

            if (httpCases.TryGetValue(item.Id, out var cases))
            {
                foreach (var caseItem in cases)
                {
                    interfaceNode.Children.Add(new ExplorerItemViewModel
                    {
                        Title = caseItem.Name,
                        Subtitle = string.Empty,
                        NodeType = RequestEntryTypes.HttpCase,
                        CanLoad = true,
                        SourceCase = caseItem.SourceCase
                    });
                }
            }
        }

        foreach (var item in quickRequests)
        {
            QuickRequestTreeItems.Add(new ExplorerItemViewModel
            {
                Title = item.Name,
                Subtitle = string.Empty,
                NodeType = RequestEntryTypes.QuickRequest,
                CanLoad = true,
                SourceCase = item.SourceCase
            });
        }

        OnPropertyChanged(nameof(InterfaceCatalogItems));
    }

    private ProjectEnvironmentDto BuildExecutionEnvironment()
    {
        var environment = EnvironmentPanel.GetSelectedEnvironmentDto();
        if (environment is not null)
        {
            return environment;
        }

        return new ProjectEnvironmentDto
        {
            Id = string.Empty,
            ProjectId = ProjectId,
            Name = "未配置环境",
            BaseUrl = string.Empty,
            IsActive = false,
            SortOrder = 0
        };
    }

    private void SetImportDataStatus(string message, string statusState)
    {
        ImportDataStatusText = message;
        ImportDataStatusState = statusState;
    }

    private static string ResolveImportSourceTypeText(string sourceType)
    {
        return string.Equals(sourceType, "URL", StringComparison.OrdinalIgnoreCase)
            ? "URL 导入"
            : "文件上传";
    }

    private void OpenQuickRequestSaveDialog(RequestWorkspaceTabViewModel workspaceTab)
    {
        var fallbackName = string.IsNullOrWhiteSpace(workspaceTab.ConfigTab.RequestName)
            ? workspaceTab.ResolveRequestName()
            : workspaceTab.ConfigTab.RequestName.Trim();
        QuickRequestSaveName = fallbackName;
        QuickRequestSaveDescription = workspaceTab.ConfigTab.RequestDescription;
        IsQuickRequestSaveDialogOpen = true;
        StatusMessage = "请输入快捷请求名称后再保存。";
        NotifyShellState();
    }

    private RequestWorkspaceTabViewModel ReuseActiveLandingOrCreateWorkspace()
    {
        if (ActiveWorkspaceTab?.IsLandingTab == true)
        {
            return ActiveWorkspaceTab;
        }

        return CreateWorkspaceTab(activate: false);
    }

    private RequestWorkspaceTabViewModel CreateWorkspaceTab(bool activate, bool showInTabStrip = true)
    {
        var tab = new RequestWorkspaceTabViewModel();
        tab.ConfigureAsLanding();
        tab.ShowInTabStrip = showInTabStrip;
        AttachWorkspaceTab(tab);
        WorkspaceTabs.Add(tab);
        if (activate)
        {
            ActivateWorkspaceTabCore(tab);
        }

        return tab;
    }

    private void EnsureLandingWorkspaceTab()
    {
        if (WorkspaceTabs.Count == 0)
        {
            var tab = CreateWorkspaceTab(activate: false, showInTabStrip: false);
            tab.ConfigureAsLanding();
            tab.ShowInTabStrip = false;
            ActivateWorkspaceTabCore(tab);
            return;
        }

        if (ActiveWorkspaceTab is null)
        {
            ActivateWorkspaceTabCore(WorkspaceTabs[0]);
        }
    }

    private RequestWorkspaceTabViewModel? FindLandingWorkspaceTab()
    {
        return WorkspaceTabs
            .Where(item => item.IsLandingTab)
            .OrderByDescending(item => item.ShowInTabStrip)
            .FirstOrDefault();
    }

    private RequestWorkspaceTabViewModel? FindFirstQuickRequestTab()
    {
        return WorkspaceTabs.FirstOrDefault(item => item.IsQuickRequestTab);
    }

    private RequestWorkspaceTabViewModel? FindWorkspaceTabForSource(RequestCaseDto source)
    {
        return WorkspaceTabs.FirstOrDefault(item =>
            string.Equals(source.EntryType, RequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase)
                ? string.Equals(item.EditingQuickRequestId, source.Id, StringComparison.OrdinalIgnoreCase)
                : string.Equals(source.EntryType, RequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase)
                    ? string.Equals(item.EditingInterfaceId, source.Id, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(item.EditingCaseId, source.Id, StringComparison.OrdinalIgnoreCase));
    }

    private void AttachWorkspaceTab(RequestWorkspaceTabViewModel tab)
    {
        tab.PropertyChanged += OnWorkspaceTabPropertyChanged;
        tab.ConfigTab.PropertyChanged += OnWorkspaceConfigPropertyChanged;
        tab.ConfigTab.QueryParameters.CollectionChanged += OnWorkspaceConfigCollectionChanged;
        tab.ConfigTab.Headers.CollectionChanged += OnWorkspaceConfigCollectionChanged;
        tab.ConfigTab.FormFields.CollectionChanged += OnWorkspaceConfigCollectionChanged;
    }

    private void DetachWorkspaceTab(RequestWorkspaceTabViewModel tab)
    {
        tab.PropertyChanged -= OnWorkspaceTabPropertyChanged;
        tab.ConfigTab.PropertyChanged -= OnWorkspaceConfigPropertyChanged;
        tab.ConfigTab.QueryParameters.CollectionChanged -= OnWorkspaceConfigCollectionChanged;
        tab.ConfigTab.Headers.CollectionChanged -= OnWorkspaceConfigCollectionChanged;
        tab.ConfigTab.FormFields.CollectionChanged -= OnWorkspaceConfigCollectionChanged;
    }

    private void ActivateWorkspaceTabCore(RequestWorkspaceTabViewModel tab)
    {
        ActiveWorkspaceTab = tab;
    }

    private RequestCaseDto? FindRequestById(string id)
    {
        return SavedRequests
            .Select(item => item.SourceCase)
            .FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveLatestCaseName(string interfaceId)
    {
        return SavedRequests
            .Where(item => string.Equals(item.SourceCase.EntryType, RequestEntryTypes.HttpCase, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.Equals(item.SourceCase.ParentId, interfaceId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => item.Name)
            .FirstOrDefault()
            ?? "成功";
    }

    private static string BuildHttpCaseName(RequestWorkspaceTabViewModel workspaceTab)
    {
        return string.IsNullOrWhiteSpace(workspaceTab.HttpCaseName)
            ? "成功"
            : workspaceTab.HttpCaseName.Trim();
    }

    private static bool HasAbsoluteHttpUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private string BuildHttpDocumentUrl()
    {
        var path = RequestUrl.Trim();
        string resolvedUrl;
        if (string.IsNullOrWhiteSpace(path))
        {
            resolvedUrl = string.IsNullOrWhiteSpace(CurrentHttpInterfaceBaseUrl)
                ? "未配置 BaseUrl / 未填写路径"
                : $"{CurrentHttpInterfaceBaseUrl.TrimEnd('/')}/";
        }
        else if (Uri.TryCreate(path, UriKind.Absolute, out _))
        {
            resolvedUrl = path;
        }
        else if (string.IsNullOrWhiteSpace(CurrentHttpInterfaceBaseUrl))
        {
            resolvedUrl = $"未配置 BaseUrl {path}";
        }
        else
        {
            resolvedUrl = $"{CurrentHttpInterfaceBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
        }

        var queryString = string.Join("&", ConfigTab.QueryParameters
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Select(item =>
                $"{Uri.EscapeDataString(item.Name.Trim())}={Uri.EscapeDataString((item.Value ?? string.Empty).Trim())}"));

        if (string.IsNullOrWhiteSpace(queryString))
        {
            return resolvedUrl;
        }

        return resolvedUrl.Contains('?', StringComparison.Ordinal)
            ? $"{resolvedUrl}&{queryString}"
            : $"{resolvedUrl}?{queryString}";
    }

    private string BuildHttpDocumentCurlSnippet()
    {
        var url = BuildHttpDocumentUrl();
        var resolvedUrl = url.StartsWith("未配置", StringComparison.OrdinalIgnoreCase)
            ? RequestUrl.Trim()
            : url;

        var builder = new StringBuilder();
        builder.Append("curl --request ")
            .Append(SelectedMethod)
            .Append(" \"")
            .Append(EscapeCurlValue(resolvedUrl))
            .Append('"');

        foreach (var header in ConfigTab.Headers.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            builder.Append(" \\\n  --header \"")
                .Append(EscapeCurlValue(header.Name.Trim()))
                .Append(": ")
                .Append(EscapeCurlValue((header.Value ?? string.Empty).Trim()))
                .Append('"');
        }

        var bodyContent = ResolveHttpDocumentBodyContent();
        if (!string.IsNullOrWhiteSpace(bodyContent))
        {
            builder.Append(" \\\n  --data-raw \"")
                .Append(EscapeCurlValue(bodyContent))
                .Append('"');
        }

        return builder.ToString();
    }

    private string ResolveHttpDocumentBodyContent()
    {
        if (ConfigTab.SelectedBodyMode is BodyModes.FormData or BodyModes.FormUrlEncoded)
        {
            return string.Join("&", ConfigTab.FormFields
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .Select(item =>
                    $"{Uri.EscapeDataString(item.Name.Trim())}={Uri.EscapeDataString((item.Value ?? string.Empty).Trim())}"));
        }

        return ConfigTab.HasBodyContent ? ConfigTab.RequestBody.Trim() : string.Empty;
    }

    private static string EscapeCurlValue(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string NormalizeFolderPath(string folderPath)
    {
        var normalized = folderPath.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return string.Join('/',
            normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static Dictionary<string, int> BuildFolderDescendantCounts(IReadOnlyCollection<RequestCaseItemViewModel> httpInterfaces)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in httpInterfaces)
        {
            var folderPath = NormalizeFolderPath(item.SourceCase.FolderPath);
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                continue;
            }

            var segments = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var currentPath = string.Empty;
            foreach (var segment in segments)
            {
                currentPath = string.IsNullOrWhiteSpace(currentPath) ? segment : $"{currentPath}/{segment}";
                counts[currentPath] = counts.TryGetValue(currentPath, out var count) ? count + 1 : 1;
            }
        }

        return counts;
    }

    private static string BuildFolderTitle(string segment, string path, IReadOnlyDictionary<string, int> folderCounts)
    {
        return folderCounts.TryGetValue(path, out var count) && count > 0
            ? $"{segment} ({count})"
            : segment;
    }

    private static string BuildInterfaceTitle(string name, int caseCount)
    {
        return caseCount > 0 ? $"{name} ({caseCount})" : name;
    }

    private void OnSelectedEnvironmentChanged(ProjectEnvironmentItemViewModel? environment)
    {
        StatusMessage = environment is null
            ? "当前项目尚未配置环境。"
            : $"当前环境已切换为：{environment.Name}";
        NotifyWorkspaceEditorState();
    }

    private void OnWorkspaceTabsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<RequestWorkspaceTabViewModel>())
            {
                item.IsActive = ReferenceEquals(item, ActiveWorkspaceTab);
            }
        }

        OnPropertyChanged(nameof(WorkspaceTabs));
        OnPropertyChanged(nameof(VisibleWorkspaceTabs));
        NotifyShellState();
    }

    private void OnWorkspaceTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not RequestWorkspaceTabViewModel tab)
        {
            return;
        }

        if (!ReferenceEquals(tab, ActiveWorkspaceTab))
        {
            if (e.PropertyName == nameof(RequestWorkspaceTabViewModel.HeaderText))
            {
                OnPropertyChanged(nameof(VisibleWorkspaceTabs));
                NotifyShellState();
            }

            return;
        }

        if (e.PropertyName is nameof(RequestWorkspaceTabViewModel.SelectedMethod)
            or nameof(RequestWorkspaceTabViewModel.RequestUrl)
            or nameof(RequestWorkspaceTabViewModel.InterfaceFolderPath)
            or nameof(RequestWorkspaceTabViewModel.HttpCaseName)
            or nameof(RequestWorkspaceTabViewModel.EntryType)
            or nameof(RequestWorkspaceTabViewModel.ShowInTabStrip)
            or nameof(RequestWorkspaceTabViewModel.HeaderText))
        {
            OnPropertyChanged(nameof(VisibleWorkspaceTabs));
            NotifyWorkspaceEditorState();
        }
    }

    private void OnWorkspaceConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var tab = WorkspaceTabs.FirstOrDefault(item => ReferenceEquals(item.ConfigTab, sender));
        if (tab is null || !ReferenceEquals(tab, ActiveWorkspaceTab))
        {
            return;
        }

        NotifyWorkspaceEditorState();
    }

    private void OnWorkspaceConfigCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var tab = WorkspaceTabs.FirstOrDefault(item =>
            ReferenceEquals(item.ConfigTab.QueryParameters, sender)
            || ReferenceEquals(item.ConfigTab.Headers, sender)
            || ReferenceEquals(item.ConfigTab.FormFields, sender));
        if (tab is null || !ReferenceEquals(tab, ActiveWorkspaceTab))
        {
            return;
        }

        NotifyWorkspaceEditorState();
    }

    private void NotifyWorkspaceEditorState()
    {
        OnPropertyChanged(nameof(ConfigTab));
        OnPropertyChanged(nameof(ResponseSection));
        OnPropertyChanged(nameof(SelectedMethod));
        OnPropertyChanged(nameof(RequestUrl));
        OnPropertyChanged(nameof(CurrentInterfaceFolderPath));
        OnPropertyChanged(nameof(CurrentHttpCaseName));
        OnPropertyChanged(nameof(CurrentHttpInterfaceName));
        OnPropertyChanged(nameof(CurrentHttpInterfaceDisplayName));
        OnPropertyChanged(nameof(IsHttpDebugEditorMode));
        OnPropertyChanged(nameof(IsHttpDesignEditorMode));
        OnPropertyChanged(nameof(IsHttpDocumentPreviewMode));
        OnPropertyChanged(nameof(ShowHttpWorkbenchContent));
        OnPropertyChanged(nameof(ShowHttpDocumentPreviewContent));
        OnPropertyChanged(nameof(HasHttpDocumentParameters));
        OnPropertyChanged(nameof(HasHttpDocumentHeaders));
        OnPropertyChanged(nameof(HasHttpDocumentRequestDetails));
        OnPropertyChanged(nameof(ShowHttpDocumentRequestEmpty));
        OnPropertyChanged(nameof(CurrentHttpDocumentBodyModeText));
        OnPropertyChanged(nameof(CurrentHttpDocumentUrl));
        OnPropertyChanged(nameof(CurrentHttpDocumentResponseSummary));
        OnPropertyChanged(nameof(CurrentHttpDocumentBodyPreview));
        OnPropertyChanged(nameof(CurrentHttpDocumentCurlSnippet));
        NotifyShellState();
    }

    partial void OnSelectedWorkspaceSectionChanged(string value)
    {
        SyncWorkspaceNavigationSelection();
        OnPropertyChanged(nameof(IsInterfaceManagementSection));
        OnPropertyChanged(nameof(IsRequestHistorySection));
        OnPropertyChanged(nameof(IsProjectSettingsSection));
        OnPropertyChanged(nameof(ShowProjectSettingsOverviewSection));
        OnPropertyChanged(nameof(ShowProjectSettingsImportDataSection));
        OnPropertyChanged(nameof(ShowInterfaceManagementLanding));
        OnPropertyChanged(nameof(ShowRequestEditorWorkspace));
    }

    partial void OnSelectedProjectSettingsSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsProjectSettingsOverviewSelected));
        OnPropertyChanged(nameof(IsProjectSettingsImportDataSelected));
        OnPropertyChanged(nameof(ShowProjectSettingsOverviewSection));
        OnPropertyChanged(nameof(ShowProjectSettingsImportDataSection));
        OnPropertyChanged(nameof(CurrentProjectSettingsTitle));
        OnPropertyChanged(nameof(CurrentProjectSettingsSubtitle));
    }

    partial void OnSelectedImportDataModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsImportFileMode));
        OnPropertyChanged(nameof(IsImportUrlMode));
    }

    partial void OnSelectedImportFilePathChanged(string value)
    {
        OnPropertyChanged(nameof(HasSelectedImportFile));
        OnPropertyChanged(nameof(SelectedImportFileName));
        OnPropertyChanged(nameof(SelectedImportFileSummary));
    }

    partial void OnImportDataStatusStateChanged(string value)
    {
        OnPropertyChanged(nameof(ShowImportStatusInfo));
        OnPropertyChanged(nameof(ShowImportStatusSuccess));
        OnPropertyChanged(nameof(ShowImportStatusError));
        OnPropertyChanged(nameof(ShowProjectImportDialogStatus));
    }

    partial void OnActiveWorkspaceTabChanged(RequestWorkspaceTabViewModel? oldValue, RequestWorkspaceTabViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsActive = false;
        }

        if (newValue is not null)
        {
            newValue.IsActive = true;
        }

        if (newValue is null || !newValue.IsQuickRequestTab)
        {
            IsQuickRequestSaveDialogOpen = false;
        }

        NotifyWorkspaceEditorState();
    }

    private void NotifyShellState()
    {
        OnPropertyChanged(nameof(TabTitle));
        OnPropertyChanged(nameof(ProjectSummary));
        OnPropertyChanged(nameof(CurrentEnvironmentLabel));
        OnPropertyChanged(nameof(CurrentBaseUrlText));
        OnPropertyChanged(nameof(HasEnvironmentContext));
        OnPropertyChanged(nameof(HasSavedRequests));
        OnPropertyChanged(nameof(VisibleWorkspaceTabs));
        OnPropertyChanged(nameof(HasQuickRequestEntries));
        OnPropertyChanged(nameof(HasInterfaceEntries));
        OnPropertyChanged(nameof(ShowInterfaceEntriesEmptyState));
        OnPropertyChanged(nameof(ShowQuickRequestEntriesEmptyState));
        OnPropertyChanged(nameof(HasHistory));
        OnPropertyChanged(nameof(ShowSavedRequestsEmptyState));
        OnPropertyChanged(nameof(ShowHistoryEmptyState));
        OnPropertyChanged(nameof(IsInterfaceManagementSection));
        OnPropertyChanged(nameof(IsRequestHistorySection));
        OnPropertyChanged(nameof(IsProjectSettingsSection));
        OnPropertyChanged(nameof(IsProjectSettingsOverviewSelected));
        OnPropertyChanged(nameof(IsProjectSettingsImportDataSelected));
        OnPropertyChanged(nameof(ShowProjectSettingsOverviewSection));
        OnPropertyChanged(nameof(ShowProjectSettingsImportDataSection));
        OnPropertyChanged(nameof(IsImportFileMode));
        OnPropertyChanged(nameof(IsImportUrlMode));
        OnPropertyChanged(nameof(ShowProjectImportDialogStatus));
        OnPropertyChanged(nameof(HasSelectedImportFile));
        OnPropertyChanged(nameof(SelectedImportFileName));
        OnPropertyChanged(nameof(SelectedImportFileSummary));
        OnPropertyChanged(nameof(HasImportedApiDocuments));
        OnPropertyChanged(nameof(ShowImportedApiDocumentsEmptyState));
        OnPropertyChanged(nameof(ImportedApiDocumentCountText));
        OnPropertyChanged(nameof(CurrentProjectSettingsTitle));
        OnPropertyChanged(nameof(CurrentProjectSettingsSubtitle));
        OnPropertyChanged(nameof(ShowImportStatusInfo));
        OnPropertyChanged(nameof(ShowImportStatusSuccess));
        OnPropertyChanged(nameof(ShowImportStatusError));
        OnPropertyChanged(nameof(IsQuickRequestEditor));
        OnPropertyChanged(nameof(IsHttpInterfaceEditor));
        OnPropertyChanged(nameof(IsRequestEditorOpen));
        OnPropertyChanged(nameof(ShowInterfaceManagementLanding));
        OnPropertyChanged(nameof(ShowRequestEditorWorkspace));
        OnPropertyChanged(nameof(SavedRequestCountText));
        OnPropertyChanged(nameof(HistoryCountText));
        OnPropertyChanged(nameof(EnvironmentCountText));
        OnPropertyChanged(nameof(ProjectSettingsDescription));
        OnPropertyChanged(nameof(InterfaceSectionHint));
        OnPropertyChanged(nameof(QuickRequestSectionHint));
        OnPropertyChanged(nameof(CurrentEditorTitle));
        OnPropertyChanged(nameof(CurrentEditorDescription));
        OnPropertyChanged(nameof(CurrentEditorPrimaryActionText));
        OnPropertyChanged(nameof(CurrentEditorUrlWatermark));
        OnPropertyChanged(nameof(ShowEditorBaseUrlPrefix));
        OnPropertyChanged(nameof(CurrentEditorBaseUrlPrefix));
        OnPropertyChanged(nameof(CurrentHttpInterfaceBaseUrl));
        OnPropertyChanged(nameof(ShowSaveHttpCaseAction));
        OnPropertyChanged(nameof(CurrentEditorBaseUrlCaption));
        OnPropertyChanged(nameof(CurrentResponseValidationResultText));
        ShellStateChanged?.Invoke(this);
    }

    private void SyncWorkspaceNavigationSelection()
    {
        var selectedItem = WorkspaceNavigationItems.FirstOrDefault(item =>
            string.Equals(item.SectionKey, SelectedWorkspaceSection, StringComparison.OrdinalIgnoreCase));
        if (!ReferenceEquals(SelectedWorkspaceNavigationItem, selectedItem))
        {
            SelectedWorkspaceNavigationItem = selectedItem;
        }
    }
}
