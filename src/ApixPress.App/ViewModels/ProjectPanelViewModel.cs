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
    }

    public ObservableCollection<ProjectWorkspaceItemViewModel> Projects { get; } = [];
    public ObservableCollection<ProjectWorkspaceItemViewModel> FilteredProjects { get; } = [];

    [ObservableProperty]
    private ProjectWorkspaceItemViewModel? selectedProject;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateProjectCommand))]
    private string draftProjectName = string.Empty;

    [ObservableProperty]
    private string draftProjectDescription = string.Empty;

    [ObservableProperty]
    private string searchText = string.Empty;

    public bool HasSelectedProject => SelectedProject is not null;
    public bool HasProjects => FilteredProjects.Count > 0;

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
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreateProject))]
    private async Task CreateProjectAsync()
    {
        var projectName = DraftProjectName.Trim();
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return;
        }

        var result = await _projectWorkspaceService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = projectName,
            Description = DraftProjectDescription.Trim(),
            IsDefault = Projects.Count == 0
        }, CancellationToken.None);

        if (result.IsSuccess && result.Data is not null)
        {
            DraftProjectName = string.Empty;
            DraftProjectDescription = string.Empty;
            await LoadProjectsAsync(result.Data.Id);
            ProjectCreated?.Invoke();
        }
    }

    private bool CanCreateProject()
    {
        return !string.IsNullOrWhiteSpace(DraftProjectName);
    }

    [RelayCommand]
    private void UseProjectTemplate()
    {
        DraftProjectName = BuildNextProjectName();
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
            await LoadProjectsAsync(result.Data.Id);
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
        await LoadProjectsAsync(autoSelect: false);
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
            await LoadProjectsAsync(result.Data.Id);
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
            if (!Projects.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
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
}
