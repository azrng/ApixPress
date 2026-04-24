using System.Collections.Specialized;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel
{
    private ProjectTabViewModel CreateProjectTab(ProjectWorkspaceItemViewModel project)
    {
        var tab = new ProjectTabViewModel(
            project,
            _requestExecutionService,
            _requestCaseService,
            _requestHistoryService,
            _environmentVariableService,
            _apiWorkspaceService,
            _filePickerService,
            _appNotificationService);
        tab.ShellStateChanged += OnProjectTabShellStateChanged;
        return tab;
    }

    private void ReleaseProjectTab(ProjectTabViewModel tab)
    {
        tab.ShellStateChanged -= OnProjectTabShellStateChanged;
        tab.Dispose();
    }

    private void ActivateProjectTabCore(ProjectTabViewModel tab)
    {
        if (IsDisposed)
        {
            return;
        }

        ActiveProjectTab = tab;
        IsEnvironmentManagerOpen = false;
    }

    private void OnProjectsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        SyncProjectTabsWithProjectList();
    }

    private void SyncProjectTabsWithProjectList()
    {
        var sourceLookup = ProjectPanel.Projects.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var removedTabs = ProjectTabs
            .Where(tab => !sourceLookup.ContainsKey(tab.ProjectId))
            .ToList();

        foreach (var tab in ProjectTabs)
        {
            if (sourceLookup.TryGetValue(tab.ProjectId, out var source))
            {
                SyncTabProject(tab, source);
            }
        }

        foreach (var tab in removedTabs)
        {
            ProjectTabs.Remove(tab);
            ReleaseProjectTab(tab);
        }

        if (ActiveProjectTab is not null && !ProjectTabs.Contains(ActiveProjectTab))
        {
            ActiveProjectTab = ProjectTabs.FirstOrDefault();
            if (ActiveProjectTab is null)
            {
                IsEnvironmentManagerOpen = false;
                StatusMessage = BrowserStatusText;
            }
        }

        NotifyShellState();
    }

    private static void SyncTabProject(ProjectTabViewModel tab, ProjectWorkspaceItemViewModel source)
    {
        tab.Project.Name = source.Name;
        tab.Project.Description = source.Description;
        tab.Project.IsDefault = source.IsDefault;
    }

    private void OnProjectTabsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        OnPropertyChanged(nameof(HasProjectTabs));
        NotifyShellState();
    }
}
