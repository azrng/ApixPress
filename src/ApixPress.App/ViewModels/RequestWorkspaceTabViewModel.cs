using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class RequestWorkspaceTabViewModel : ViewModelBase
{
    private const string DefaultInterfaceFolderName = "默认模块";

    private static class WorkspaceEntryTypes
    {
        public const string Landing = "landing";
        public const string QuickRequest = "quick-request";
        public const string HttpInterface = "http-interface";
    }

    public RequestWorkspaceTabViewModel()
    {
        ConfigTab = new RequestConfigTabViewModel(null);
        ResponseSection = new ResponseSectionViewModel();

        ConfigTab.PropertyChanged += OnConfigTabPropertyChanged;
        UpdateTabHeader();
    }

    public RequestConfigTabViewModel ConfigTab { get; }
    public ResponseSectionViewModel ResponseSection { get; }

    [ObservableProperty]
    private string id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string entryType = WorkspaceEntryTypes.Landing;

    [ObservableProperty]
    private string selectedMethod = "GET";

    [ObservableProperty]
    private string requestUrl = string.Empty;

    [ObservableProperty]
    private string interfaceFolderPath = DefaultInterfaceFolderName;

    [ObservableProperty]
    private string httpCaseName = "成功";

    [ObservableProperty]
    private string sourceEndpointId = string.Empty;

    [ObservableProperty]
    private string editingQuickRequestId = string.Empty;

    [ObservableProperty]
    private string editingInterfaceId = string.Empty;

    [ObservableProperty]
    private string editingCaseId = string.Empty;

    [ObservableProperty]
    private int httpEditorViewIndex;

    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private bool showInTabStrip = true;

    [ObservableProperty]
    private string headerText = "新建...";

    public bool IsLandingTab => EntryType == WorkspaceEntryTypes.Landing;
    public bool IsQuickRequestTab => EntryType == WorkspaceEntryTypes.QuickRequest;
    public bool IsHttpInterfaceTab => EntryType == WorkspaceEntryTypes.HttpInterface;
    public bool IsHttpDebugView => IsHttpInterfaceTab && HttpEditorViewIndex == 0;
    public bool IsHttpDesignView => IsHttpInterfaceTab && HttpEditorViewIndex == 1;
    public bool IsHttpDocumentPreviewView => IsHttpInterfaceTab && HttpEditorViewIndex == 2;
    public bool ShowMethodBadge => IsHttpInterfaceTab;
    public string MethodBadgeText => SelectedMethod;
    public string EditorTitle => IsHttpInterfaceTab ? "HTTP 接口" : IsQuickRequestTab ? "快捷请求" : "新建...";
    public string EditorDescription => IsHttpInterfaceTab
        ? "HTTP 接口会自动使用当前环境的 BaseUrl，请在右侧输入相对路径。"
        : IsQuickRequestTab
            ? "快捷请求不固定 BaseUrl，请输入完整的 http:// 或 https:// 地址。"
            : "从下方卡片中选择要创建的工作内容。";
    public string PrimaryActionText => IsHttpInterfaceTab ? "保存接口" : "保存";
    public string UrlWatermark => IsHttpInterfaceTab ? "接口路径，如 /起始" : "输入完整地址，如 https://api.example.com/users";

    public void ConfigureAsLanding()
    {
        EntryType = WorkspaceEntryTypes.Landing;
        SelectedMethod = "GET";
        RequestUrl = string.Empty;
        InterfaceFolderPath = DefaultInterfaceFolderName;
        HttpCaseName = "成功";
        SourceEndpointId = string.Empty;
        EditingQuickRequestId = string.Empty;
        EditingInterfaceId = string.Empty;
        EditingCaseId = string.Empty;
        HttpEditorViewIndex = 0;
        ConfigTab.Reset();
        ResponseSection.Reset();
        UpdateTabHeader();
    }

    public void ConfigureAsQuickRequest()
    {
        EntryType = WorkspaceEntryTypes.QuickRequest;
        SelectedMethod = "GET";
        RequestUrl = string.Empty;
        InterfaceFolderPath = DefaultInterfaceFolderName;
        HttpCaseName = "成功";
        SourceEndpointId = string.Empty;
        EditingQuickRequestId = string.Empty;
        EditingInterfaceId = string.Empty;
        EditingCaseId = string.Empty;
        HttpEditorViewIndex = 0;
        ConfigTab.Reset();
        ResponseSection.Reset();
        UpdateTabHeader();
    }

    public void ConfigureAsHttpInterface()
    {
        EntryType = WorkspaceEntryTypes.HttpInterface;
        SelectedMethod = "GET";
        RequestUrl = string.Empty;
        InterfaceFolderPath = DefaultInterfaceFolderName;
        HttpCaseName = "成功";
        SourceEndpointId = string.Empty;
        EditingQuickRequestId = string.Empty;
        EditingInterfaceId = string.Empty;
        EditingCaseId = string.Empty;
        HttpEditorViewIndex = 0;
        ConfigTab.Reset();
        ResponseSection.Reset();
        UpdateTabHeader();
    }

    public void ApplySavedRequest(RequestCaseDto source, RequestCaseDto? parentInterface = null)
    {
        switch (source.EntryType)
        {
            case WorkspaceEntryTypes.HttpInterface:
                EntryType = WorkspaceEntryTypes.HttpInterface;
                EditingInterfaceId = source.Id;
                EditingCaseId = string.Empty;
                EditingQuickRequestId = string.Empty;
                InterfaceFolderPath = string.IsNullOrWhiteSpace(source.FolderPath) ? DefaultInterfaceFolderName : source.FolderPath;
                HttpCaseName = "成功";
                SourceEndpointId = source.RequestSnapshot.EndpointId;
                break;
            case "http-case":
                EntryType = WorkspaceEntryTypes.HttpInterface;
                EditingCaseId = source.Id;
                EditingQuickRequestId = string.Empty;
                EditingInterfaceId = parentInterface?.Id ?? source.ParentId;
                InterfaceFolderPath = !string.IsNullOrWhiteSpace(parentInterface?.FolderPath)
                    ? parentInterface.FolderPath
                    : string.IsNullOrWhiteSpace(source.FolderPath)
                        ? DefaultInterfaceFolderName
                        : source.FolderPath;
                HttpCaseName = source.Name;
                SourceEndpointId = !string.IsNullOrWhiteSpace(source.RequestSnapshot.EndpointId)
                    ? source.RequestSnapshot.EndpointId
                    : parentInterface?.RequestSnapshot.EndpointId ?? string.Empty;
                break;
            default:
                EntryType = WorkspaceEntryTypes.QuickRequest;
                EditingQuickRequestId = source.Id;
                EditingInterfaceId = string.Empty;
                EditingCaseId = string.Empty;
                InterfaceFolderPath = DefaultInterfaceFolderName;
                HttpCaseName = "成功";
                SourceEndpointId = source.RequestSnapshot.EndpointId;
                break;
        }

        ApplySnapshot(source.RequestSnapshot);
        if (source.EntryType == "http-case" && parentInterface is not null && string.IsNullOrWhiteSpace(ConfigTab.RequestName))
        {
            ConfigTab.RequestName = parentInterface.Name;
        }

        UpdateTabHeader();
    }

    public void ApplySnapshot(RequestSnapshotDto snapshot)
    {
        SelectedMethod = string.IsNullOrWhiteSpace(snapshot.Method) ? "GET" : snapshot.Method;
        RequestUrl = snapshot.Url;
        ConfigTab.ApplySnapshot(snapshot);
        UpdateTabHeader();
    }

    public RequestSnapshotDto BuildSnapshot(string? requestNameOverride = null)
    {
        var currentName = ConfigTab.RequestName;
        ConfigTab.RequestName = string.IsNullOrWhiteSpace(requestNameOverride)
            ? ResolveRequestName()
            : requestNameOverride.Trim();
        var snapshot = ConfigTab.BuildRequestSnapshot(SourceEndpointId, SelectedMethod, RequestUrl);
        ConfigTab.RequestName = currentName;
        return snapshot;
    }

    public string ResolveGeneratedRequestName()
    {
        var target = RequestUrl.Trim();
        if (IsHttpInterfaceTab)
        {
            return string.IsNullOrWhiteSpace(target)
                ? "新建 HTTP 接口"
                : $"{SelectedMethod} {target}";
        }

        return string.IsNullOrWhiteSpace(target)
            ? "快捷请求"
            : $"{SelectedMethod} {target}";
    }

    public string ResolveRequestName()
    {
        if (!string.IsNullOrWhiteSpace(ConfigTab.RequestName))
        {
            return ConfigTab.RequestName.Trim();
        }

        return ResolveGeneratedRequestName();
    }

    private void OnConfigTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RequestConfigTabViewModel.RequestName)
            or nameof(RequestConfigTabViewModel.RequestDescription))
        {
            UpdateTabHeader();
        }
    }

    partial void OnEntryTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsLandingTab));
        OnPropertyChanged(nameof(IsQuickRequestTab));
        OnPropertyChanged(nameof(IsHttpInterfaceTab));
        OnPropertyChanged(nameof(IsHttpDebugView));
        OnPropertyChanged(nameof(IsHttpDesignView));
        OnPropertyChanged(nameof(IsHttpDocumentPreviewView));
        OnPropertyChanged(nameof(ShowMethodBadge));
        OnPropertyChanged(nameof(MethodBadgeText));
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(EditorDescription));
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(UrlWatermark));
        UpdateTabHeader();
    }

    partial void OnSelectedMethodChanged(string value)
    {
        OnPropertyChanged(nameof(MethodBadgeText));
        UpdateTabHeader();
    }

    partial void OnRequestUrlChanged(string value)
    {
        UpdateTabHeader();
    }

    partial void OnHttpEditorViewIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsHttpDebugView));
        OnPropertyChanged(nameof(IsHttpDesignView));
        OnPropertyChanged(nameof(IsHttpDocumentPreviewView));
    }

    private void UpdateTabHeader()
    {
        HeaderText = EntryType switch
        {
            WorkspaceEntryTypes.Landing => "新建...",
            WorkspaceEntryTypes.HttpInterface => ResolveHttpInterfaceTabHeader(),
            WorkspaceEntryTypes.QuickRequest => ResolveQuickRequestTabHeader(),
            _ => ResolveRequestName()
        };
    }

    private string ResolveHttpInterfaceTabHeader()
    {
        if (!string.IsNullOrWhiteSpace(ConfigTab.RequestName))
        {
            return ConfigTab.RequestName.Trim();
        }

        return string.IsNullOrWhiteSpace(EditingInterfaceId) && string.IsNullOrWhiteSpace(EditingCaseId)
            ? "新建 HTTP 接口"
            : ResolveGeneratedRequestName();
    }

    private string ResolveQuickRequestTabHeader()
    {
        if (!string.IsNullOrWhiteSpace(ConfigTab.RequestName))
        {
            return ConfigTab.RequestName.Trim();
        }

        return string.IsNullOrWhiteSpace(EditingQuickRequestId)
            ? "快捷请求"
            : ResolveGeneratedRequestName();
    }
}
