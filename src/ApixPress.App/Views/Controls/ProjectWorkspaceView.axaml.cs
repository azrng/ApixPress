using Avalonia.Controls;
using ApixPress.App.ViewModels;
using Ursa.Common;
using Ursa.Controls;
using Ursa.Controls.Options;

namespace ApixPress.App.Views.Controls;

public partial class ProjectWorkspaceView : UserControl
{
    private bool _isProjectDrawerOpen;

    public ProjectWorkspaceView()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private void OnOpenProjectDrawer(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isProjectDrawerOpen || ViewModel is null)
        {
            return;
        }

        var activeProjectId = ViewModel.ActiveProjectTab?.ProjectId;
        if (!string.IsNullOrWhiteSpace(activeProjectId))
        {
            ViewModel.ProjectPanel.SelectedProject = ViewModel.ProjectPanel.Projects.FirstOrDefault(item =>
                string.Equals(item.Id, activeProjectId, StringComparison.OrdinalIgnoreCase));
        }

        _ = OpenProjectDrawerAsync(ViewModel);
    }

    private async Task OpenProjectDrawerAsync(MainWindowViewModel viewModel)
    {
        _isProjectDrawerOpen = true;
        try
        {
            var topLevelHashCode = TopLevel.GetTopLevel(this)?.GetHashCode() ?? GetHashCode();
            await Drawer.ShowModal(
                new ProjectDrawerView(),
                viewModel,
                null,
                new DrawerOptions
                {
                    Buttons = DialogButton.None,
                    Title = "项目管理",
                    Position = Position.Right,
                    MinWidth = 920,
                    MaxWidth = 1024,
                    CanResize = true,
                    TopLevelHashCode = topLevelHashCode
                });
        }
        finally
        {
            _isProjectDrawerOpen = false;
        }
    }
}
