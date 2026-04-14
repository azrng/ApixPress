using System.Collections.Specialized;
using System.ComponentModel;
using ApixPress.App.Models.DTOs;
using Azrng.Core.Results;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
    private void OnSelectedEnvironmentChanged(ProjectEnvironmentItemViewModel? environment)
    {
        StatusMessage = environment is null
            ? "当前项目尚未配置环境。"
            : $"当前环境已切换为：{environment.Name}";
        NotifyWorkspaceEditorState();
    }

    private void OnWorkspaceTabsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<RequestWorkspaceTabViewModel>())
            {
                item.IsActive = ReferenceEquals(item, ActiveWorkspaceTab);
            }
        }

        OnPropertyChanged(nameof(WorkspaceTabs));
        OnPropertyChanged(nameof(VisibleWorkspaceTabs));
        NotifyShellState();
    }

    private void OnWorkspaceTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not RequestWorkspaceTabViewModel tab)
        {
            return;
        }

        if (!ReferenceEquals(tab, ActiveWorkspaceTab))
        {
            if (e.PropertyName == nameof(RequestWorkspaceTabViewModel.HeaderText))
            {
                OnPropertyChanged(nameof(VisibleWorkspaceTabs));
                NotifyShellState();
            }

            return;
        }

        if (e.PropertyName is nameof(RequestWorkspaceTabViewModel.SelectedMethod)
            or nameof(RequestWorkspaceTabViewModel.RequestUrl)
            or nameof(RequestWorkspaceTabViewModel.InterfaceFolderPath)
            or nameof(RequestWorkspaceTabViewModel.HttpCaseName)
            or nameof(RequestWorkspaceTabViewModel.EntryType)
            or nameof(RequestWorkspaceTabViewModel.ShowInTabStrip)
            or nameof(RequestWorkspaceTabViewModel.HeaderText))
        {
            OnPropertyChanged(nameof(VisibleWorkspaceTabs));
            NotifyWorkspaceEditorState();
        }
    }

    private void OnWorkspaceConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var tab = WorkspaceTabs.FirstOrDefault(item => ReferenceEquals(item.ConfigTab, sender));
        if (tab is null || !ReferenceEquals(tab, ActiveWorkspaceTab))
        {
            return;
        }

        NotifyWorkspaceEditorState();
    }

    private void OnWorkspaceConfigCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var tab = WorkspaceTabs.FirstOrDefault(item =>
            ReferenceEquals(item.ConfigTab.QueryParameters, sender)
            || ReferenceEquals(item.ConfigTab.Headers, sender)
            || ReferenceEquals(item.ConfigTab.FormFields, sender));
        if (tab is null || !ReferenceEquals(tab, ActiveWorkspaceTab))
        {
            return;
        }

        NotifyWorkspaceEditorState();
    }

    private void NotifyWorkspaceEditorState()
    {
        OnPropertyChanged(nameof(ConfigTab));
        OnPropertyChanged(nameof(ResponseSection));
        OnPropertyChanged(nameof(SelectedMethod));
        OnPropertyChanged(nameof(RequestUrl));
        OnPropertyChanged(nameof(CurrentInterfaceFolderPath));
        OnPropertyChanged(nameof(CurrentHttpCaseName));
        OnPropertyChanged(nameof(CurrentHttpInterfaceName));
        OnPropertyChanged(nameof(CurrentHttpInterfaceDisplayName));
        OnPropertyChanged(nameof(CurrentQuickRequestName));
        OnPropertyChanged(nameof(IsHttpDebugEditorMode));
        OnPropertyChanged(nameof(IsHttpDesignEditorMode));
        OnPropertyChanged(nameof(IsHttpDocumentPreviewMode));
        OnPropertyChanged(nameof(ShowHttpWorkbenchContent));
        OnPropertyChanged(nameof(ShowHttpDocumentPreviewContent));
        OnPropertyChanged(nameof(HasHttpDocumentParameters));
        OnPropertyChanged(nameof(HasHttpDocumentHeaders));
        OnPropertyChanged(nameof(HasHttpDocumentRequestDetails));
        OnPropertyChanged(nameof(ShowHttpDocumentRequestEmpty));
        OnPropertyChanged(nameof(CurrentHttpDocumentBodyModeText));
        OnPropertyChanged(nameof(CurrentHttpDocumentUrl));
        OnPropertyChanged(nameof(CurrentHttpDocumentResponseSummary));
        OnPropertyChanged(nameof(CurrentHttpDocumentBodyPreview));
        OnPropertyChanged(nameof(CurrentHttpDocumentCurlSnippet));
        NotifyShellState();
    }

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

    partial void OnActiveWorkspaceTabChanged(RequestWorkspaceTabViewModel? oldValue, RequestWorkspaceTabViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsActive = false;
        }

        if (newValue is not null)
        {
            newValue.IsActive = true;
            SelectedWorkspaceSection = WorkspaceSections.InterfaceManagement;
            StatusMessage = newValue.IsLandingTab
                ? "已切换到新建页。"
                : $"已切换到标签：{newValue.HeaderText}";
        }

        if (newValue is null || !newValue.IsQuickRequestTab)
        {
            IsQuickRequestSaveDialogOpen = false;
        }

        NotifyWorkspaceEditorState();
    }

    private void NotifyShellState()
    {
        OnPropertyChanged(nameof(TabTitle));
        OnPropertyChanged(nameof(ProjectSummary));
        OnPropertyChanged(nameof(CurrentEnvironmentLabel));
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
        OnPropertyChanged(nameof(IsImportFileMode));
        OnPropertyChanged(nameof(IsImportUrlMode));
        OnPropertyChanged(nameof(ShowProjectImportDialogStatus));
        OnPropertyChanged(nameof(HasPendingImportPreview));
        OnPropertyChanged(nameof(PendingImportOverwriteTitle));
        OnPropertyChanged(nameof(PendingImportOverwriteSummary));
        OnPropertyChanged(nameof(PendingImportOverwriteDetailText));
        OnPropertyChanged(nameof(HasSelectedImportFile));
        OnPropertyChanged(nameof(SelectedImportFileName));
        OnPropertyChanged(nameof(SelectedImportFileSummary));
        OnPropertyChanged(nameof(HasImportedApiDocuments));
        OnPropertyChanged(nameof(ShowImportedApiDocumentsEmptyState));
        OnPropertyChanged(nameof(ImportedApiDocumentCountText));
        OnPropertyChanged(nameof(CurrentProjectSettingsTitle));
        OnPropertyChanged(nameof(CurrentProjectSettingsSubtitle));
        OnPropertyChanged(nameof(ShowImportStatusInfo));
        OnPropertyChanged(nameof(ShowImportStatusSuccess));
        OnPropertyChanged(nameof(ShowImportStatusError));
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
        OnPropertyChanged(nameof(CurrentEditorTitle));
        OnPropertyChanged(nameof(CurrentEditorDescription));
        OnPropertyChanged(nameof(CurrentEditorPrimaryActionText));
        OnPropertyChanged(nameof(CurrentEditorUrlWatermark));
        OnPropertyChanged(nameof(ShowEditorBaseUrlPrefix));
        OnPropertyChanged(nameof(CurrentEditorBaseUrlPrefix));
        OnPropertyChanged(nameof(CurrentHttpInterfaceBaseUrl));
        OnPropertyChanged(nameof(ShowSaveHttpCaseAction));
        OnPropertyChanged(nameof(CurrentEditorBaseUrlCaption));
        OnPropertyChanged(nameof(CurrentResponseValidationResultText));
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

    private sealed class PendingImportRequest
    {
        public PendingImportRequest(
            Func<CancellationToken, Task<IResultModel<ApiDocumentDto>>> importAction,
            Func<ApiDocumentDto, string> buildSuccessMessage)
        {
            ImportAction = importAction;
            BuildSuccessMessage = buildSuccessMessage;
        }

        public Func<CancellationToken, Task<IResultModel<ApiDocumentDto>>> ImportAction { get; }

        public Func<ApiDocumentDto, string> BuildSuccessMessage { get; }
    }
}
