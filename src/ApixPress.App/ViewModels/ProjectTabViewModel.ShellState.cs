namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
    private void NotifyShellState()
    {
        OnPropertyChanged(nameof(TabTitle));
        OnPropertyChanged(nameof(ProjectSummary));
        OnPropertyChanged(nameof(CurrentEnvironmentLabel));
        OnPropertyChanged(nameof(CurrentEnvironmentSummaryText));
        OnPropertyChanged(nameof(CurrentBaseUrlText));
        OnPropertyChanged(nameof(HasEnvironmentContext));
        OnPropertyChanged(nameof(HasSavedRequests));
        OnPropertyChanged(nameof(VisibleWorkspaceTabs));
        OnPropertyChanged(nameof(HasHistory));
        OnPropertyChanged(nameof(ShowHistoryEmptyState));
        OnPropertyChanged(nameof(ImportedApiDocumentSummaryText));
        OnPropertyChanged(nameof(IsQuickRequestEditor));
        OnPropertyChanged(nameof(IsHttpInterfaceEditor));
        OnPropertyChanged(nameof(IsRequestEditorOpen));
        OnPropertyChanged(nameof(SavedRequestCountText));
        OnPropertyChanged(nameof(HistoryCountText));
        OnPropertyChanged(nameof(EnvironmentCountText));
        ShellStateChanged?.Invoke(this);
    }
}
