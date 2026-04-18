using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ProjectPanelViewModel : ViewModelBase
{
    private readonly IProjectWorkspaceService _projectWorkspaceService;
    private CancellationTokenSource? _loadProjectsCancellationTokenSource;
    private bool _isUpdatingSelection;
    private List<ProjectWorkspaceItemViewModel> _allProjects = [];

    public event Action<ProjectWorkspaceItemViewModel?>? SelectedProjectChanged;
    public event Action? ProjectCreated;

    public ProjectPanelViewModel(IProjectWorkspaceService projectWorkspaceService)
    {
        _projectWorkspaceService = projectWorkspaceService;
        Creation = new ProjectCreationViewModel(BuildNextProjectName, CreateProjectAsync);
    }

    public ObservableCollection<ProjectWorkspaceItemViewModel> Projects { get; } = [];
    public ObservableCollection<ProjectWorkspaceItemViewModel> FilteredProjects { get; } = [];
    public ProjectCreationViewModel Creation { get; }

    [ObservableProperty]
    private ProjectWorkspaceItemViewModel? selectedProject;

    [ObservableProperty]
    private string searchText = string.Empty;

    public bool HasSelectedProject => SelectedProject is not null;
    public bool HasProjects => FilteredProjects.Count > 0;
    public bool HasAnyProjects => Projects.Count > 0;

    public async Task LoadProjectsAsync(string? preferredProjectId = null, bool autoSelect = true)
    {
        var cancellationToken = CancellationTokenSourceHelper.Refresh(ref _loadProjectsCancellationTokenSource).Token;
        try
        {
            var projects = await _projectWorkspaceService.GetProjectsAsync(cancellationToken);
            _allProjects = projects.Select(project => new ProjectWorkspaceItemViewModel
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                IsDefault = project.IsDefault
            }).ToList();

            Projects.ReplaceWith(_allProjects);
            RefreshFilteredProjects();

            _isUpdatingSelection = true;
            SelectedProject = autoSelect ? ResolveSelection(_allProjects, preferredProjectId) : null;
            _isUpdatingSelection = false;
            OnPropertyChanged(nameof(HasSelectedProject));
            OnPropertyChanged(nameof(HasProjects));
            OnPropertyChanged(nameof(HasAnyProjects));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task<bool> CreateProjectAsync(string projectName, string projectDescription)
    {
        var result = await _projectWorkspaceService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = projectName,
            Description = projectDescription,
            IsDefault = Projects.Count == 0
        }, CancellationToken.None);

        if (!result.IsSuccess || result.Data is null)
        {
            return false;
        }

        var created = CreateProjectItem(result.Data);
        _allProjects.Add(created);
        Projects.Add(created);
        RefreshFilteredProjects();
        _isUpdatingSelection = true;
        SelectedProject = created;
        _isUpdatingSelection = false;
        OnPropertyChanged(nameof(HasSelectedProject));
        OnPropertyChanged(nameof(HasProjects));
        OnPropertyChanged(nameof(HasAnyProjects));
        ProjectCreated?.Invoke();
        return true;
    }

    [RelayCommand]
    private async Task SaveSelectedProjectAsync()
    {
        if (SelectedProject is null)
        {
            return;
        }

        var result = await _projectWorkspaceService.SaveAsync(new ProjectWorkspaceDto
        {
            Id = SelectedProject.Id,
            Name = SelectedProject.Name,
            Description = SelectedProject.Description,
            IsDefault = SelectedProject.IsDefault
        }, CancellationToken.None);

        if (result.IsSuccess && result.Data is not null)
        {
            ApplySavedProject(result.Data);
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedProjectAsync()
    {
        if (SelectedProject is null)
        {
            return;
        }

        await _projectWorkspaceService.DeleteAsync(SelectedProject.Id, CancellationToken.None);
        var deletedProjectId = SelectedProject.Id;
        var deletedItem = SelectedProject;
        _allProjects.RemoveAll(item => string.Equals(item.Id, deletedProjectId, StringComparison.OrdinalIgnoreCase));
        if (deletedItem is not null)
        {
            Projects.Remove(deletedItem);
            FilteredProjects.Remove(deletedItem);
        }

        _isUpdatingSelection = true;
        SelectedProject = null;
        _isUpdatingSelection = false;
        RefreshFilteredProjects();
        OnPropertyChanged(nameof(HasSelectedProject));
        OnPropertyChanged(nameof(HasProjects));
        OnPropertyChanged(nameof(HasAnyProjects));
    }

    [RelayCommand]
    private async Task SetSelectedProjectAsDefaultAsync()
    {
        if (SelectedProject is null)
        {
            return;
        }

        var result = await _projectWorkspaceService.SetDefaultAsync(SelectedProject.Id, CancellationToken.None);
        if (result.IsSuccess && result.Data is not null)
        {
            ApplySavedProject(result.Data);
        }
    }

    private ProjectWorkspaceItemViewModel? ResolveSelection(IReadOnlyList<ProjectWorkspaceItemViewModel> items, string? preferredProjectId)
    {
        if (!string.IsNullOrWhiteSpace(preferredProjectId))
        {
            var preferred = items.FirstOrDefault(item => string.Equals(item.Id, preferredProjectId, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
            {
                return preferred;
            }
        }

        if (!string.IsNullOrWhiteSpace(SelectedProject?.Id))
        {
            var current = items.FirstOrDefault(item => string.Equals(item.Id, SelectedProject.Id, StringComparison.OrdinalIgnoreCase));
            if (current is not null)
            {
                return current;
            }
        }

        return items.FirstOrDefault(item => item.IsDefault) ?? items.FirstOrDefault();
    }

    private string BuildNextProjectName()
    {
        var index = 1;
        while (true)
        {
            var name = $"新项目 {index}";
            if (!_allProjects.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return name;
            }

            index++;
        }
    }

    partial void OnSelectedProjectChanged(ProjectWorkspaceItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedProject));
        if (_isUpdatingSelection)
        {
            return;
        }

        SelectedProjectChanged?.Invoke(value);
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshFilteredProjects();
        OnPropertyChanged(nameof(HasProjects));
        OnPropertyChanged(nameof(HasAnyProjects));
    }

    private void RefreshFilteredProjects()
    {
        var keyword = SearchText.Trim();
        var items = string.IsNullOrWhiteSpace(keyword)
            ? _allProjects
            : _allProjects.Where(item =>
                    item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();

        FilteredProjects.ReplaceWith(items);
    }

    private void ApplySavedProject(ProjectWorkspaceDto project)
    {
        foreach (var item in _allProjects)
        {
            item.IsDefault = string.Equals(item.Id, project.Id, StringComparison.OrdinalIgnoreCase)
                ? project.IsDefault
                : item.IsDefault && !project.IsDefault
                    ? false
                    : item.IsDefault;
        }

        var existing = _allProjects.FirstOrDefault(item => string.Equals(item.Id, project.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = CreateProjectItem(project);
            _allProjects.Add(existing);
            Projects.Add(existing);
        }
        else
        {
            existing.Name = project.Name;
            existing.Description = project.Description;
            existing.IsDefault = project.IsDefault;
        }

        if (project.IsDefault)
        {
            foreach (var item in _allProjects.Where(item => !string.Equals(item.Id, project.Id, StringComparison.OrdinalIgnoreCase)))
            {
                item.IsDefault = false;
            }
        }

        RefreshFilteredProjects();
        _isUpdatingSelection = true;
        SelectedProject = existing;
        _isUpdatingSelection = false;
        OnPropertyChanged(nameof(HasSelectedProject));
        OnPropertyChanged(nameof(HasProjects));
        OnPropertyChanged(nameof(HasAnyProjects));
    }

    private static ProjectWorkspaceItemViewModel CreateProjectItem(ProjectWorkspaceDto project)
    {
        return new ProjectWorkspaceItemViewModel
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            IsDefault = project.IsDefault
        };
    }
}
