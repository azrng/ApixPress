using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class EnvironmentPanelViewModel : ViewModelBase
{
    private readonly IEnvironmentVariableService _environmentVariableService;
    private CancellationTokenSource? _loadProjectCancellationTokenSource;
    private CancellationTokenSource? _activateEnvironmentCancellationTokenSource;
    private string _currentProjectId = string.Empty;
    private bool _isUpdatingSelection;

    public event Action<ProjectEnvironmentItemViewModel?>? SelectedEnvironmentChanged;

    public EnvironmentPanelViewModel(IEnvironmentVariableService environmentVariableService)
    {
        _environmentVariableService = environmentVariableService;
    }

    public ObservableCollection<ProjectEnvironmentItemViewModel> Environments { get; } = [];

    public ObservableCollection<EnvironmentVariableItemViewModel> EnvironmentVariables { get; } = [];

    [ObservableProperty]
    private ProjectEnvironmentItemViewModel? selectedEnvironment;

    public bool HasSelectedEnvironment => SelectedEnvironment is not null && !string.IsNullOrWhiteSpace(SelectedEnvironment.Id);

    public string ActiveEnvironmentName => SelectedEnvironment?.Name ?? string.Empty;

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
            EnvironmentVariables.Clear();
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
                await LoadVariablesAsync(SelectedEnvironment.Id, cancellationToken);
                UpdateActiveFlags(SelectedEnvironment.Id);
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
        EnvironmentVariables.Clear();
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
            EnvironmentVariables.Clear();
            NotifySelectionState();
        }
    }

    [RelayCommand]
    private async Task SaveEnvironmentAsync()
    {
        if (SelectedEnvironment is null || string.IsNullOrWhiteSpace(_currentProjectId))
        {
            return;
        }

        var saveResult = await _environmentVariableService.SaveEnvironmentAsync(new ProjectEnvironmentDto
        {
            Id = SelectedEnvironment.Id,
            ProjectId = _currentProjectId,
            Name = SelectedEnvironment.Name,
            BaseUrl = SelectedEnvironment.BaseUrl,
            IsActive = SelectedEnvironment.IsActive,
            SortOrder = SelectedEnvironment.SortOrder
        }, CancellationToken.None);

        if (!saveResult.IsSuccess || saveResult.Data is null)
        {
            return;
        }

        var variablesResult = await _environmentVariableService.SaveVariablesAsync(
            saveResult.Data,
            EnvironmentVariables.Select(item => new EnvironmentVariableDto
            {
                Id = item.Id,
                EnvironmentId = saveResult.Data.Id,
                EnvironmentName = saveResult.Data.Name,
                Key = item.Key,
                Value = item.Value,
                IsEnabled = item.IsEnabled
            }).ToList(),
            CancellationToken.None);
        if (!variablesResult.IsSuccess || variablesResult.Data is null)
        {
            return;
        }

        ApplySavedEnvironment(saveResult.Data);
        ApplySavedVariables(variablesResult.Data);
    }

    [RelayCommand]
    private async Task RemoveEnvironmentAsync()
    {
        if (SelectedEnvironment is null || string.IsNullOrWhiteSpace(_currentProjectId))
        {
            return;
        }

        var deletedEnvironmentId = SelectedEnvironment.Id;
        var result = await _environmentVariableService.DeleteEnvironmentAsync(_currentProjectId, deletedEnvironmentId, CancellationToken.None);
        if (result.IsSuccess)
        {
            var nextSelection = Environments.FirstOrDefault(item => !string.Equals(item.Id, deletedEnvironmentId, StringComparison.OrdinalIgnoreCase));
            var deletedItem = SelectedEnvironment;
            if (deletedItem is not null)
            {
                Environments.Remove(deletedItem);
            }

            _isUpdatingSelection = true;
            SelectedEnvironment = nextSelection;
            _isUpdatingSelection = false;
            EnvironmentVariables.Clear();

            if (nextSelection is not null)
            {
                UpdateActiveFlags(nextSelection.Id);
                await LoadVariablesAsync(nextSelection.Id, CancellationToken.None);
            }

            NotifySelectionState();
        }
    }

    [RelayCommand]
    private void AddVariable()
    {
        if (SelectedEnvironment is null)
        {
            return;
        }

        EnvironmentVariables.Add(new EnvironmentVariableItemViewModel
        {
            EnvironmentId = SelectedEnvironment.Id,
            EnvironmentName = SelectedEnvironment.Name,
            Key = string.Empty,
            Value = string.Empty,
            IsEnabled = true
        });
    }

    [RelayCommand]
    private async Task RemoveVariableAsync(EnvironmentVariableItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(item.Id))
        {
            await _environmentVariableService.DeleteVariableAsync(item.Id, CancellationToken.None);
        }

        EnvironmentVariables.Remove(item);
    }

    private async Task LoadVariablesAsync(string environmentId, CancellationToken cancellationToken)
    {
        EnvironmentVariables.Clear();
        var items = await _environmentVariableService.GetVariablesAsync(environmentId, cancellationToken);
        EnvironmentVariables.ReplaceWith(items.Select(CreateVariableItem));
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
    }

    private void ApplySavedEnvironment(ProjectEnvironmentDto environment)
    {
        var existing = Environments.FirstOrDefault(item => string.Equals(item.Id, environment.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = CreateEnvironmentItem(environment);
            Environments.Add(existing);
        }
        else
        {
            existing.Name = environment.Name;
            existing.BaseUrl = environment.BaseUrl;
            existing.ProjectId = environment.ProjectId;
            existing.SortOrder = environment.SortOrder;
        }

        UpdateActiveFlags(environment.Id);
        _isUpdatingSelection = true;
        SelectedEnvironment = existing;
        _isUpdatingSelection = false;
        NotifySelectionState();
    }

    private void ApplySavedVariables(IReadOnlyList<EnvironmentVariableDto> variables)
    {
        EnvironmentVariables.ReplaceWith(variables.Select(CreateVariableItem));
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

        NotifySelectionState();
        if (_isUpdatingSelection)
        {
            return;
        }

        var cancellationToken = CancellationTokenSourceHelper.Refresh(ref _activateEnvironmentCancellationTokenSource).Token;
        _ = ActivateEnvironmentSelectionAsync(value, cancellationToken);
    }

    private async Task ActivateEnvironmentSelectionAsync(ProjectEnvironmentItemViewModel? value, CancellationToken cancellationToken)
    {
        try
        {
            EnvironmentVariables.Clear();
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

            await LoadVariablesAsync(value.Id, cancellationToken);
            SelectedEnvironmentChanged?.Invoke(value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
