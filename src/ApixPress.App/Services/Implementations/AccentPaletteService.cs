using ApixPress.App.Services.Interfaces;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Azrng.Core.DependencyInjection;

namespace ApixPress.App.Services.Implementations;

/// <summary>
/// 强调色预设服务（T123）：预设三套主色（青/蓝/紫），每个预设携带完整派生色族。
/// 换色引擎采用「就地修改 Colors.axaml 合并字典中画刷实例的 Color」——StaticResource 别名层
/// （Semi/Ursa 桥接，如 SemiColorPrimary → Brush.Brand.Primary）引用同一画刷实例，实例改色可整体传导；
/// 若按顶层键覆盖新实例，别名仍指向旧实例不会跟随。须在 UI 线程调用。
/// </summary>
public sealed class AccentPaletteService : IAccentPaletteService, ISingletonDependency
{
    public const string DefaultTheme = "teal";

    private const string SystemAccentColorKey = "SystemAccentColor";

    private static readonly Dictionary<string, AccentPalette> Palettes = new(StringComparer.OrdinalIgnoreCase)
    {
        // teal = Colors.axaml 既有默认值（design-system brand 族原值）
        ["teal"] = new AccentPalette
        {
            Primary = "#44E7D4",
            Strong = "#22C7B8",
            Hover = "#6BF0E2",
            OnBrand = "#08201E",
            Ring = "#3D44E7D4",
            SubtleBackground = "#8C123B3C",
            SubtleBorder = "#3844E7D4",
            SubtleText = "#81F1E4"
        },
        ["blue"] = new AccentPalette
        {
            Primary = "#4C9AF0",
            Strong = "#2E7FD9",
            Hover = "#6FAFF4",
            OnBrand = "#0A1B2E",
            Ring = "#3D4C9AF0",
            SubtleBackground = "#8C1B3353",
            SubtleBorder = "#384C9AF0",
            SubtleText = "#9EC2F5"
        },
        ["purple"] = new AccentPalette
        {
            Primary = "#A797FF",
            Strong = "#8A7AE8",
            Hover = "#B9ADFF",
            OnBrand = "#17102B",
            Ring = "#3DA797FF",
            SubtleBackground = "#8C2B2450",
            SubtleBorder = "#38A797FF",
            SubtleText = "#CFC6FF"
        }
    };

    public void Apply(string accentTheme)
    {
        var resources = Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        if (!Palettes.TryGetValue(accentTheme, out var palette))
        {
            palette = Palettes[DefaultTheme];
        }

        // 主色族：链接与焦点边框沿用主色（与 Colors.axaml 现状一致的一个 token 双职能）
        SetBrushColor(resources, "Brush.Brand.Primary", palette.Primary);
        SetBrushColor(resources, "Brush.Brand.Primary.Strong", palette.Strong);
        SetBrushColor(resources, "Brush.Brand.Hover", palette.Hover);
        SetBrushColor(resources, "Brush.Text.Link", palette.Primary);
        SetBrushColor(resources, "Brush.Border.Focus", palette.Primary);
        SetBrushColor(resources, "Brush.Focus.Ring", palette.Ring);
        SetBrushColor(resources, "Brush.Text.OnBrand", palette.OnBrand);
        SetBrushColor(resources, "Brush.Brand.Subtle.Background", palette.SubtleBackground);
        SetBrushColor(resources, "Brush.Brand.Subtle.Border", palette.SubtleBorder);
        SetBrushColor(resources, "Brush.Brand.Subtle.Text", palette.SubtleText);

        // 品牌渐变（首页空状态徽章）：两个 stop 对应主色/深主色
        if (resources["Brush.Brand.Gradient"] is LinearGradientBrush gradient && gradient.GradientStops.Count >= 2)
        {
            gradient.GradientStops[0].Color = Color.Parse(palette.Primary);
            gradient.GradientStops[1].Color = Color.Parse(palette.Strong);
        }

        // AvaloniaEdit 兼容层：Color 为结构体装箱，无法就地改，顶层覆盖键值（DynamicResource 消费者可感知）
        resources[SystemAccentColorKey] = Color.Parse(palette.Primary);
    }

    private static void SetBrushColor(IResourceDictionary resources, string key, string colorText)
    {
        if (resources[key] is SolidColorBrush brush)
        {
            brush.Color = Color.Parse(colorText);
        }
    }
}

/// <summary>单个强调色预设的完整派生色族；颜色文字沿用 design-system 的 #AARRGGBB 约定。</summary>
internal sealed record AccentPalette
{
    public required string Primary { get; init; }

    public required string Strong { get; init; }

    public required string Hover { get; init; }

    /// <summary>品牌实心底上的深色文字（各预设同色相极暗色，对比度 ≥4.5:1）。</summary>
    public required string OnBrand { get; init; }

    /// <summary>焦点环半透明外圈（主色 @ 约 24% 透明度）。</summary>
    public required string Ring { get; init; }

    public required string SubtleBackground { get; init; }

    public required string SubtleBorder { get; init; }

    public required string SubtleText { get; init; }
}
