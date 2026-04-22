using System.ComponentModel;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Tests.ViewModels;

public sealed class ViewModelLifecycleTests
{
    [Fact]
    public void CloseWorkspaceTabCommand_ShouldDisposeRemovedWorkspaceTab()
    {
        var viewModel = new ProjectWorkspaceTabsViewModel(() => { }, _ => { });
        var tab = viewModel.CreateWorkspaceTab(activate: true, showInTabStrip: true);
        tab.ConfigureAsQuickRequest();
        tab.ConfigTab.RequestName = "初始请求";
        Assert.Equal("初始请求", tab.HeaderText);

        viewModel.CloseWorkspaceTabCommand.Execute(tab);

        var headerBeforeMutation = tab.HeaderText;
        tab.ConfigTab.RequestName = "释放后请求";

        Assert.DoesNotContain(tab, viewModel.WorkspaceTabs);
        Assert.Equal(headerBeforeMutation, tab.HeaderText);
    }

    [Fact]
    public void ExplorerItemDispose_ShouldStopChildCollectionNotifications()
    {
        var viewModel = new ExplorerItemViewModel
        {
            NodeType = "http-interface"
        };
        var hasChildrenChangedCount = 0;
        viewModel.PropertyChanged += OnPropertyChanged;

        viewModel.Children.Add(new ExplorerItemViewModel());
        Assert.True(hasChildrenChangedCount > 0);
        var countBeforeDispose = hasChildrenChangedCount;

        viewModel.Dispose();
        viewModel.Children.Add(new ExplorerItemViewModel());

        Assert.Equal(countBeforeDispose, hasChildrenChangedCount);
        viewModel.PropertyChanged -= OnPropertyChanged;

        void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ExplorerItemViewModel.HasChildren))
            {
                hasChildrenChangedCount++;
            }
        }
    }
}
