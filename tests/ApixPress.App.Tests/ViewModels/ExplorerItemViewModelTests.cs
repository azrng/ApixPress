using ApixPress.App.ViewModels;

namespace ApixPress.App.Tests.ViewModels;

public sealed class ExplorerItemViewModelTests
{
    [Fact]
    public void EnsureChildrenLoaded_ShouldRaiseSingleBatchedCollectionChange()
    {
        // 大组（59+ 子节点）延迟展开是树交互热点：ReplaceChildren 必须合并为一次 Reset，
        // 否则每个 Add 触发 1 次集合事件 + 7 个派生属性通知，通知成本随子节点数线性放大
        var children = Enumerable.Range(0, 20)
            .Select(index => new ExplorerItemViewModel { NodeKey = $"child-{index}" })
            .ToList();
        var parent = new ExplorerItemViewModel { NodeKey = "parent" };
        parent.SetDeferredChildren(() => children);

        var collectionChangeCount = 0;
        parent.Children.CollectionChanged += (_, _) => collectionChangeCount++;

        parent.IsExpanded = true;

        Assert.Equal(20, parent.Children.Count);
        Assert.Equal(1, collectionChangeCount);
        Assert.True(parent.AreChildrenLoaded);
    }

    [Fact]
    public void SyncFrom_WithoutMeaningfulChange_ShouldSkipDerivedNotifications()
    {
        var source = new ExplorerItemViewModel
        {
            NodeKey = "node-1",
            Title = "接口 A",
            NodeType = "http-interface",
            CanLoad = true
        };
        var target = new ExplorerItemViewModel
        {
            NodeKey = "node-1",
            Title = "接口 A",
            NodeType = "http-interface",
            CanLoad = true
        };

        var notifications = new List<string?>();
        target.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        target.SyncFrom(source);

        // 数据未变时不允许派生通知风暴：这是树重建时全体行重估绑定的卡顿放大器
        Assert.Empty(notifications);
    }
}
