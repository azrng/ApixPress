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
        Editor.NotifyStateChanged();
        NotifyShellState();
    }
}
