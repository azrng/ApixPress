using Avalonia.Controls;
using Avalonia.Interactivity;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Views.Controls;

public partial class ProjectInnerTabBarView : UserControl
{
    public ProjectInnerTabBarView()
    {
        InitializeComponent();
    }

    private void OnWorkspaceTabMenuButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button
            || button.ContextMenu is not ContextMenu contextMenu)
        {
            return;
        }

        if (contextMenu.IsOpen)
        {
            contextMenu.Close();
            return;
        }

        contextMenu.Open(button);
    }

    private void OnWorkspaceTabMoreContextMenuOpened(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProjectTabViewModel viewModel)
        {
            viewModel.IsWorkspaceTabMenuOpen = true;
        }
    }

    private void OnWorkspaceTabMoreContextMenuClosed(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProjectTabViewModel viewModel)
        {
            viewModel.IsWorkspaceTabMenuOpen = false;
        }
    }

    // Flyout 内编译绑定跨层查找会静默断链（$parent[Window] 取不到命令），激活标签走 code-behind
    private void OnWorkspaceTabListItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: RequestWorkspaceTabViewModel tab }
            || DataContext is not ProjectTabViewModel viewModel)
        {
            return;
        }

        viewModel.Workspace.ActivateWorkspaceTab(tab);
        WorkspaceTabListButton.Flyout?.Hide();
    }
}
