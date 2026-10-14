using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ApixPress.App.Helpers;
using ApixPress.App.Messages;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class UseCasesPanelViewModel : ViewModelBase
{
    private readonly IRequestCaseService _requestCaseService;
    private readonly ProjectWorkspaceTabsViewModel? _workspace;
    private readonly IDialogService? _dialogService;
    private readonly IMessenger? _messenger;
    private CancellationTokenSource? _loadCasesCancellationTokenSource;
    private string _currentProjectId = string.Empty;
    private readonly BatchObservableCollection<RequestCaseItemViewModel> _currentInterfaceCaseItems = [];
    private RequestWorkspaceTabViewModel? _subscribedActiveWorkspaceTab;
    private Func<RequestCaseDto, Task>? _loadRequestCaseAsync;

    public event Action<RequestSnapshotDto>? CaseApplied;

    /// <summary>用例集合变更后的目录分类；空集合表示全量刷新。</summary>
    public event Action<IReadOnlyCollection<string>>? RequestCasesChanged;

    public UseCasesPanelViewModel(
        IRequestCaseService requestCaseService,
        ProjectWorkspaceTabsViewModel? workspace = null,
        IDialogService? dialogService = null,
        IMessenger? messenger = null)
    {
        _requestCaseService = requestCaseService;
        _workspace = workspace;
        _dialogService = dialogService;
        _messenger = messenger;
        SelectableCaseItems = new ReadOnlyObservableCollection<RequestCaseItemViewModel>(_selectableCaseItems);
        CurrentInterfaceCaseItems = new ReadOnlyObservableCollection<RequestCaseItemViewModel>(_currentInterfaceCaseItems);
        RequestCases.CollectionChanged += (_, _) => RefreshSelectableCaseItems();

        if (_workspace is not null)
        {
            _workspace.ActiveWorkspaceTabChanged += OnActiveWorkspaceTabChanged;
            SubscribeActiveWorkspaceTab(_workspace.ActiveWorkspaceTab);
        }
    }

    public BatchObservableCollection<RequestCaseItemViewModel> RequestCases { get; } = [];

    /// <summary>可加载/复制的用例列表：目录节点只承载树结构，不出现在用例管理抽屉中。</summary>
    public ReadOnlyObservableCollection<RequestCaseItemViewModel> SelectableCaseItems { get; }

    /// <summary>当前活动 HTTP 接口下的用例列表，供用例抽屉展示。</summary>
    public ReadOnlyObservableCollection<RequestCaseItemViewModel> CurrentInterfaceCaseItems { get; }

    public bool HasSelectableCases => SelectableCaseItems.Count > 0;
    public bool HasCurrentInterfaceCases => CurrentInterfaceCaseItems.Count > 0;
    public bool HasCurrentHttpInterface => _workspace?.ActiveWorkspaceTab?.IsHttpInterfaceTab == true;
    public string CurrentInterfaceName => _workspace?.ActiveWorkspaceTab is { IsHttpInterfaceTab: true } tab
        ? tab.ResolveRequestName()
        : string.Empty;
    public string CurrentInterfaceMethod => _workspace?.ActiveWorkspaceTab is { IsHttpInterfaceTab: true } tab
        ? tab.SelectedMethod.ToUpperInvariant()
        : string.Empty;
    public string CurrentInterfaceMethodBadgeClass => CurrentInterfaceMethod switch
    {
        "GET" => "Light Success",
        "POST" => "Light Warning",
        "PUT" => "Light Primary",
        "DELETE" => "Light Danger",
        "PATCH" => "Light Success",
        _ => "Light Secondary"
    };
    public string CurrentInterfaceCaseCountText => $"共 {CurrentInterfaceCaseItems.Count} 个用例";
    public string CurrentInterfaceCaseEmptyStateText => HasCurrentHttpInterface
        ? "当前接口还没有用例；在请求编辑区填写用例名称后即可保存。"
        : "请先打开一个 HTTP 接口。";

    private readonly BatchObservableCollection<RequestCaseItemViewModel> _selectableCaseItems = [];

    [ObservableProperty]
    private string caseName = string.Empty;

    [ObservableProperty]
    private string caseGroupName = "默认分组";

    [ObservableProperty]
    private string caseDescription = string.Empty;

    [ObservableProperty]
    private RequestCaseItemViewModel? selectedRequestCase;

    public ObservableCollection<string> CaseTags { get; } = [];

    protected override void DisposeManaged()
    {
        CancellationTokenSourceHelper.CancelAndDispose(ref _loadCasesCancellationTokenSource);
        if (_workspace is not null)
        {
            _workspace.ActiveWorkspaceTabChanged -= OnActiveWorkspaceTabChanged;
        }

        SubscribeActiveWorkspaceTab(null);
        _loadRequestCaseAsync = null;
        CaseApplied = null;
        RequestCasesChanged = null;
    }

    public void SetProjectContext(string projectId)
    {
        _currentProjectId = projectId;
        RefreshSelectableCaseItems();
    }

    public void ClearProjectContext()
    {
        _currentProjectId = string.Empty;
        RequestCases.Clear();
        SelectedRequestCase = null;
        NotifyRequestCasesChanged();
    }

    /// <summary>注入工作区用例加载入口，复用目录加载的标签复用、详情加载和请求回填逻辑。</summary>
    public void SetRequestCaseLoader(Func<RequestCaseDto, Task> loadRequestCaseAsync)
    {
        _loadRequestCaseAsync = loadRequestCaseAsync;
    }

    public async Task LoadCasesAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        var cancellationToken = CancellationTokenSourceHelper.Refresh(ref _loadCasesCancellationTokenSource).Token;
        try
        {
            RequestCases.Clear();
            NotifyRequestCasesChanged();
            if (string.IsNullOrWhiteSpace(_currentProjectId))
            {
                return;
            }

            var cases = await _requestCaseService.GetCasesAsync(_currentProjectId, cancellationToken);
            RequestCases.ReplaceWith(cases.Select(CreateRequestCaseItem));
            NotifyRequestCasesChanged();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public void SetDefaultsFromEndpoint(ApiEndpointDto endpoint)
    {
        CaseName = endpoint.Name;
        CaseDescription = endpoint.Description;
        CaseGroupName = endpoint.GroupName;
    }

    public RequestCaseDto BuildCaseDto(string caseId, string requestName, RequestSnapshotDto snapshot)
    {
        return new RequestCaseDto
        {
            Id = caseId,
            ProjectId = _currentProjectId,
            Name = string.IsNullOrWhiteSpace(CaseName) ? requestName : CaseName,
            GroupName = CaseGroupName,
            Tags = CaseTags.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToList(),
            Description = CaseDescription,
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        };
    }

    [RelayCommand]
    public async Task SaveCaseAsync(RequestSnapshotDto snapshot)
    {
        if (string.IsNullOrWhiteSpace(_currentProjectId))
        {
            return;
        }

        var dto = BuildCaseDto(SelectedRequestCase?.Id ?? string.Empty, snapshot.Name, snapshot);
        var result = await _requestCaseService.SaveAsync(dto, CancellationToken.None);
        if (result.IsSuccess && result.Data is not null)
        {
            UpsertCaseItem(result.Data);
        }
        else
        {
            // 此前失败分支静默返回，用户无从得知保存未生效；经状态栏给出原因
            _messenger?.Send(new StatusMessageRequest($"保存用例「{dto.Name}」失败：{result.Message}"));
        }
    }

    [RelayCommand]
    private async Task LoadSelectedCase()
    {
        if (SelectedRequestCase is null) return;
        await LoadCaseCoreAsync(SelectedRequestCase);
    }

    [RelayCommand]
    private async Task LoadCaseAsync(RequestCaseItemViewModel? item)
    {
        if (item is null) return;
        SelectedRequestCase = item;
        await LoadCaseCoreAsync(item);
    }

    private async Task LoadCaseCoreAsync(RequestCaseItemViewModel item)
    {
        var detail = await EnsureCaseDetailLoadedAsync(item);
        var snapshot = detail.RequestSnapshot;
        CaseName = detail.Name;
        CaseGroupName = detail.GroupName;
        ReplaceCaseTags(detail.Tags);
        CaseDescription = detail.Description;
        CaseApplied?.Invoke(snapshot);
        if (_loadRequestCaseAsync is not null)
        {
            await _loadRequestCaseAsync(detail);
        }
    }

    [RelayCommand]
    private async Task DuplicateCaseAsync(RequestCaseItemViewModel? item)
    {
        if (item is null) return;
        if (string.IsNullOrWhiteSpace(_currentProjectId)) return;
        var result = await _requestCaseService.DuplicateAsync(_currentProjectId, item.Id, CancellationToken.None);
        if (result.IsSuccess && result.Data is not null)
        {
            UpsertCaseItem(result.Data);
            _messenger?.Send(new StatusMessageRequest($"已复制为「{result.Data.Name}」。"));
        }
    }

    [RelayCommand]
    private async Task DuplicateSelectedCaseAsync()
    {
        if (SelectedRequestCase is null) return;
        await DuplicateCaseAsync(SelectedRequestCase);
    }

    /// <summary>
    /// 重命名用例：弹窗预填当前名，确认后按全量 DTO 更新（快照保持原样），同名校验错误在弹窗内回显。
    /// </summary>
    [RelayCommand]
    private async Task RenameCaseAsync(RequestCaseItemViewModel? item)
    {
        if (item is null || _dialogService is null || string.IsNullOrWhiteSpace(_currentProjectId))
        {
            return;
        }

        var detail = await EnsureCaseDetailLoadedAsync(item);
        var dialog = new ProjectInfoEditDialogViewModel(
            isNameMode: true,
            detail.Name,
            async newName =>
            {
                var renamed = new RequestCaseDto
                {
                    Id = detail.Id,
                    ProjectId = detail.ProjectId,
                    EntryType = detail.EntryType,
                    Name = newName,
                    GroupName = detail.GroupName,
                    FolderPath = detail.FolderPath,
                    ParentId = detail.ParentId,
                    Tags = detail.Tags,
                    Description = detail.Description,
                    RequestSnapshot = detail.RequestSnapshot,
                    HasLoadedDetail = true,
                    UpdatedAt = DateTime.UtcNow
                };
                var result = await _requestCaseService.SaveAsync(renamed, CancellationToken.None);
                if (!result.IsSuccess)
                {
                    return result.Message;
                }

                UpsertCaseItem(result.Data!);
                return null;
            },
            title: "重命名用例",
            fieldLabel: "用例名称");

        await _dialogService.ShowDialogAsync<Views.Controls.ProjectInfoEditDialogView>(dialog);
    }

    /// <summary>打开用例删除确认弹窗；确认后从持久化层和当前抽屉列表同时删除。</summary>
    [RelayCommand]
    private async Task RequestDeleteCaseAsync(RequestCaseItemViewModel? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(_currentProjectId) || _dialogService is null)
        {
            return;
        }

        var result = await _dialogService.ShowDialogAsync<Views.Controls.RequestCaseDeleteConfirmDialogView>(
            new RequestCaseDeleteConfirmDialogViewModel(item.Name),
            "确认删除");
        if (result != Ursa.Controls.DialogResult.OK)
        {
            return;
        }

        await DeleteCaseAsync(item);
    }

    [RelayCommand]
    private async Task DeleteSelectedCaseAsync()
    {
        if (SelectedRequestCase is null) return;
        await RequestDeleteCaseAsync(SelectedRequestCase);
    }

    private async Task DeleteCaseAsync(RequestCaseItemViewModel item)
    {
        var result = await _requestCaseService.DeleteAsync(_currentProjectId, item.Id, CancellationToken.None);
        if (!result.IsSuccess)
        {
            _messenger?.Send(new StatusMessageRequest(result.Message));
            return;
        }

        RemoveCases([item.Id]);
        if (ReferenceEquals(SelectedRequestCase, item))
        {
            SelectedRequestCase = null;
        }

        _messenger?.Send(new StatusMessageRequest($"已删除用例「{item.Name}」。"));
    }

    partial void OnSelectedRequestCaseChanged(RequestCaseItemViewModel? value)
    {
        foreach (var caseItem in _selectableCaseItems)
        {
            caseItem.IsSelected = ReferenceEquals(caseItem, value);
        }
    }

    private void ReplaceCaseTags(IEnumerable<string> tags)
    {
        CaseTags.Clear();
        foreach (var tag in tags.Where(tag => !string.IsNullOrWhiteSpace(tag)))
        {
            CaseTags.Add(tag);
        }
    }

    public void UpsertCaseItem(RequestCaseDto requestCase)
    {
        UpsertCases([requestCase]);
    }

    /// <summary>批量按排序位置插入/更新用例：整体一次 ReplaceWith，导入 N 条只触发一次集合重置。</summary>
    public void UpsertCases(IEnumerable<RequestCaseDto> requestCases)
    {
        var upserts = requestCases as IReadOnlyList<RequestCaseDto> ?? requestCases.ToList();
        if (upserts.Count == 0)
        {
            return;
        }

        var upsertIds = upserts.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var affectedEntryTypes = RequestCases
            .Where(item => upsertIds.Contains(item.Id))
            .Select(item => item.SourceCase.EntryType)
            .Concat(upserts.Select(item => item.EntryType))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var merged = RequestCases
            .Where(item => !upsertIds.Contains(item.Id))
            .Concat(upserts.Select(CreateRequestCaseItem))
            .ToList();
        merged.Sort((left, right) => CompareRequestCaseItems(left, right));
        RequestCases.ReplaceWith(merged);
        NotifyRequestCasesChanged(affectedEntryTypes);
    }

    public void RemoveCases(IEnumerable<string> ids)
    {
        var targetIds = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (targetIds.Count == 0)
        {
            return;
        }

        var affectedEntryTypes = RequestCases
            .Where(item => targetIds.Contains(item.Id))
            .Select(item => item.SourceCase.EntryType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var retained = RequestCases
            .Where(item => !targetIds.Contains(item.Id))
            .ToList();
        if (retained.Count == RequestCases.Count)
        {
            return;
        }

        RequestCases.ReplaceWith(retained);
        NotifyRequestCasesChanged(affectedEntryTypes);
    }

    private void NotifyRequestCasesChanged(IReadOnlyCollection<string>? entryTypes = null)
    {
        RequestCasesChanged?.Invoke(entryTypes ?? Array.Empty<string>());
    }

    private void RefreshSelectableCaseItems()
    {
        // 可选用例列表随用例集合全量重建：用批量替换单次 Reset 通知 UI，避免逐个 Add 触发 N 次刷新
        _selectableCaseItems.ReplaceWith(RequestCases
            .Where(item => !string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.Folder, StringComparison.OrdinalIgnoreCase))
            .ToList());

        var currentInterfaceId = ResolveCurrentInterfaceId();
        _currentInterfaceCaseItems.ReplaceWith(string.IsNullOrWhiteSpace(currentInterfaceId)
            ? []
            : RequestCases
                .Where(item => string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.HttpCase, StringComparison.OrdinalIgnoreCase))
                .Where(item => string.Equals(item.SourceCase.ParentId, currentInterfaceId, StringComparison.OrdinalIgnoreCase))
                .ToList());

        if (SelectedRequestCase is not null && !_currentInterfaceCaseItems.Contains(SelectedRequestCase))
        {
            SelectedRequestCase = null;
        }

        OnPropertyChanged(nameof(HasSelectableCases));
        OnPropertyChanged(nameof(HasCurrentInterfaceCases));
        OnPropertyChanged(nameof(HasCurrentHttpInterface));
        OnPropertyChanged(nameof(CurrentInterfaceName));
        OnPropertyChanged(nameof(CurrentInterfaceMethod));
        OnPropertyChanged(nameof(CurrentInterfaceMethodBadgeClass));
        OnPropertyChanged(nameof(CurrentInterfaceCaseCountText));
        OnPropertyChanged(nameof(CurrentInterfaceCaseEmptyStateText));
    }

    private string ResolveCurrentInterfaceId()
    {
        return _workspace?.ActiveWorkspaceTab is { IsHttpInterfaceTab: true } tab
            ? tab.EditingInterfaceId
            : string.Empty;
    }

    private void OnActiveWorkspaceTabChanged(RequestWorkspaceTabViewModel? oldValue, RequestWorkspaceTabViewModel? newValue)
    {
        SubscribeActiveWorkspaceTab(newValue);
        RefreshSelectableCaseItems();
    }

    private void SubscribeActiveWorkspaceTab(RequestWorkspaceTabViewModel? tab)
    {
        if (_subscribedActiveWorkspaceTab is not null)
        {
            _subscribedActiveWorkspaceTab.PropertyChanged -= OnActiveWorkspaceTabPropertyChanged;
        }

        _subscribedActiveWorkspaceTab = tab;
        if (_subscribedActiveWorkspaceTab is not null)
        {
            _subscribedActiveWorkspaceTab.PropertyChanged += OnActiveWorkspaceTabPropertyChanged;
        }
    }

    private void OnActiveWorkspaceTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RequestWorkspaceTabViewModel.EntryType)
            or nameof(RequestWorkspaceTabViewModel.EditingInterfaceId)
            or nameof(RequestWorkspaceTabViewModel.EditingCaseId)
            or nameof(RequestWorkspaceTabViewModel.SelectedMethod)
            or nameof(RequestWorkspaceTabViewModel.HeaderText))
        {
            RefreshSelectableCaseItems();
        }
    }

    private RequestCaseItemViewModel CreateRequestCaseItem(RequestCaseDto requestCase)
    {
        return new RequestCaseItemViewModel
        {
            Id = requestCase.Id,
            Name = requestCase.Name,
            GroupName = requestCase.GroupName,
            TagsText = string.Join(", ", requestCase.Tags),
            Description = requestCase.Description,
            UpdatedAt = requestCase.UpdatedAt.ToLocalTime(),
            SourceCase = requestCase,
            // 右键菜单命令直接挂行项（与卡片行内按钮同一批命令），跨 ContextMenu PopupRoot 保持绑定可用
            LoadCommand = LoadCaseCommand,
            RenameCommand = RenameCaseCommand,
            DuplicateCommand = DuplicateCaseCommand,
            DeleteCommand = RequestDeleteCaseCommand
        };
    }

    private static int CompareRequestCaseItems(RequestCaseItemViewModel left, RequestCaseItemViewModel right)
    {
        var entryTypeCompare = StringComparer.OrdinalIgnoreCase.Compare(left.SourceCase.EntryType, right.SourceCase.EntryType);
        if (entryTypeCompare != 0)
        {
            return entryTypeCompare;
        }

        var folderCompare = StringComparer.OrdinalIgnoreCase.Compare(left.SourceCase.FolderPath, right.SourceCase.FolderPath);
        if (folderCompare != 0)
        {
            return folderCompare;
        }

        return StringComparer.OrdinalIgnoreCase.Compare(left.SourceCase.Name, right.SourceCase.Name);
    }

    private async Task<RequestCaseDto> EnsureCaseDetailLoadedAsync(RequestCaseItemViewModel item)
    {
        if (item.SourceCase.HasLoadedDetail)
        {
            return item.SourceCase;
        }

        if (string.IsNullOrWhiteSpace(_currentProjectId))
        {
            return item.SourceCase;
        }

        var detail = await _requestCaseService.GetDetailAsync(_currentProjectId, item.Id, CancellationToken.None);
        if (detail is null)
        {
            return item.SourceCase;
        }

        item.ApplyDetail(detail);
        return detail;
    }
}
