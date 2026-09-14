using ApixPress.App.ViewModels;
using ApixPress.App.Views.Controls;
using Avalonia.Controls;
using Xunit;

namespace ApixPress.App.Tests.ViewModels;

public sealed partial class ProjectTabViewModelTests
{
    [Fact]
    public void ContentHost_ShouldSurviveRepeatedContentModeSwitches()
    {
        // 回归：内容宿主按某一内容模式构建过一次后，再次切换内容模式时，
        // 缓存复用的控件仍挂在上一次的中间 contentColumn 上，未解除旧父级会抛
        // InvalidOperationException（already has a visual parent）导致程序闪退
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());
        var host = new ProjectWorkspaceContentHostView { DataContext = viewModel };

        Assert.Equal(2, host.HostGrid.Children.Count);
        viewModel.Shell.SelectRequestHistorySection();
        Assert.Equal(ProjectWorkspaceContentMode.RequestHistory, viewModel.Shell.CurrentContentMode);
        Assert.Equal(2, host.HostGrid.Children.Count);

        viewModel.Shell.SelectInterfaceManagementSection();
        viewModel.Shell.SelectRequestHistorySection();
        viewModel.Shell.SelectInterfaceManagementSection();
        viewModel.Shell.ShowProjectSettingsSection();
        Assert.Equal(1, host.HostGrid.Children.Count);
        viewModel.Shell.SelectInterfaceManagementSection();

        Assert.Equal(2, host.HostGrid.Children.Count);
        var contentColumn = Assert.IsType<Grid>(host.HostGrid.Children[1]);
        Assert.Single(contentColumn.Children.OfType<ProjectInnerTabBarView>());
        Assert.Single(contentColumn.Children.OfType<WorkspaceLandingView>());
    }
}
