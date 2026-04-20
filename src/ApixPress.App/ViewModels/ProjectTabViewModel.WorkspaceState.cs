using System.Collections.Specialized;
using System.ComponentModel;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
    private void OnWorkspaceTabsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<RequestWorkspaceTabViewModel>())
            {
                item.IsActive = ReferenceEquals(item, ActiveWorkspaceTab);
            }
        }

        SyncVisibleWorkspaceTabs();
        OnPropertyChanged(nameof(WorkspaceTabs));
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
            if (e.PropertyName is nameof(RequestWorkspaceTabViewModel.EntryType)
                or nameof(RequestWorkspaceTabViewModel.ShowInTabStrip))
            {
                SyncVisibleWorkspaceTabs();
            }

            NotifyShellState();
            return;
        }

        if (e.PropertyName is nameof(RequestWorkspaceTabViewModel.EntryType)
            or nameof(RequestWorkspaceTabViewModel.ShowInTabStrip))
        {
            SyncVisibleWorkspaceTabs();
        }

        if (e.PropertyName is nameof(RequestWorkspaceTabViewModel.SelectedMethod)
            or nameof(RequestWorkspaceTabViewModel.RequestUrl)
            or nameof(RequestWorkspaceTabViewModel.InterfaceFolderPath)
            or nameof(RequestWorkspaceTabViewModel.HttpCaseName)
            or nameof(RequestWorkspaceTabViewModel.EntryType)
            or nameof(RequestWorkspaceTabViewModel.ShowInTabStrip)
            or nameof(RequestWorkspaceTabViewModel.HeaderText))
        {
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
            QuickRequestSave.Dismiss();
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
}
