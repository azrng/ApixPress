using Avalonia.Controls;
using Avalonia.Interactivity;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Views.Controls;

public partial class ProjectWorkspaceToolbarView : UserControl
{
    public ProjectWorkspaceToolbarView()
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
}
