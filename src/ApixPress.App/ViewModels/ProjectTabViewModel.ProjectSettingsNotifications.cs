namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
    partial void OnSelectedWorkspaceSectionChanged(string value)
    {
        SyncWorkspaceNavigationSelection();
        OnPropertyChanged(nameof(IsInterfaceManagementSection));
        OnPropertyChanged(nameof(IsRequestHistorySection));
        OnPropertyChanged(nameof(IsProjectSettingsSection));
        OnPropertyChanged(nameof(ShowProjectSettingsOverviewSection));
        OnPropertyChanged(nameof(ShowProjectSettingsImportDataSection));
        OnPropertyChanged(nameof(ShowInterfaceManagementLanding));
        OnPropertyChanged(nameof(ShowRequestEditorWorkspace));
    }

    partial void OnSelectedProjectSettingsSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsProjectSettingsOverviewSelected));
        OnPropertyChanged(nameof(IsProjectSettingsImportDataSelected));
        OnPropertyChanged(nameof(ShowProjectSettingsOverviewSection));
        OnPropertyChanged(nameof(ShowProjectSettingsImportDataSection));
        OnPropertyChanged(nameof(CurrentProjectSettingsTitle));
        OnPropertyChanged(nameof(CurrentProjectSettingsSubtitle));
    }

    partial void OnSelectedWorkspaceNavigationItemChanged(ProjectWorkspaceNavItemViewModel? value)
    {
        if (value is null || string.Equals(SelectedWorkspaceSection, value.SectionKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SelectedWorkspaceSection = value.SectionKey;
    }

    partial void OnPendingDeleteWorkspaceItemChanged(ExplorerItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasPendingWorkspaceDeleteTarget));
        OnPropertyChanged(nameof(PendingWorkspaceDeleteTitle));
        OnPropertyChanged(nameof(PendingWorkspaceDeleteDescription));
    }
}
