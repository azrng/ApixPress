using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Helpers;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ProjectEnvironmentItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string id = string.Empty;

    [ObservableProperty]
    private string projectId = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string baseUrl = string.Empty;

    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private int sortOrder;

    // 环境抽屉内当前展开编辑的卡片
    [ObservableProperty]
    private bool isSelected;

    public ProjectEnvironmentItemViewModel()
    {
        Variables.CollectionChanged += OnVariablesCollectionChanged;
    }

    // 变量集合挂环境自身：抽屉多卡片平铺时每个环境的变量可同时展开编辑；批量集合避免加载时 Clear+Add 逐条通知
    public BatchObservableCollection<EnvironmentVariableItemViewModel> Variables { get; } = [];

    public bool HasVariables => Variables.Count > 0;

    public string VariableCountText => HasVariables
        ? $"{Variables.Count} 个变量 · 发送时自动替换 {{{{变量名}}}}"
        : "暂无变量 · 发送时自动替换 {{变量名}}";

    public string DisplayName => IsActive ? $"{Name}（当前）" : Name;
    public string CompactDisplayName => Name;
    public string DisplayBaseUrl => string.IsNullOrWhiteSpace(BaseUrl) ? "未配置前置 URL" : BaseUrl;

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(CompactDisplayName));
    }

    partial void OnIsActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(CompactDisplayName));
    }

    partial void OnBaseUrlChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayBaseUrl));
    }

    private void OnVariablesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasVariables));
        OnPropertyChanged(nameof(VariableCountText));
    }
}
