using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class EnvironmentPanelViewModel : ViewModelBase
{
    private readonly IEnvironmentVariableService _environmentVariableService;
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

    public async Task LoadProjectAsync(string projectId, string? preferredEnvironmentId = null)
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

        var environments = await _environmentVariableService.GetEnvironmentsAsync(projectId, CancellationToken.None);
        var items = environments.Select(environment => new ProjectEnvironmentItemViewModel
        {
            Id = environment.Id,
            ProjectId = environment.ProjectId,
            Name = environment.Name,
            BaseUrl = environment.BaseUrl,
            IsActive = environment.IsActive,
            SortOrder = environment.SortOrder
        }).ToList();

        foreach (var item in items)
        {
            Environments.Add(item);
        }

        _isUpdatingSelection = true;
        SelectedEnvironment = ResolveSelection(items, preferredEnvironmentId);
        _isUpdatingSelection = false;

        if (SelectedEnvironment is not null)
        {
            await LoadVariablesAsync(SelectedEnvironment.Id);
            UpdateActiveFlags(SelectedEnvironment.Id);
        }

        NotifySelectionState();
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
            await LoadProjectAsync(_currentProjectId, result.Data.Id);
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

        foreach (var item in EnvironmentVariables)
        {
            var result = await _environmentVariableService.SaveVariableAsync(new EnvironmentVariableDto
            {
                Id = item.Id,
                EnvironmentId = saveResult.Data.Id,
                EnvironmentName = saveResult.Data.Name,
                Key = item.Key,
                Value = item.Value,
                IsEnabled = item.IsEnabled
            }, CancellationToken.None);

            if (result.IsSuccess && result.Data is not null)
            {
                item.Id = result.Data.Id;
                item.EnvironmentId = result.Data.EnvironmentId;
                item.EnvironmentName = result.Data.EnvironmentName;
            }
        }

        await LoadProjectAsync(_currentProjectId, saveResult.Data.Id);
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
            await LoadProjectAsync(_currentProjectId, nextSelection?.Id);
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

    private async Task LoadVariablesAsync(string environmentId)
    {
        EnvironmentVariables.Clear();
        var items = await _environmentVariableService.GetVariablesAsync(environmentId, CancellationToken.None);
        foreach (var item in items)
        {
            EnvironmentVariables.Add(new EnvironmentVariableItemViewModel
            {
                Id = item.Id,
                EnvironmentId = item.EnvironmentId,
                EnvironmentName = item.EnvironmentName,
                Key = item.Key,
                Value = item.Value,
                IsEnabled = item.IsEnabled
            });
        }
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

    partial void OnSelectedEnvironmentChanged(ProjectEnvironmentItemViewModel? value)
    {
        NotifySelectionState();
        if (_isUpdatingSelection)
        {
            return;
        }

        _ = ActivateEnvironmentSelectionAsync(value);
    }

    private async Task ActivateEnvironmentSelectionAsync(ProjectEnvironmentItemViewModel? value)
    {
        EnvironmentVariables.Clear();
        if (value is null || string.IsNullOrWhiteSpace(value.Id) || string.IsNullOrWhiteSpace(_currentProjectId))
        {
            SelectedEnvironmentChanged?.Invoke(value);
            return;
        }

        var result = await _environmentVariableService.SetActiveEnvironmentAsync(_currentProjectId, value.Id, CancellationToken.None);
        if (result.IsSuccess)
        {
            UpdateActiveFlags(value.Id);
        }

        await LoadVariablesAsync(value.Id);
        SelectedEnvironmentChanged?.Invoke(value);
    }
}
