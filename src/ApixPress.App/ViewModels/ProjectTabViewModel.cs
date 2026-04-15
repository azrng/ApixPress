using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel : ViewModelBase
{
    private const string ImportedEndpointKeyPrefix = "swagger-import:";

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

    private readonly IRequestCaseService _requestCaseService;
    private readonly IRequestExecutionService _requestExecutionService;
    private readonly IRequestHistoryService _requestHistoryService;
    private readonly IApiWorkspaceService _apiWorkspaceService;
    private readonly IFilePickerService _filePickerService;
    private readonly RequestWorkspaceTabViewModel _fallbackWorkspaceTab;
    private CancellationTokenSource? _importCancellationTokenSource;
    private CancellationTokenSource? _sendRequestCancellationTokenSource;
    private PendingImportRequest? _pendingImportRequest;
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
    public bool HasQuickRequestEntries => SavedRequests.Any(item => string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase));
    public bool HasInterfaceEntries => SavedRequests.Any(item => string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase));
    public bool ShowInterfaceEntriesEmptyState => !HasInterfaceEntries;
    public bool ShowQuickRequestEntriesEmptyState => !HasQuickRequestEntries;
    public bool ShowSavedRequestsEmptyState => !HasQuickRequestEntries && !HasInterfaceEntries;
    public bool ShowHistoryEmptyState => !HasHistory;
    public bool IsInterfaceManagementSection => SelectedWorkspaceSection == WorkspaceSections.InterfaceManagement;
    public bool IsRequestHistorySection => SelectedWorkspaceSection == WorkspaceSections.RequestHistory;
    public bool IsProjectSettingsSection => SelectedWorkspaceSection == WorkspaceSections.ProjectSettings;
    public bool IsQuickRequestEditor => ActiveWorkspaceTab?.IsQuickRequestTab ?? false;
    public bool IsHttpInterfaceEditor => ActiveWorkspaceTab?.IsHttpInterfaceTab ?? false;
    public bool IsRequestEditorOpen => ActiveWorkspaceTab is not null && !ActiveWorkspaceTab.IsLandingTab;
    public bool ShowInterfaceManagementLanding => IsInterfaceManagementSection && (ActiveWorkspaceTab?.IsLandingTab ?? true);
    public bool ShowRequestEditorWorkspace => IsInterfaceManagementSection && IsRequestEditorOpen;
    public string SavedRequestCountText => SavedRequests.Count(item =>
        string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase)).ToString();
    public string HistoryCountText => RequestHistory.Count.ToString();
    public string EnvironmentCountText => EnvironmentPanel.Environments.Count.ToString();
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
    public string CurrentQuickRequestName
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
                return "未命名请求";
            }

            return ActiveWorkspaceTab.IsQuickRequestTab
                && string.Equals(currentName, ActiveWorkspaceTab.ResolveGeneratedRequestName(), StringComparison.Ordinal)
                ? "未命名请求"
                : currentName;
        }
        set
        {
            if (ActiveWorkspaceTab is null)
            {
                return;
            }

            var normalizedValue = string.Equals(value?.Trim(), "未命名请求", StringComparison.Ordinal)
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
    public bool IsHttpDebugEditorMode => ActiveWorkspaceTab?.IsHttpDebugView ?? false;
    public bool IsHttpDesignEditorMode => ActiveWorkspaceTab?.IsHttpDesignView ?? false;
    public bool IsHttpDocumentPreviewMode => ActiveWorkspaceTab?.IsHttpDocumentPreviewView ?? false;
    public bool ShowHttpWorkbenchContent => IsHttpInterfaceEditor && !IsHttpDocumentPreviewMode;
    public bool ShowHttpDocumentPreviewContent => IsHttpInterfaceEditor && IsHttpDocumentPreviewMode;
    public bool ShowSaveHttpCaseAction => IsHttpInterfaceEditor;
    public string CurrentEditorBaseUrlCaption => IsHttpInterfaceEditor
        ? (string.IsNullOrWhiteSpace(EnvironmentPanel.SelectedEnvironment?.BaseUrl) ? "当前环境未配置 BaseUrl" : EnvironmentPanel.SelectedEnvironment.BaseUrl)
        : IsQuickRequestEditor
            ? "完整地址"
            : string.Empty;
    public bool HasHttpDocumentParameters => ConfigTab.QueryParameters.Count > 0;
    public bool HasHttpDocumentHeaders => ConfigTab.Headers.Count > 0;
    public bool HasHttpDocumentRequestDetails => HasHttpDocumentParameters || HasHttpDocumentHeaders || ConfigTab.HasBodyContent;
    public bool ShowHttpDocumentRequestEmpty => !HasHttpDocumentRequestDetails;
    public string CurrentHttpDocumentBodyModeText => ConfigTab.HasBodyContent
        ? (ConfigTab.SelectedBodyModeOption?.DisplayName ?? ConfigTab.SelectedBodyMode)
        : "无";
    public string CurrentHttpDocumentUrl => ProjectHttpDocumentFormatter.BuildUrl(RequestUrl, CurrentHttpInterfaceBaseUrl, ConfigTab.QueryParameters);
    public string CurrentHttpDocumentResponseSummary => ResponseSection.HasResponse
        ? CurrentResponseValidationResultText
        : "等待调试后生成响应示例";
    public string CurrentHttpDocumentBodyPreview => ResponseSection.HasResponse && !string.IsNullOrWhiteSpace(ResponseSection.BodyText)
        ? ResponseSection.BodyText
        : "{ }";
    public string CurrentHttpDocumentCurlSnippet => ProjectHttpDocumentFormatter.BuildCurlSnippet(SelectedMethod, RequestUrl, CurrentHttpInterfaceBaseUrl, ConfigTab);
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
        get => ActiveWorkspaceTab?.InterfaceFolderPath ?? "默认模块";
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
    private bool isWorkspaceDeleteConfirmDialogOpen;

    [ObservableProperty]
    private bool isImportOverwriteConfirmDialogOpen;

    [ObservableProperty]
    private string quickRequestSaveName = string.Empty;

    [ObservableProperty]
    private string quickRequestSaveDescription = string.Empty;

    [ObservableProperty]
    private bool responseValidationEnabled = true;

    [ObservableProperty]
    private bool isWorkspaceTabMenuOpen;

    [ObservableProperty]
    private ExplorerItemViewModel? pendingDeleteWorkspaceItem;

    [ObservableProperty]
    private ApiImportPreviewDto? pendingImportPreview;
}
