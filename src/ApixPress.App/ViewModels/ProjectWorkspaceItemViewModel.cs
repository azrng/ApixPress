using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ProjectWorkspaceItemViewModel : ViewModelBase
{
    // 项目图标渐变配色（对标 Apifox 多彩项目图标），按名称稳定取模分配
    private static readonly (Color From, Color To)[] AvatarGradients =
    [
        (Color.Parse("#7C6CF0"), Color.Parse("#5B8DEF")),
        (Color.Parse("#2DD4BF"), Color.Parse("#22A06B")),
        (Color.Parse("#F472B6"), Color.Parse("#C2418C")),
        (Color.Parse("#F59E0B"), Color.Parse("#D97706")),
        (Color.Parse("#38BDF8"), Color.Parse("#0284C7")),
        (Color.Parse("#F87171"), Color.Parse("#B91C1C")),
    ];
    [ObservableProperty]
    private string id = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private bool isDefault;

    public string DisplayName => IsDefault ? $"{Name}（默认）" : Name;
    public string SummaryText => string.IsNullOrWhiteSpace(Description) ? "暂无备注信息，可进入项目详情继续完善说明。" : Description;
    public string CategoryText => "HTTP";
    public string AvatarText => string.IsNullOrWhiteSpace(Name) ? "A" : Name[..1].ToUpperInvariant();

    // 按名称稳定哈希取模，让不同项目获得固定且分布均匀的彩色图标底色
    public IBrush AvatarBrush
    {
        get
        {
            var hash = 0;
            foreach (var ch in Name)
            {
                hash = (hash * 31 + ch) & 0x7FFFFFFF;
            }

            var (from, to) = AvatarGradients[hash % AvatarGradients.Length];
            return new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(from, 0),
                    new GradientStop(to, 1),
                },
            };
        }
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(AvatarText));
        OnPropertyChanged(nameof(AvatarBrush));
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
