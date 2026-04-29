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
        tab.MarkCleanState();
        Assert.Equal("初始请求", tab.HeaderText);

        viewModel.CloseWorkspaceTabCommand.Execute(tab);

        var headerBeforeMutation = tab.HeaderText;
        tab.ConfigTab.RequestName = "释放后请求";

        Assert.DoesNotContain(tab, viewModel.WorkspaceTabs);
        Assert.Equal(headerBeforeMutation, tab.HeaderText);
    }

    [Fact]
    public void CloseWorkspaceTabCommand_ShouldRequireSecondClose_WhenTabHasUnsavedChanges()
    {
        var viewModel = new ProjectWorkspaceTabsViewModel(() => { }, _ => { });
        var tab = viewModel.CreateWorkspaceTab(activate: true, showInTabStrip: true);
        tab.ConfigureAsQuickRequest();

        tab.RequestUrl = "https://demo.local/orders";
        viewModel.CloseWorkspaceTabCommand.Execute(tab);

        Assert.True(tab.HasUnsavedChanges);
        Assert.True(tab.IsCloseDiscardPending);
        Assert.Contains(tab, viewModel.WorkspaceTabs);

        viewModel.CloseWorkspaceTabCommand.Execute(tab);

        Assert.DoesNotContain(tab, viewModel.WorkspaceTabs);
    }

    [Fact]
    public void PinnedWorkspaceTab_ShouldExposePinStateAndRejectDirectClose()
    {
        var viewModel = new ProjectWorkspaceTabsViewModel(() => { }, _ => { });
        var tab = viewModel.CreateWorkspaceTab(activate: true, showInTabStrip: true);

        Assert.False(tab.IsPinned);
        Assert.True(tab.CanCloseFromTab);
        Assert.Equal("固定标签页", tab.PinMenuHeader);

        tab.TogglePinCommand.Execute(null);
        viewModel.CloseWorkspaceTabCommand.Execute(tab);

        Assert.True(tab.IsPinned);
        Assert.False(tab.CanCloseFromTab);
        Assert.Equal("取消固定标签页", tab.PinMenuHeader);
        Assert.Contains(tab, viewModel.WorkspaceTabs);
    }

    [Fact]
    public void CloseAllWorkspaceTabsCommand_ShouldKeepPinnedTabs()
    {
        var viewModel = new ProjectWorkspaceTabsViewModel(() => { }, _ => { });
        var pinnedTab = viewModel.CreateWorkspaceTab(activate: true, showInTabStrip: true);
        var normalTab = viewModel.CreateWorkspaceTab(activate: true, showInTabStrip: true);
        pinnedTab.TogglePinCommand.Execute(null);

        viewModel.CloseAllWorkspaceTabsCommand.Execute(null);

        Assert.Contains(pinnedTab, viewModel.WorkspaceTabs);
        Assert.DoesNotContain(normalTab, viewModel.WorkspaceTabs);
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
