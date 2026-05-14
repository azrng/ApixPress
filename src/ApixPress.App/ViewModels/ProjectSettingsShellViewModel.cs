using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ApixPress.App.Messages;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ProjectSettingsShellViewModel : ViewModelBase
{
    private static class Sections
    {
        public const string Overview = "overview";
        public const string ImportData = "import-data";
        public const string ExportData = "export-data";
    }

    private readonly Action _showProjectSettingsWorkspace;
    private readonly Action _dismissImportDialog;
    private readonly Func<bool> _isProjectSettingsSection;
    private readonly string _projectId;
    private readonly Func<string> _getProjectName;
    private readonly Func<string> _getProjectDescription;
    private readonly Func<Task> _ensureImportedDocumentsLoadedAsync;
    private readonly Func<Task> _reloadAfterProjectDataClearedAsync;
    private readonly Func<string, Task> _handleProjectDeletedAsync;
    private readonly ISystemDataService _systemDataService;
    private readonly IProjectWorkspaceService _projectWorkspaceService;
    private readonly IMessenger _messenger;

    public ProjectSettingsShellViewModel(
        Action showProjectSettingsWorkspace,
        Action dismissImportDialog,
        Func<bool> isProjectSettingsSection,
        string projectId,
        Func<string> getProjectName,
        Func<string> getProjectDescription,
        Func<Task> ensureImportedDocumentsLoadedAsync,
        Func<Task> reloadAfterProjectDataClearedAsync,
        Func<string, Task> handleProjectDeletedAsync,
        ISystemDataService systemDataService,
        IProjectWorkspaceService projectWorkspaceService,
        IMessenger messenger)
    {
        _showProjectSettingsWorkspace = showProjectSettingsWorkspace;
        _dismissImportDialog = dismissImportDialog;
        _isProjectSettingsSection = isProjectSettingsSection;
        _projectId = projectId;
        _getProjectName = getProjectName;
        _getProjectDescription = getProjectDescription;
        _ensureImportedDocumentsLoadedAsync = ensureImportedDocumentsLoadedAsync;
        _reloadAfterProjectDataClearedAsync = reloadAfterProjectDataClearedAsync;
        _handleProjectDeletedAsync = handleProjectDeletedAsync;
        _systemDataService = systemDataService;
        _projectWorkspaceService = projectWorkspaceService;
        _messenger = messenger;
    }

    public bool IsOverviewSelected => SelectedSection == Sections.Overview;
    public bool IsImportDataSelected => SelectedSection == Sections.ImportData;
    public bool IsExportDataSelected => SelectedSection == Sections.ExportData;
    public bool ShowOverviewSection => _isProjectSettingsSection() && IsOverviewSelected;
    public bool ShowImportDataSection => _isProjectSettingsSection() && IsImportDataSelected;
    public bool ShowExportDataSection => _isProjectSettingsSection() && IsExportDataSelected;
    public string ProjectDescription => string.IsNullOrWhiteSpace(_getProjectDescription())
        ? ProjectSettingsTexts.EmptyDescription
        : _getProjectDescription();
    public string CurrentTitle => SelectedSection switch
    {
        Sections.ImportData => ProjectSettingsTexts.ImportDataTitle,
        Sections.ExportData => ProjectSettingsTexts.ExportDataTitle,
        _ => ProjectSettingsTexts.OverviewTitle
    };
    public string CurrentSubtitle => SelectedSection switch
    {
        Sections.ImportData => ProjectSettingsTexts.ImportSubtitle,
        Sections.ExportData => ProjectSettingsTexts.ExportSubtitle,
        _ => string.Empty
    };
    public string ClearProjectDataButtonText => IsProjectDangerOperationBusy ? "处理中..." : ProjectSettingsTexts.ClearProjectDataAction;
    public string DeleteProjectButtonText => IsProjectDangerOperationBusy ? "处理中..." : ProjectSettingsTexts.DeleteProjectAction;
    public bool CanRunProjectDangerOperation => !IsProjectDangerOperationBusy;

    [ObservableProperty]
    private string selectedSection = Sections.Overview;

    [ObservableProperty]
    private bool isClearProjectDataConfirmDialogOpen;

    [ObservableProperty]
    private bool isDeleteProjectConfirmDialogOpen;

    [ObservableProperty]
    private bool isProjectDangerOperationBusy;

    [ObservableProperty]
    private string projectDangerOperationStatus = ProjectSettingsTexts.DangerOperationStatus;

    [RelayCommand]
    private void OpenWorkspace()
    {
        ShowOverviewInternal(ProjectSettingsTexts.OverviewDescription);
    }

    [RelayCommand]
    private void ShowOverview()
    {
        ShowOverviewInternal(ProjectSettingsTexts.OverviewDescription);
    }

    [RelayCommand]
    private async Task ShowImportDataAsync()
    {
        _showProjectSettingsWorkspace();
        SelectedSection = Sections.ImportData;
        _dismissImportDialog();
        await _ensureImportedDocumentsLoadedAsync();
        _messenger.Send(new StatusMessageRequest(ProjectSettingsTexts.ImportDescription));
        _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
    }

    [RelayCommand]
    private void ShowExportData()
    {
        _showProjectSettingsWorkspace();
        SelectedSection = Sections.ExportData;
        _dismissImportDialog();
        _messenger.Send(new StatusMessageRequest(ProjectSettingsTexts.ExportDescription));
        _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
    }

    [RelayCommand]
    private void RequestClearProjectData()
    {
        if (IsProjectDangerOperationBusy)
        {
            return;
        }

        IsClearProjectDataConfirmDialogOpen = true;
        ProjectDangerOperationStatus = ProjectSettingsTexts.ClearProjectDataPendingStatus;
        _messenger.Send(new StatusMessageRequest(ProjectSettingsTexts.ClearProjectDataPendingStatus));
        _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
    }

    [RelayCommand]
    private void CancelClearProjectData()
    {
        IsClearProjectDataConfirmDialogOpen = false;
        ProjectDangerOperationStatus = ProjectSettingsTexts.ClearProjectDataCancelledStatus;
        _messenger.Send(new StatusMessageRequest(ProjectSettingsTexts.ClearProjectDataCancelledStatus));
        _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
    }

    [RelayCommand]
    private async Task ConfirmClearProjectDataAsync()
    {
        if (IsProjectDangerOperationBusy)
        {
            return;
        }

        IsClearProjectDataConfirmDialogOpen = false;
        IsProjectDangerOperationBusy = true;
        ProjectDangerOperationStatus = ProjectSettingsTexts.ClearingProjectDataStatus;
        _messenger.Send(new StatusMessageRequest(ProjectSettingsTexts.ClearingProjectDataStatus));
        _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
        try
        {
            var result = await _systemDataService.ClearProjectAsync(_projectId, CancellationToken.None);
            if (!result.IsSuccess)
            {
                var failureMessage = string.IsNullOrWhiteSpace(result.Message)
                    ? ProjectSettingsTexts.ClearProjectDataFailureFallback
                    : result.Message;
                ProjectDangerOperationStatus = failureMessage;
                _messenger.Send(new StatusMessageRequest(failureMessage));
                return;
            }

            await _reloadAfterProjectDataClearedAsync();
            var successMessage = ProjectSettingsTexts.FormatClearProjectDataSuccess(_getProjectName());
            ProjectDangerOperationStatus = successMessage;
            _messenger.Send(new StatusMessageRequest(successMessage));
        }
        finally
        {
            IsProjectDangerOperationBusy = false;
            _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
        }
    }

    [RelayCommand]
    private void RequestDeleteProject()
    {
        if (IsProjectDangerOperationBusy)
        {
            return;
        }

        IsDeleteProjectConfirmDialogOpen = true;
        ProjectDangerOperationStatus = ProjectSettingsTexts.DeleteProjectPendingStatus;
        _messenger.Send(new StatusMessageRequest(ProjectSettingsTexts.DeleteProjectPendingStatus));
        _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
    }

    [RelayCommand]
    private void CancelDeleteProject()
    {
        IsDeleteProjectConfirmDialogOpen = false;
        ProjectDangerOperationStatus = ProjectSettingsTexts.DeleteProjectCancelledStatus;
        _messenger.Send(new StatusMessageRequest(ProjectSettingsTexts.DeleteProjectCancelledStatus));
        _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
    }

    [RelayCommand]
    private async Task ConfirmDeleteProjectAsync()
    {
        if (IsProjectDangerOperationBusy)
        {
            return;
        }

        IsDeleteProjectConfirmDialogOpen = false;
        IsProjectDangerOperationBusy = true;
        ProjectDangerOperationStatus = ProjectSettingsTexts.DeletingProjectStatus;
        _messenger.Send(new StatusMessageRequest(ProjectSettingsTexts.DeletingProjectStatus));
        _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
        try
        {
            var result = await _projectWorkspaceService.DeleteAsync(_projectId, CancellationToken.None);
            if (!result.IsSuccess)
            {
                var failureMessage = string.IsNullOrWhiteSpace(result.Message)
                    ? ProjectSettingsTexts.DeleteProjectFailureFallback
                    : result.Message;
                ProjectDangerOperationStatus = failureMessage;
                _messenger.Send(new StatusMessageRequest(failureMessage));
                IsProjectDangerOperationBusy = false;
                _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
                return;
            }

            ProjectDangerOperationStatus = ProjectSettingsTexts.FormatDeleteProjectSuccess(_getProjectName());
            IsProjectDangerOperationBusy = false;
            await _handleProjectDeletedAsync(_projectId);
        }
        catch
        {
            IsProjectDangerOperationBusy = false;
            _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
            throw;
        }
    }

    public void NotifyWorkspaceSectionChanged()
    {
        OnPropertyChanged(nameof(ShowOverviewSection));
        OnPropertyChanged(nameof(ShowImportDataSection));
        OnPropertyChanged(nameof(ShowExportDataSection));
    }

    public void NotifyProjectChanged()
    {
        OnPropertyChanged(nameof(ProjectDescription));
    }

    partial void OnSelectedSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsOverviewSelected));
        OnPropertyChanged(nameof(IsImportDataSelected));
        OnPropertyChanged(nameof(IsExportDataSelected));
        OnPropertyChanged(nameof(ShowOverviewSection));
        OnPropertyChanged(nameof(ShowImportDataSection));
        OnPropertyChanged(nameof(ShowExportDataSection));
        OnPropertyChanged(nameof(CurrentTitle));
        OnPropertyChanged(nameof(CurrentSubtitle));
    }

    partial void OnIsProjectDangerOperationBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(ClearProjectDataButtonText));
        OnPropertyChanged(nameof(DeleteProjectButtonText));
        OnPropertyChanged(nameof(CanRunProjectDangerOperation));
    }

    private void ShowOverviewInternal(string statusMessage)
    {
        _showProjectSettingsWorkspace();
        SelectedSection = Sections.Overview;
        _dismissImportDialog();
        _messenger.Send(new StatusMessageRequest(statusMessage));
        _messenger.Send(new WorkspaceStateChangedMessage(WorkspaceStateChangeFlags.ShellState));
    }
}
