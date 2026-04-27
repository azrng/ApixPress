using System.ComponentModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class RequestWorkspaceTabViewModel : ViewModelBase
{
    private const string DefaultInterfaceFolderName = "默认模块";
    private string _cleanStateSignature = string.Empty;
    private int _bulkStateMutationDepth;
    private bool _tabHeaderUpdatePending;

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
    private bool isPinned;

    [ObservableProperty]
    private string headerText = "新建...";

    internal Action<RequestWorkspaceTabViewModel>? CloseRequested { get; set; }
    internal Action<RequestWorkspaceTabViewModel>? CloseOtherRequested { get; set; }
    internal Action? CloseAllRequested { get; set; }

    public bool IsLandingTab => EntryType == WorkspaceEntryTypes.Landing;
    public bool IsQuickRequestTab => EntryType == WorkspaceEntryTypes.QuickRequest;
    public bool IsHttpInterfaceTab => EntryType == WorkspaceEntryTypes.HttpInterface;
    public bool IsHttpDebugView => IsHttpInterfaceTab && HttpEditorViewIndex == 0;
    public bool IsHttpDesignView => IsHttpInterfaceTab && HttpEditorViewIndex == 1;
    public bool IsHttpDocumentPreviewView => IsHttpInterfaceTab && HttpEditorViewIndex == 2;
    public bool ShowMethodBadge => IsHttpInterfaceTab;
    public string MethodBadgeText => SelectedMethod;
    public bool CanCloseFromTab => !IsPinned;
    public bool HasUnsavedChanges => !string.Equals(_cleanStateSignature, BuildStateSignature(), StringComparison.Ordinal);
    public bool CanReuseForWorkspaceNavigation => !IsPinned && !IsLandingTab && !HasUnsavedChanges;
    public string PinMenuHeader => IsPinned ? "取消固定标签页" : "固定标签页";
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
        RunWithBulkStateMutation(() =>
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
        });

        MarkCleanState();
    }

    public void ConfigureAsQuickRequest()
    {
        RunWithBulkStateMutation(() =>
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
        });

        MarkCleanState();
    }

    public void ConfigureAsHttpInterface()
    {
        RunWithBulkStateMutation(() =>
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
        });

        MarkCleanState();
    }

    public void ApplySavedRequest(RequestCaseDto source, RequestCaseDto? parentInterface = null)
    {
        RunWithBulkStateMutation(() =>
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

            ApplySnapshotCore(source.RequestSnapshot);
            if (source.EntryType == "http-case" && parentInterface is not null && string.IsNullOrWhiteSpace(ConfigTab.RequestName))
            {
                ConfigTab.RequestName = parentInterface.Name;
            }
        });

        MarkCleanState();
    }

    public void ApplySnapshot(RequestSnapshotDto snapshot)
    {
        RunWithBulkStateMutation(() => ApplySnapshotCore(snapshot));
        MarkCleanState();
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

    public void MarkCleanState()
    {
        _cleanStateSignature = BuildStateSignature();
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CanReuseForWorkspaceNavigation));
    }

    [RelayCommand]
    private void TogglePin()
    {
        IsPinned = !IsPinned;
    }

    [RelayCommand]
    private void CloseCurrentFromTabMenu()
    {
        CloseRequested?.Invoke(this);
    }

    [RelayCommand]
    private void CloseOtherFromTabMenu()
    {
        CloseOtherRequested?.Invoke(this);
    }

    [RelayCommand]
    private void CloseAllFromTabMenu()
    {
        CloseAllRequested?.Invoke();
    }

    protected override void DisposeManaged()
    {
        ConfigTab.PropertyChanged -= OnConfigTabPropertyChanged;
    }

    private void OnConfigTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        if (e.PropertyName is nameof(RequestConfigTabViewModel.RequestName)
            or nameof(RequestConfigTabViewModel.RequestDescription))
        {
            RequestTabHeaderUpdate();
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
        RequestTabHeaderUpdate();
    }

    partial void OnSelectedMethodChanged(string value)
    {
        OnPropertyChanged(nameof(MethodBadgeText));
        RequestTabHeaderUpdate();
    }

    partial void OnIsPinnedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCloseFromTab));
        OnPropertyChanged(nameof(CanReuseForWorkspaceNavigation));
        OnPropertyChanged(nameof(PinMenuHeader));
    }

    partial void OnRequestUrlChanged(string value)
    {
        RequestTabHeaderUpdate();
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

    private void ApplySnapshotCore(RequestSnapshotDto snapshot)
    {
        SelectedMethod = string.IsNullOrWhiteSpace(snapshot.Method) ? "GET" : snapshot.Method;
        RequestUrl = snapshot.Url;
        ConfigTab.ApplySnapshot(snapshot);
    }

    private void RunWithBulkStateMutation(Action action)
    {
        _bulkStateMutationDepth++;
        try
        {
            action();
        }
        finally
        {
            _bulkStateMutationDepth--;
            if (_bulkStateMutationDepth == 0)
            {
                FlushDeferredStateUpdates();
            }
        }
    }

    private void RequestTabHeaderUpdate()
    {
        if (_bulkStateMutationDepth > 0)
        {
            _tabHeaderUpdatePending = true;
            return;
        }

        UpdateTabHeader();
    }

    private void FlushDeferredStateUpdates()
    {
        if (_tabHeaderUpdatePending)
        {
            _tabHeaderUpdatePending = false;
            UpdateTabHeader();
        }
    }

    private string BuildStateSignature()
    {
        var builder = new StringBuilder();
        Append(builder, EntryType);
        Append(builder, SelectedMethod);
        Append(builder, RequestUrl);
        Append(builder, InterfaceFolderPath);
        Append(builder, HttpCaseName);
        Append(builder, SourceEndpointId);
        Append(builder, EditingQuickRequestId);
        Append(builder, EditingInterfaceId);
        Append(builder, EditingCaseId);
        Append(builder, HttpEditorViewIndex.ToString());
        Append(builder, ConfigTab.RequestName);
        Append(builder, ConfigTab.RequestDescription);
        Append(builder, ConfigTab.RequestBody);
        Append(builder, ConfigTab.SelectedBodyMode);
        Append(builder, ConfigTab.IgnoreSslErrors ? "1" : "0");
        AppendParameters(builder, ConfigTab.QueryParameters);
        AppendParameters(builder, ConfigTab.PathParameters);
        AppendParameters(builder, ConfigTab.Headers);
        AppendParameters(builder, ConfigTab.FormFields);
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string? value)
    {
        builder.Append(value?.Length ?? 0);
        builder.Append(':');
        builder.Append(value);
        builder.Append('|');
    }

    private static void AppendParameters(StringBuilder builder, IEnumerable<RequestParameterItemViewModel> parameters)
    {
        foreach (var parameter in parameters)
        {
            Append(builder, parameter.Name);
            Append(builder, parameter.Value);
            Append(builder, parameter.Description);
            Append(builder, parameter.IsEnabled ? "1" : "0");
        }

        builder.Append(';');
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
