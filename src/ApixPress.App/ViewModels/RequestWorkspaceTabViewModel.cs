using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class RequestWorkspaceTabViewModel : ViewModelBase
{
    private const string DefaultInterfaceFolderPath = "";
    private bool _dirtySinceClean;
    private int _bulkStateMutationDepth;
    private bool _hasUnsavedChanges;
    private bool _dirtyStateUpdatePending;
    private bool _tabHeaderUpdatePending;

    private static class WorkspaceEntryTypes
    {
        public const string Landing = "landing";
        public const string InterfaceRoot = "interface-root";
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
    private string interfaceFolderPath = DefaultInterfaceFolderPath;

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
    private bool isCloseDiscardPending;

    [ObservableProperty]
    private string headerText = "新建...";

    internal Action<RequestWorkspaceTabViewModel>? CloseRequested { get; set; }
    internal Action<RequestWorkspaceTabViewModel>? CloseOtherRequested { get; set; }
    internal Action? CloseAllRequested { get; set; }

    public bool IsLandingTab => EntryType == WorkspaceEntryTypes.Landing;
    public bool IsInterfaceRootTab => EntryType == WorkspaceEntryTypes.InterfaceRoot;
    public bool IsQuickRequestTab => EntryType == WorkspaceEntryTypes.QuickRequest;
    public bool IsHttpInterfaceTab => EntryType == WorkspaceEntryTypes.HttpInterface;
    public bool IsHttpDebugView => IsHttpInterfaceTab && HttpEditorViewIndex == 0;
    public bool IsHttpDesignView => IsHttpInterfaceTab && HttpEditorViewIndex == 1;
    public bool IsHttpDocumentPreviewView => IsHttpInterfaceTab && HttpEditorViewIndex == 2;
    public bool ShowMethodBadge => IsHttpInterfaceTab;
    public string MethodBadgeText => SelectedMethod;
    public bool CanCloseFromTab => !IsPinned;
    public bool HasUnsavedChanges => _hasUnsavedChanges;
    public bool ShowUnsavedChanges => HasUnsavedChanges;
    public bool CanReuseForWorkspaceNavigation => !IsPinned && !IsLandingTab && !IsInterfaceRootTab && !HasUnsavedChanges;
    public string PinMenuHeader => IsPinned ? "取消固定标签页" : "固定标签页";
    public string EditorTitle => IsHttpInterfaceTab ? "HTTP 接口" : IsQuickRequestTab ? "快捷请求" : IsInterfaceRootTab ? "根目录" : "新建...";
    public string EditorDescription => IsHttpInterfaceTab
        ? "HTTP 接口会自动使用当前环境的 BaseUrl，请在右侧输入相对路径。"
        : IsQuickRequestTab
            ? "快捷请求不固定 BaseUrl，请输入完整的 http:// 或 https:// 地址。"
            : IsInterfaceRootTab
                ? "根目录承载当前模块下的 Auth 与全部接口。"
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
            InterfaceFolderPath = DefaultInterfaceFolderPath;
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
            InterfaceFolderPath = DefaultInterfaceFolderPath;
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

    public void ConfigureAsInterfaceRoot()
    {
        RunWithBulkStateMutation(() =>
        {
            EntryType = WorkspaceEntryTypes.InterfaceRoot;
            SelectedMethod = "GET";
            RequestUrl = string.Empty;
            InterfaceFolderPath = DefaultInterfaceFolderPath;
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

    /// <summary>打开新建 HTTP 接口编辑；传入目录路径时接口归属该目录，空值落在根目录。</summary>
    public void ConfigureAsHttpInterface(string? folderPath = null)
    {
        RunWithBulkStateMutation(() =>
        {
            EntryType = WorkspaceEntryTypes.HttpInterface;
            SelectedMethod = "GET";
            RequestUrl = string.Empty;
            InterfaceFolderPath = ProjectWorkspaceTreeBuilder.NormalizeFolderPath(folderPath ?? string.Empty);
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
                    InterfaceFolderPath = string.IsNullOrWhiteSpace(source.FolderPath) ? DefaultInterfaceFolderPath : source.FolderPath;
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
                            ? DefaultInterfaceFolderPath
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
                    InterfaceFolderPath = DefaultInterfaceFolderPath;
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
        var resolvedRequestName = string.IsNullOrWhiteSpace(requestNameOverride)
            ? ResolveRequestName()
            : requestNameOverride.Trim();
        return ConfigTab.BuildRequestSnapshot(SourceEndpointId, SelectedMethod, RequestUrl, resolvedRequestName);
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
        _dirtySinceClean = false;
        IsCloseDiscardPending = false;
        UpdateDirtyState(forceNotify: true);
    }

    /// <summary>用户编辑了签名覆盖的字段（方法/URL/名称/请求体/参数集合等）时置脏，O(1) 不再拼接全文签名。</summary>
    public void MarkDirtyFromUserEdit()
    {
        _dirtySinceClean = true;
        NotifyDirtyStateChanged();
    }

    /// <summary>仅刷新未保存状态展示，不置脏（非编辑性写回、联动赋值等场景）。</summary>
    public void NotifyDirtyStateChanged()
    {
        if (_bulkStateMutationDepth > 0)
        {
            _dirtyStateUpdatePending = true;
            return;
        }

        UpdateDirtyState(forceNotify: false);
    }

    private void UpdateDirtyState(bool forceNotify)
    {
        // 脏标志方案：相关属性一旦变化即置脏，避免每次按键在 UI 线程拼接全文签名做对比。
        // 代价是把内容改回原样仍视为未保存（与主流工具行为一致）。
        if (!forceNotify && _dirtySinceClean == _hasUnsavedChanges)
        {
            return;
        }

        var hasUnsavedChanges = _dirtySinceClean;
        if (!forceNotify && _hasUnsavedChanges == hasUnsavedChanges)
        {
            return;
        }

        _hasUnsavedChanges = hasUnsavedChanges;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(ShowUnsavedChanges));
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
        IsCloseDiscardPending = false;
        OnPropertyChanged(nameof(IsLandingTab));
        OnPropertyChanged(nameof(IsInterfaceRootTab));
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
        MarkDirtyFromUserEdit();
    }

    partial void OnSelectedMethodChanged(string value)
    {
        IsCloseDiscardPending = false;
        OnPropertyChanged(nameof(MethodBadgeText));
        RequestTabHeaderUpdate();
        MarkDirtyFromUserEdit();
    }

    partial void OnIsPinnedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCloseFromTab));
        OnPropertyChanged(nameof(CanReuseForWorkspaceNavigation));
        OnPropertyChanged(nameof(PinMenuHeader));
    }

    partial void OnRequestUrlChanged(string value)
    {
        IsCloseDiscardPending = false;
        RequestTabHeaderUpdate();
        MarkDirtyFromUserEdit();
    }

    partial void OnInterfaceFolderPathChanged(string value)
    {
        IsCloseDiscardPending = false;
        MarkDirtyFromUserEdit();
    }

    partial void OnHttpCaseNameChanged(string value)
    {
        IsCloseDiscardPending = false;
        MarkDirtyFromUserEdit();
    }

    partial void OnHttpEditorViewIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsHttpDebugView));
        OnPropertyChanged(nameof(IsHttpDesignView));
        OnPropertyChanged(nameof(IsHttpDocumentPreviewView));
        NotifyDirtyStateChanged();
    }

    private void UpdateTabHeader()
    {
        HeaderText = EntryType switch
        {
            WorkspaceEntryTypes.Landing => "新建...",
            WorkspaceEntryTypes.InterfaceRoot => "根目录",
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

        if (_dirtyStateUpdatePending)
        {
            _dirtyStateUpdatePending = false;
            UpdateDirtyState(forceNotify: false);
        }
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
