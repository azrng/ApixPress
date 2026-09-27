using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ApixPress.App.Helpers;
using ApixPress.App.Messages;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class EnvironmentPanelViewModel : ViewModelBase
{
    private readonly IEnvironmentVariableService _environmentVariableService;
    private readonly IMessenger? _messenger;
    private CancellationTokenSource? _loadProjectCancellationTokenSource;
    private CancellationTokenSource? _activateEnvironmentCancellationTokenSource;
    private string _currentProjectId = string.Empty;
    private bool _isUpdatingSelection;

    public event Action<ProjectEnvironmentItemViewModel?>? SelectedEnvironmentChanged;

    public EnvironmentPanelViewModel(
        IEnvironmentVariableService environmentVariableService,
        IMessenger? messenger = null)
    {
        _environmentVariableService = environmentVariableService;
        _messenger = messenger;
    }

    public BatchObservableCollection<ProjectEnvironmentItemViewModel> Environments { get; } = [];

    [ObservableProperty]
    private ProjectEnvironmentItemViewModel? selectedEnvironment;

    [ObservableProperty]
    private bool isRemoveEnvironmentConfirmOpen;

    public bool HasSelectedEnvironment => SelectedEnvironment is not null && !string.IsNullOrWhiteSpace(SelectedEnvironment.Id);

    public string ActiveEnvironmentName => SelectedEnvironment?.Name ?? string.Empty;

    public string EnvironmentChipText => string.IsNullOrWhiteSpace(ActiveEnvironmentName) ? "环境" : ActiveEnvironmentName;

    protected override void DisposeManaged()
    {
        CancellationTokenSourceHelper.CancelAndDispose(ref _loadProjectCancellationTokenSource);
        CancellationTokenSourceHelper.CancelAndDispose(ref _activateEnvironmentCancellationTokenSource);
        SelectedEnvironmentChanged = null;
    }

    public async Task LoadProjectAsync(string projectId, string? preferredEnvironmentId = null)
    {
        if (IsDisposed)
        {
            return;
        }

        var cancellationToken = CancellationTokenSourceHelper.Refresh(ref _loadProjectCancellationTokenSource).Token;
        try
        {
            _currentProjectId = projectId;
            Environments.Clear();

            if (string.IsNullOrWhiteSpace(projectId))
            {
                _isUpdatingSelection = true;
                SelectedEnvironment = null;
                _isUpdatingSelection = false;
                NotifySelectionState();
                return;
            }

            var environments = await _environmentVariableService.GetEnvironmentsAsync(projectId, cancellationToken);
            var items = environments.Select(CreateEnvironmentItem).ToList();

            Environments.ReplaceWith(items);

            _isUpdatingSelection = true;
            SelectedEnvironment = ResolveSelection(items, preferredEnvironmentId);
            _isUpdatingSelection = false;

            if (SelectedEnvironment is not null)
            {
                UpdateActiveFlags(SelectedEnvironment.Id);
            }

            // 环境抽屉一次性加载全部变量，避免每个环境单独打开连接并发查询。
            var variablesByEnvironment = await _environmentVariableService.GetVariablesByEnvironmentIdsAsync(
                Environments.Select(item => item.Id),
                cancellationToken);
            foreach (var item in Environments)
            {
                variablesByEnvironment.TryGetValue(item.Id, out var variables);
                item.Variables.ReplaceWith((variables ?? []).Select(CreateVariableItem));
            }

            NotifySelectionState();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public void ClearProjectContext()
    {
        _currentProjectId = string.Empty;
        Environments.Clear();
        _isUpdatingSelection = true;
        SelectedEnvironment = null;
        _isUpdatingSelection = false;
        NotifySelectionState();
    }

    public ProjectEnvironmentDto? GetSelectedEnvironmentDto()
    {
        if (SelectedEnvironment is null)
        {
            return null;
        }

        return new ProjectEnvironmentDto
        {
            Id = SelectedEnvironment.Id,
            ProjectId = SelectedEnvironment.ProjectId,
            Name = SelectedEnvironment.Name,
            BaseUrl = SelectedEnvironment.BaseUrl,
            IsActive = SelectedEnvironment.IsActive,
            SortOrder = SelectedEnvironment.SortOrder
        };
    }

    public async Task<bool> ApplyImportedBaseUrlAsync(string baseUrl)
    {
        if (IsDisposed || string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(_currentProjectId))
        {
            return false;
        }

        var applied = await _environmentVariableService.ApplyImportedBaseUrlAsync(_currentProjectId, baseUrl, CancellationToken.None);
        if (applied is null)
        {
            return false;
        }

        var existing = Environments.FirstOrDefault(item => string.Equals(item.Id, applied.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return false;
        }

        existing.BaseUrl = applied.BaseUrl;
        return true;
    }

    [RelayCommand]
    private void SelectEnvironment(ProjectEnvironmentItemViewModel? item)
    {
        if (item is null || ReferenceEquals(item, SelectedEnvironment))
        {
            return;
        }

        // 与 ComboBox 双向绑定路径一致：由 OnSelectedEnvironmentChanged 触发激活与变量加载
        SelectedEnvironment = item;
    }

    [RelayCommand]
    private async Task AddEnvironmentAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentProjectId))
        {
            return;
        }

        var result = await _environmentVariableService.SaveEnvironmentAsync(new ProjectEnvironmentDto
        {
            ProjectId = _currentProjectId,
            Name = BuildNextEnvironmentName(),
            BaseUrl = string.Empty,
            IsActive = Environments.Count == 0,
            SortOrder = Environments.Count + 1
        }, CancellationToken.None);

        if (result.IsSuccess && result.Data is not null)
        {
            var createdItem = CreateEnvironmentItem(result.Data);
            Environments.Add(createdItem);
            UpdateActiveFlags(createdItem.Id);
            _isUpdatingSelection = true;
            SelectedEnvironment = createdItem;
            _isUpdatingSelection = false;
            NotifySelectionState();
        }
    }

    // 抽屉底部全局保存：把全部环境与其变量依次入库
    [RelayCommand]
    private async Task SaveAllEnvironmentsAsync()
    {
        var failedCount = 0;
        var lastFailure = string.Empty;
        foreach (var item in Environments.ToList())
        {
            if (!await SaveEnvironmentCoreAsync(item))
            {
                failedCount++;
                lastFailure = item.Name;
            }
        }

        if (failedCount > 0)
        {
            _messenger?.Send(new StatusMessageRequest(
                failedCount == Environments.Count
                    ? $"环境保存全部失败（{failedCount} 个），请检查后重试。"
                    : $"环境保存完成，{failedCount} 个失败（如「{lastFailure}」），请检查后重新保存。"));
        }
        else
        {
            _messenger?.Send(new StatusMessageRequest($"已保存全部环境（{Environments.Count} 个）。"));
        }
    }

    [RelayCommand]
    private async Task SaveEnvironmentAsync(ProjectEnvironmentItemViewModel? item)
    {
        if (await SaveEnvironmentCoreAsync(item) && item is not null)
        {
            _messenger?.Send(new StatusMessageRequest($"环境「{item.Name}」已保存。"));
        }
    }

    /// <summary>保存单个环境与其变量；返回是否全部成功，失败时经状态栏反馈原因。</summary>
    private async Task<bool> SaveEnvironmentCoreAsync(ProjectEnvironmentItemViewModel? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(_currentProjectId))
        {
            return false;
        }

        var saveResult = await _environmentVariableService.SaveEnvironmentAsync(new ProjectEnvironmentDto
        {
            Id = item.Id,
            ProjectId = _currentProjectId,
            Name = item.Name,
            BaseUrl = item.BaseUrl,
            IsActive = item.IsActive,
            SortOrder = item.SortOrder
        }, CancellationToken.None);

        if (!saveResult.IsSuccess || saveResult.Data is null)
        {
            _messenger?.Send(new StatusMessageRequest($"环境「{item.Name}」保存失败：{saveResult.Message}"));
            return false;
        }

        var variablesResult = await _environmentVariableService.SaveVariablesAsync(
            saveResult.Data,
            item.Variables.Select(variable => new EnvironmentVariableDto
            {
                Id = variable.Id,
                EnvironmentId = saveResult.Data.Id,
                EnvironmentName = saveResult.Data.Name,
                Key = variable.Key,
                Value = variable.Value,
                IsEnabled = variable.IsEnabled
            }).ToList(),
            CancellationToken.None);
        if (!variablesResult.IsSuccess || variablesResult.Data is null)
        {
            _messenger?.Send(new StatusMessageRequest($"环境「{item.Name}」的变量保存失败：{variablesResult.Message}"));
            return false;
        }

        ApplySavedEnvironment(item, saveResult.Data);
        ApplySavedVariables(item, variablesResult.Data);
        return true;
    }

    [RelayCommand]
    private async Task RemoveEnvironmentAsync(ProjectEnvironmentItemViewModel? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(_currentProjectId))
        {
            return;
        }

        var result = await _environmentVariableService.DeleteEnvironmentAsync(_currentProjectId, item.Id, CancellationToken.None);
        if (result.IsSuccess)
        {
            Environments.Remove(item);
            if (ReferenceEquals(SelectedEnvironment, item))
            {
                var nextSelection = Environments.FirstOrDefault(environment => environment.IsActive) ?? Environments.FirstOrDefault();
                _isUpdatingSelection = true;
                SelectedEnvironment = nextSelection;
                _isUpdatingSelection = false;
                NotifySelectionState();
            }

            UpdateActiveFlags(SelectedEnvironment?.Id ?? string.Empty);
        }
    }

    [RelayCommand]
    private void AddVariable(ProjectEnvironmentItemViewModel? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Id))
        {
            return;
        }

        item.Variables.Add(new EnvironmentVariableItemViewModel
        {
            EnvironmentId = item.Id,
            EnvironmentName = item.Name,
            Key = string.Empty,
            Value = string.Empty,
            IsEnabled = true
        });
    }

    [RelayCommand]
    private async Task RemoveVariableAsync(EnvironmentVariableItemViewModel? variable)
    {
        if (variable is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(variable.Id))
        {
            await _environmentVariableService.DeleteVariableAsync(variable.Id, CancellationToken.None);
        }

        var owner = Environments.FirstOrDefault(environment => string.Equals(environment.Id, variable.EnvironmentId, StringComparison.OrdinalIgnoreCase));
        owner?.Variables.Remove(variable);
    }

    private ProjectEnvironmentItemViewModel? ResolveSelection(IReadOnlyList<ProjectEnvironmentItemViewModel> items, string? preferredEnvironmentId)
    {
        if (!string.IsNullOrWhiteSpace(preferredEnvironmentId))
        {
            var preferred = items.FirstOrDefault(item => string.Equals(item.Id, preferredEnvironmentId, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
            {
                return preferred;
            }
        }

        if (!string.IsNullOrWhiteSpace(SelectedEnvironment?.Id))
        {
            var current = items.FirstOrDefault(item => string.Equals(item.Id, SelectedEnvironment.Id, StringComparison.OrdinalIgnoreCase));
            if (current is not null)
            {
                return current;
            }
        }

        return items.FirstOrDefault(item => item.IsActive) ?? items.FirstOrDefault();
    }

    private string BuildNextEnvironmentName()
    {
        var index = 1;
        while (true)
        {
            var name = $"新环境 {index}";
            if (!Environments.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return name;
            }

            index++;
        }
    }

    private void UpdateActiveFlags(string environmentId)
    {
        foreach (var environment in Environments)
        {
            environment.IsActive = string.Equals(environment.Id, environmentId, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void NotifySelectionState()
    {
        OnPropertyChanged(nameof(HasSelectedEnvironment));
        OnPropertyChanged(nameof(ActiveEnvironmentName));
        OnPropertyChanged(nameof(EnvironmentChipText));
    }

    private void ApplySavedEnvironment(ProjectEnvironmentItemViewModel item, ProjectEnvironmentDto environment)
    {
        // 新增场景：保存成功后把服务端分配的 Id 回填到卡片
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            item.Id = environment.Id;
        }

        item.ProjectId = environment.ProjectId;
        item.SortOrder = environment.SortOrder;
        UpdateActiveFlags(item.IsActive ? item.Id : (SelectedEnvironment?.Id ?? string.Empty));
    }

    private void ApplySavedVariables(ProjectEnvironmentItemViewModel item, IReadOnlyList<EnvironmentVariableDto> variables)
    {
        // 以服务端返回为准回填 Id/EnvironmentId（新增变量入库后才有 Id），不整表替换避免打断编辑
        foreach (var variable in item.Variables)
        {
            var saved = variables.FirstOrDefault(candidate => string.Equals(candidate.Key, variable.Key, StringComparison.Ordinal)
                && string.Equals(candidate.Value, variable.Value, StringComparison.Ordinal)
                && candidate.IsEnabled == variable.IsEnabled);
            if (saved is null)
            {
                continue;
            }

            variable.Id = saved.Id;
            variable.EnvironmentId = saved.EnvironmentId;
            variable.EnvironmentName = saved.EnvironmentName;
        }
    }

    private static ProjectEnvironmentItemViewModel CreateEnvironmentItem(ProjectEnvironmentDto environment)
    {
        return new ProjectEnvironmentItemViewModel
        {
            Id = environment.Id,
            ProjectId = environment.ProjectId,
            Name = environment.Name,
            BaseUrl = environment.BaseUrl,
            IsActive = environment.IsActive,
            SortOrder = environment.SortOrder
        };
    }

    private static EnvironmentVariableItemViewModel CreateVariableItem(EnvironmentVariableDto item)
    {
        return new EnvironmentVariableItemViewModel
        {
            Id = item.Id,
            EnvironmentId = item.EnvironmentId,
            EnvironmentName = item.EnvironmentName,
            Key = item.Key,
            Value = item.Value,
            IsEnabled = item.IsEnabled
        };
    }

    partial void OnSelectedEnvironmentChanged(ProjectEnvironmentItemViewModel? value)
    {
        if (IsDisposed)
        {
            return;
        }

        UpdateSelectionFlags(value);
        NotifySelectionState();
        if (_isUpdatingSelection)
        {
            return;
        }

        var cancellationToken = CancellationTokenSourceHelper.Refresh(ref _activateEnvironmentCancellationTokenSource).Token;
        _ = ActivateEnvironmentSelectionAsync(value, cancellationToken);
    }

    private void UpdateSelectionFlags(ProjectEnvironmentItemViewModel? selected)
    {
        foreach (var environment in Environments)
        {
            environment.IsSelected = ReferenceEquals(environment, selected);
        }
    }

    private async Task ActivateEnvironmentSelectionAsync(ProjectEnvironmentItemViewModel? value, CancellationToken cancellationToken)
    {
        try
        {
            if (value is null || string.IsNullOrWhiteSpace(value.Id) || string.IsNullOrWhiteSpace(_currentProjectId))
            {
                SelectedEnvironmentChanged?.Invoke(value);
                return;
            }

            var result = await _environmentVariableService.SetActiveEnvironmentAsync(_currentProjectId, value.Id, cancellationToken);
            if (result.IsSuccess)
            {
                UpdateActiveFlags(value.Id);
            }

            SelectedEnvironmentChanged?.Invoke(value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
