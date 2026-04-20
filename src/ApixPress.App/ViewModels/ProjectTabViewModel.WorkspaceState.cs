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
