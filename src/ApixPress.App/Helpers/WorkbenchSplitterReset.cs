using Avalonia.Controls;

namespace ApixPress.App.Helpers;

/// <summary>
/// 工作台分栏双击重置（T123）：GridSplitter 拖拽会把配置行从 Auto 固化为像素高度，
/// 双击 splitter 恢复配置行自适应（Auto + 既有 Min/MaxHeight 约束），响应行继续占满剩余空间。
/// </summary>
public static class WorkbenchSplitterReset
{
    public static void Attach(GridSplitter splitter, int configRowIndex)
    {
        splitter.DoubleTapped += (_, _) =>
        {
            if (splitter.Parent is Grid grid
                && configRowIndex >= 0
                && configRowIndex < grid.RowDefinitions.Count)
            {
                grid.RowDefinitions[configRowIndex].Height = GridLength.Auto;
            }
        };
    }
}
