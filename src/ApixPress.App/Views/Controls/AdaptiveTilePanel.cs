using Avalonia;
using Avalonia.Controls;

namespace ApixPress.App.Views.Controls;

/// <summary>
/// 项目首页卡片网格：按容器宽度自适应列数，行内卡片均分填满整行宽，
/// 避免 WrapPanel 固定列宽在宽屏下右缘留白。等价 CSS repeat(auto-fill, minmax(n, 1fr))。
/// </summary>
public sealed class AdaptiveTilePanel : Panel
{
    /// <summary>卡片最小宽度：列数由 容器宽/最小宽 决定</summary>
    public static readonly StyledProperty<double> MinItemWidthProperty =
        AvaloniaProperty.Register<AdaptiveTilePanel, double>(nameof(MinItemWidth), 300);

    /// <summary>行高（卡片高度固定）</summary>
    public static readonly StyledProperty<double> ItemHeightProperty =
        AvaloniaProperty.Register<AdaptiveTilePanel, double>(nameof(ItemHeight), 224);

    /// <summary>列/行间距</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<AdaptiveTilePanel, double>(nameof(Gap), 10);

    /// <summary>列数上限：超宽屏不无限拉伸单卡</summary>
    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<AdaptiveTilePanel, int>(nameof(MaxColumns), 6);

    public double MinItemWidth
    {
        get => GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public int MaxColumns
    {
        get => GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }

    private int ResolveColumns(double availableWidth)
    {
        var minItem = Math.Max(1, MinItemWidth);
        var gap = Gap;
        var fitCount = (int)((availableWidth + gap) / (minItem + gap));
        return Math.Clamp(fitCount, 1, Math.Max(1, MaxColumns));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = ResolveColumns(availableSize.Width);
        var itemWidth = (availableSize.Width - (columns - 1) * Gap) / columns;
        var childSize = new Size(itemWidth, ItemHeight);
        foreach (var child in Children)
        {
            child.Measure(childSize);
        }

        var rows = (int)Math.Ceiling(Children.Count / (double)columns);
        var height = rows * ItemHeight + Math.Max(0, rows - 1) * Gap;
        return new Size(availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = ResolveColumns(finalSize.Width);
        var itemWidth = (finalSize.Width - (columns - 1) * Gap) / columns;
        for (var index = 0; index < Children.Count; index++)
        {
            var row = index / columns;
            var column = index % columns;
            var rect = new Rect(
                column * (itemWidth + Gap),
                row * (ItemHeight + Gap),
                itemWidth,
                ItemHeight);
            Children[index].Arrange(rect);
        }

        return finalSize;
    }
}
