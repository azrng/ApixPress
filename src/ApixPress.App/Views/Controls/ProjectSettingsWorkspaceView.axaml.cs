using Avalonia.Controls;
using ApixPress.App.ViewModels;
using Ursa.Common;
using Ursa.Controls;
using Ursa.Controls.Options;

namespace ApixPress.App.Views.Controls;

public partial class ProjectSettingsWorkspaceView : UserControl
{
    private bool _isProjectDrawerOpen;

    public ProjectSettingsWorkspaceView()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? HostViewModel =>
        TopLevel.GetTopLevel(this) is Window window ? window.DataContext as MainWindowViewModel : null;

    private void OnOpenProjectDrawer(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isProjectDrawerOpen || HostViewModel is null)
        {
            return;
        }

        var activeProjectId = HostViewModel.ActiveProjectTab?.ProjectId;
        if (!string.IsNullOrWhiteSpace(activeProjectId))
        {
            HostViewModel.ProjectPanel.SelectedProject = HostViewModel.ProjectPanel.Projects.FirstOrDefault(item =>
                string.Equals(item.Id, activeProjectId, StringComparison.OrdinalIgnoreCase));
        }

        _ = OpenProjectDrawerAsync(HostViewModel);
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
