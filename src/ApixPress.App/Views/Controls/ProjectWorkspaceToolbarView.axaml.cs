using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Views.Controls;

public partial class ProjectWorkspaceToolbarView : UserControl
{
    public ProjectWorkspaceToolbarView()
    {
        InitializeComponent();
    }

    private void OnEnvironmentManagerButtonClick(object? sender, RoutedEventArgs e)
    {
        // 环境管理走右侧抽屉（多环境平铺编辑，对齐原型 07-environments）
        if (this.FindAncestorOfType<MainWindow>() is { } mainWindow)
        {
            mainWindow.OpenEnvironmentDrawer();
        }
    }

    // 环境菜单项点击：业务由 SelectEnvironmentCommand 承担，这里只负责收起浮层
    private void OnEnvironmentMenuItemClick(object? sender, RoutedEventArgs e)
    {
        // Flyout 内容在 PopupRoot 中编译绑定 $parent 链断裂（命令恒 null），这里手动执行切换命令
        if (sender is Button { DataContext: ProjectEnvironmentItemViewModel item }
            && DataContext is ProjectTabViewModel tab
            && tab.EnvironmentPanel.SelectEnvironmentCommand.CanExecute(item))
        {
            tab.EnvironmentPanel.SelectEnvironmentCommand.Execute(item);
        }
        CloseEnvironmentChipFlyout();
    }

    private void OnEnvironmentManagerFromChipClick(object? sender, RoutedEventArgs e)
    {
        CloseEnvironmentChipFlyout();
        OnEnvironmentManagerButtonClick(sender, e);
    }

    private void CloseEnvironmentChipFlyout()
    {
        if (this.FindControl<Button>("EnvironmentChipButton")?.Flyout is Flyout flyout && flyout.IsOpen)
        {
            flyout.Hide();
        }
    }
}
