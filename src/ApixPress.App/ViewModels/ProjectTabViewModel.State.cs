using System.Collections.Specialized;
using System.ComponentModel;

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
}
