namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
    private void OnWorkspaceActiveWorkspaceTabChanged(RequestWorkspaceTabViewModel? oldValue, RequestWorkspaceTabViewModel? newValue)
    {
        if (newValue is null || !newValue.IsQuickRequestTab)
        {
            QuickRequestSave.Dismiss();
        }
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
