using ApixPress.App.Models.DTOs;

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

    partial void OnSelectedImportDataModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsImportFileMode));
        OnPropertyChanged(nameof(IsImportUrlMode));
    }

    partial void OnIsImportDataBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditImportData));
        OnPropertyChanged(nameof(ShowImportedApiDocumentsEmptyState));
    }

    partial void OnSelectedImportFilePathChanged(string value)
    {
        OnPropertyChanged(nameof(HasSelectedImportFile));
        OnPropertyChanged(nameof(SelectedImportFileName));
        OnPropertyChanged(nameof(SelectedImportFileSummary));
    }

    partial void OnImportDataStatusStateChanged(string value)
    {
        OnPropertyChanged(nameof(ShowImportStatusInfo));
        OnPropertyChanged(nameof(ShowImportStatusSuccess));
        OnPropertyChanged(nameof(ShowImportStatusError));
        OnPropertyChanged(nameof(ShowProjectImportDialogStatus));
    }

    partial void OnPendingDeleteWorkspaceItemChanged(ExplorerItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasPendingWorkspaceDeleteTarget));
        OnPropertyChanged(nameof(PendingWorkspaceDeleteTitle));
        OnPropertyChanged(nameof(PendingWorkspaceDeleteDescription));
    }

    partial void OnPendingImportPreviewChanged(ApiImportPreviewDto? value)
    {
        OnPropertyChanged(nameof(HasPendingImportPreview));
        OnPropertyChanged(nameof(PendingImportOverwriteTitle));
        OnPropertyChanged(nameof(PendingImportOverwriteSummary));
        OnPropertyChanged(nameof(PendingImportOverwriteDetailText));
    }
}
