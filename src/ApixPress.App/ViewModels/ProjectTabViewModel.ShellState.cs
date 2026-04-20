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
        OnPropertyChanged(nameof(HasQuickRequestEntries));
        OnPropertyChanged(nameof(HasInterfaceEntries));
        OnPropertyChanged(nameof(ShowInterfaceEntriesEmptyState));
        OnPropertyChanged(nameof(ShowQuickRequestEntriesEmptyState));
        OnPropertyChanged(nameof(HasHistory));
        OnPropertyChanged(nameof(ShowSavedRequestsEmptyState));
        OnPropertyChanged(nameof(ShowHistoryEmptyState));
        OnPropertyChanged(nameof(IsInterfaceManagementSection));
        OnPropertyChanged(nameof(IsRequestHistorySection));
        OnPropertyChanged(nameof(IsProjectSettingsSection));
        OnPropertyChanged(nameof(IsProjectSettingsOverviewSelected));
        OnPropertyChanged(nameof(IsProjectSettingsImportDataSelected));
        OnPropertyChanged(nameof(ShowProjectSettingsOverviewSection));
        OnPropertyChanged(nameof(ShowProjectSettingsImportDataSection));
        OnPropertyChanged(nameof(ImportedApiDocumentSummaryText));
        OnPropertyChanged(nameof(CurrentProjectSettingsTitle));
        OnPropertyChanged(nameof(CurrentProjectSettingsSubtitle));
        OnPropertyChanged(nameof(IsQuickRequestEditor));
        OnPropertyChanged(nameof(IsHttpInterfaceEditor));
        OnPropertyChanged(nameof(IsRequestEditorOpen));
        OnPropertyChanged(nameof(ShowInterfaceManagementLanding));
        OnPropertyChanged(nameof(ShowRequestEditorWorkspace));
        OnPropertyChanged(nameof(SavedRequestCountText));
        OnPropertyChanged(nameof(HistoryCountText));
        OnPropertyChanged(nameof(EnvironmentCountText));
        OnPropertyChanged(nameof(ProjectSettingsDescription));
        OnPropertyChanged(nameof(InterfaceSectionHint));
        OnPropertyChanged(nameof(QuickRequestSectionHint));
        OnPropertyChanged(nameof(HasPendingWorkspaceDeleteTarget));
        OnPropertyChanged(nameof(PendingWorkspaceDeleteTitle));
        OnPropertyChanged(nameof(PendingWorkspaceDeleteDescription));
        ShellStateChanged?.Invoke(this);
    }

    private void SyncWorkspaceNavigationSelection()
    {
        var selectedItem = WorkspaceNavigationItems.FirstOrDefault(item =>
            string.Equals(item.SectionKey, SelectedWorkspaceSection, StringComparison.OrdinalIgnoreCase));

        foreach (var navigationItem in WorkspaceNavigationItems)
        {
            navigationItem.IsSelected = ReferenceEquals(navigationItem, selectedItem);
        }

        if (!ReferenceEquals(SelectedWorkspaceNavigationItem, selectedItem))
        {
            SelectedWorkspaceNavigationItem = selectedItem;
        }
    }
}
