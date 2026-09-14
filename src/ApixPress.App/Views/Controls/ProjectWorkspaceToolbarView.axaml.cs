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
        if (this.FindAncestorOfType<Window>()?.DataContext is MainWindowViewModel viewModel
            && viewModel.OpenEnvironmentManagerCommand.CanExecute(null))
        {
            viewModel.OpenEnvironmentManagerCommand.Execute(null);
        }
    }

    // 环境菜单项点击：业务由 SelectEnvironmentCommand 承担，这里只负责收起浮层
    private void OnEnvironmentMenuItemClick(object? sender, RoutedEventArgs e)
    {
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
