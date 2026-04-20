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

    partial void OnSelectedWorkspaceSectionChanged(string value)
    {
        SyncWorkspaceNavigationSelection();
        Settings.NotifyWorkspaceSectionChanged();
        OnPropertyChanged(nameof(IsInterfaceManagementSection));
        OnPropertyChanged(nameof(IsRequestHistorySection));
        OnPropertyChanged(nameof(IsProjectSettingsSection));
        OnPropertyChanged(nameof(ShowInterfaceManagementLanding));
        OnPropertyChanged(nameof(ShowRequestEditorWorkspace));
    }

    partial void OnSelectedWorkspaceNavigationItemChanged(ProjectWorkspaceNavItemViewModel? value)
    {
        if (value is null || string.Equals(SelectedWorkspaceSection, value.SectionKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SelectedWorkspaceSection = value.SectionKey;
    }
}
