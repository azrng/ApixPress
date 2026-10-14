using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.ViewModels.Base;
using System.Windows.Input;

namespace ApixPress.App.ViewModels;

public partial class ProjectWorkspaceItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string id = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private bool isDefault;

    // 首页项目卡右键菜单命令：直接注入行项以跨 ContextMenu PopupRoot 保持绑定可用
    public ICommand? OpenProjectCommand { get; set; }
    public ICommand? OpenProjectSettingsCommand { get; set; }

    public string DisplayName => IsDefault ? $"{Name}（默认）" : Name;
    public string SummaryText => string.IsNullOrWhiteSpace(Description) ? "无备注" : Description;
    public string CategoryText => "HTTP";
    public string AvatarText => string.IsNullOrWhiteSpace(Name) ? "A" : Name[..1].ToUpperInvariant();

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(AvatarText));
    }

    partial void OnDescriptionChanged(string value)
    {
        OnPropertyChanged(nameof(SummaryText));
    }

    partial void OnIsDefaultChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
