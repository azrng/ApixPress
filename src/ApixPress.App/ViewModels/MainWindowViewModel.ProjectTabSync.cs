using System.Collections.Specialized;
using ApixPress.App.Helpers;

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
            _systemDataService,
            _projectWorkspaceService,
            _environmentVariableService,
            _apiWorkspaceService,
            _filePickerService,
            _appNotificationService,
            _projectDataExportService,
            _projectHttpSettingsService,
            _clipboardService,
            _dialogService,
            HandleProjectDeletedFromSettingsAsync);
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

        // 赋值同步触发 ProjectTabHostView 的内容切换（有宿主缓存，正常应毫秒级）
        PerfLog.Measure("项目标签·激活切换", () => ActiveProjectTab = tab);
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

    private async Task HandleProjectDeletedFromSettingsAsync(string projectId)
    {
        await ProjectPanel.LoadProjectsAsync(autoSelect: false);

        var deletedTabs = ProjectTabs
            .Where(tab => string.Equals(tab.ProjectId, projectId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var tab in deletedTabs)
        {
            ProjectTabs.Remove(tab);
            ReleaseProjectTab(tab);
        }

        if (ActiveProjectTab is not null && !ProjectTabs.Contains(ActiveProjectTab))
        {
            ActiveProjectTab = ProjectTabs.FirstOrDefault();
        }

        if (ActiveProjectTab is null)
        {
            StatusMessage = "项目已删除，已返回项目列表。";
        }
        else
        {
            StatusMessage = ActiveProjectTab.StatusMessage;
        }

        NotifyActiveProjectTabBindings();
        NotifyShellState();
    }
}
