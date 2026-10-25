using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace ApixPress.App.Views.Converters;

/// <summary>按项目标识稳定选择头像底色；不保存额外字段，也不依赖随机数。</summary>
public sealed class ProjectAvatarBrushConverter : IValueConverter
{
    private static readonly string[] ResourceKeys =
    [
        "Brush.Project.Avatar.Teal", "Brush.Project.Avatar.Amber",
        "Brush.Project.Avatar.Blue", "Brush.Project.Avatar.Rose"
    ];

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        uint hash = 2166136261;
        foreach (var character in value as string ?? string.Empty)
        {
            hash = unchecked((hash ^ character) * 16777619);
        }

        return Application.Current?.FindResource(ResourceKeys[(int)(hash % (uint)ResourceKeys.Length)]);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("项目头像颜色仅支持单向显示。");
}
