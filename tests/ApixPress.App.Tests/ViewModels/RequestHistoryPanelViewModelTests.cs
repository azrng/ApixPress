using System.Collections.Specialized;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Tests.ViewModels;

public sealed class RequestHistoryPanelViewModelTests
{
    [Fact]
    public async Task LoadHistoryAsync_Should_BuildDateGroupedDisplayItems()
    {
        var (viewModel, _) = CreatePanel();
        viewModel.SetProjectContext("proj-1");
        await viewModel.LoadHistoryAsync();

        // 今天 2 条 + 昨天 2 条：分组头与记录行交替
        Assert.Equal(6, viewModel.HistoryDisplayItems.Count);
        Assert.Equal("今天", GroupTitle(viewModel.HistoryDisplayItems[0]));
        Assert.Equal(2, GroupCount(viewModel.HistoryDisplayItems[0]));
        Assert.Equal("昨天", GroupTitle(viewModel.HistoryDisplayItems[3]));
        Assert.Equal(2, GroupCount(viewModel.HistoryDisplayItems[3]));
    }

    [Fact]
    public async Task PrependHistoryItem_WithoutSearch_ShouldApplyDisplayDifferentially()
    {
        var (viewModel, _) = CreatePanel();
        viewModel.SetProjectContext("proj-1");
        await viewModel.LoadHistoryAsync();

        var actions = new List<NotifyCollectionChangedAction>();
        viewModel.HistoryDisplayItems.CollectionChanged += (_, e) => actions.Add(e.Action);

        viewModel.PrependHistoryItem(CreateDto("new-1", DateTime.UtcNow));

        // 期望：分组头计数 Replace 1 次 + 新记录行 Add 1 次，禁止整表 Reset 重建
        Assert.Equal([NotifyCollectionChangedAction.Replace, NotifyCollectionChangedAction.Add], actions);
        Assert.Equal("今天", GroupTitle(viewModel.HistoryDisplayItems[0]));
        Assert.Equal(3, GroupCount(viewModel.HistoryDisplayItems[0]));
        Assert.Same(viewModel.HistoryItems[0], viewModel.HistoryDisplayItems[1]);
        Assert.Same(viewModel.HistoryItems[0], viewModel.VisibleHistoryItems[0]);
        Assert.Equal(7, viewModel.HistoryDisplayItems.Count);
    }

    [Fact]
    public async Task PrependHistoryItem_NewDate_ShouldInsertNewGroupAtFrontWithoutReset()
    {
        var (viewModel, service) = CreatePanel(yesterdayOnly: true);
        viewModel.SetProjectContext("proj-1");
        await viewModel.LoadHistoryAsync();

        var actions = new List<NotifyCollectionChangedAction>();
        viewModel.HistoryDisplayItems.CollectionChanged += (_, e) => actions.Add(e.Action);

        viewModel.PrependHistoryItem(CreateDto("new-today", DateTime.UtcNow));

        // 新日期在最前：插入新分组头 + 新记录行，原有昨天分组保持不动
        Assert.Equal([NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Add], actions);
        Assert.Equal("今天", GroupTitle(viewModel.HistoryDisplayItems[0]));
        Assert.Equal(1, GroupCount(viewModel.HistoryDisplayItems[0]));
        Assert.Equal("昨天", GroupTitle(viewModel.HistoryDisplayItems[2]));
    }

    [Fact]
    public async Task PrependHistoryItem_BeyondLimit_ShouldTrimTailRowAndShrinkGroupHeader()
    {
        var service = new ViewModelSharedTestDoubles.FakeRequestHistoryService();
        for (var index = 0; index < 50; index++)
        {
            service.Items.Add(CreateDto($"hist-{index}", DateTime.UtcNow.AddDays(-2)));
        }

        var viewModel = new RequestHistoryPanelViewModel(service);
        viewModel.SetProjectContext("proj-1");
        await viewModel.LoadHistoryAsync();
        Assert.Equal(50, viewModel.HistoryItems.Count);

        viewModel.PrependHistoryItem(CreateDto("new-1", DateTime.UtcNow));

        Assert.Equal(50, viewModel.HistoryItems.Count);
        Assert.Equal(50, viewModel.VisibleHistoryItems.Count);
        // 展示行 = 今天头(1) + 今天 1 条 + 更早日期头(1) + 更早日期 49 条 = 52
        Assert.Equal(52, viewModel.HistoryDisplayItems.Count);
        Assert.Equal(49, GroupCount(viewModel.HistoryDisplayItems[2]));
    }

    [Fact]
    public async Task DeleteHistoryItem_WithoutSearch_ShouldRemoveRowAndDecrementGroupCount()
    {
        var (viewModel, _) = CreatePanel();
        viewModel.SetProjectContext("proj-1");
        await viewModel.LoadHistoryAsync();

        var target = viewModel.HistoryItems[1];
        var actions = new List<NotifyCollectionChangedAction>();
        viewModel.HistoryDisplayItems.CollectionChanged += (_, e) => actions.Add(e.Action);

        await viewModel.DeleteHistoryItemAsync(target);

        // 分组头计数 Replace + 记录行 Remove，无 Reset
        Assert.Equal([NotifyCollectionChangedAction.Replace, NotifyCollectionChangedAction.Remove], actions);
        Assert.Equal(5, viewModel.HistoryDisplayItems.Count);
        Assert.Equal(1, GroupCount(viewModel.HistoryDisplayItems[0]));
        Assert.DoesNotContain(target, viewModel.HistoryDisplayItems.OfType<RequestHistoryItemViewModel>());
    }

    [Fact]
    public void HistoryItem_ApplyDetail_ShouldReplaceSummaryRequestSnapshot()
    {
        var item = new RequestHistoryItemViewModel
        {
            Id = "history-1",
            Method = "POST",
            Url = "/orders",
            Timestamp = DateTime.UtcNow,
            RequestSnapshot = new RequestSnapshotDto { Method = "POST", Url = "/orders" }
        };

        item.ApplyDetail(new RequestHistoryItemDto
        {
            Id = item.Id,
            Timestamp = item.Timestamp,
            RequestSnapshot = new RequestSnapshotDto
            {
                Method = "POST",
                Url = "/orders",
                BodyMode = BodyModes.RawJson,
                BodyContent = "{\"amount\":1}",
                Headers = [new RequestKeyValueDto { Name = "Content-Type", Value = "application/json" }]
            }
        });

        Assert.True(item.HasRequestBody);
        Assert.True(item.HasRequestHeaders);
        Assert.Equal("{\"amount\":1}", item.RequestSnapshot.BodyContent);
    }

    private static (RequestHistoryPanelViewModel Panel, ViewModelSharedTestDoubles.FakeRequestHistoryService Service) CreatePanel(bool yesterdayOnly = false)
    {
        var service = new ViewModelSharedTestDoubles.FakeRequestHistoryService();
        if (yesterdayOnly)
        {
            service.Items.Add(CreateDto("y-1", DateTime.UtcNow.AddDays(-1)));
            service.Items.Add(CreateDto("y-2", DateTime.UtcNow.AddDays(-1)));
        }
        else
        {
            service.Items.Add(CreateDto("t-1", DateTime.UtcNow));
            service.Items.Add(CreateDto("t-2", DateTime.UtcNow));
            service.Items.Add(CreateDto("y-1", DateTime.UtcNow.AddDays(-1)));
            service.Items.Add(CreateDto("y-2", DateTime.UtcNow.AddDays(-1)));
        }

        return (new RequestHistoryPanelViewModel(service), service);
    }

    private static RequestHistoryItemDto CreateDto(string id, DateTime timestamp)
    {
        return new RequestHistoryItemDto
        {
            Id = id,
            Timestamp = timestamp,
            HasResponse = true,
            StatusCode = 200,
            DurationMs = 12,
            SizeBytes = 256,
            RequestSnapshot = new RequestSnapshotDto { Method = "GET", Url = $"https://api.test/{id}" }
        };
    }

    private static string GroupTitle(object displayItem)
    {
        return Assert.IsType<HistoryDateGroupHeaderViewModel>(displayItem).Title;
    }

    private static int GroupCount(object displayItem)
    {
        return Assert.IsType<HistoryDateGroupHeaderViewModel>(displayItem).Count;
    }

    [Fact]
    public async Task LoadHistory_WhenServiceThrows_ShouldShowErrorStateInsteadOfEmptyText()
    {
        var service = new ViewModelSharedTestDoubles.FakeRequestHistoryService
        {
            ShouldThrowHistoryError = true
        };
        var viewModel = new RequestHistoryPanelViewModel(service);
        viewModel.SetProjectContext("proj-1");

        await viewModel.LoadHistoryAsync();

        // 加载失败必须与“还没有发送记录”空态区分：错误态开启且文案带原因
        Assert.True(viewModel.HasHistoryLoadError);
        Assert.Contains("请求历史加载失败", viewModel.HistoryLoadErrorText);
        Assert.Contains("数据库暂时不可用", viewModel.HistoryLoadErrorText);
        Assert.False(viewModel.HasHistory);
        // 空态文案随之隐藏，错误块独占列表区
        Assert.False(viewModel.ShowHistoryEmptyText);
    }

    [Fact]
    public async Task LoadHistoryCommand_AfterFailure_ShouldRetryAndClearErrorState()
    {
        var service = new ViewModelSharedTestDoubles.FakeRequestHistoryService
        {
            ShouldThrowHistoryError = true
        };
        service.Items.Add(CreateDto("hist-1", DateTime.UtcNow));
        var viewModel = new RequestHistoryPanelViewModel(service);
        viewModel.SetProjectContext("proj-1");
        await viewModel.LoadHistoryAsync();
        Assert.True(viewModel.HasHistoryLoadError);

        // 重试入口存在且恢复后错误态清除、列表正常展示
        service.ShouldThrowHistoryError = false;
        await viewModel.LoadHistoryCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasHistoryLoadError);
        Assert.Equal(string.Empty, viewModel.HistoryLoadErrorText);
        Assert.Single(viewModel.HistoryItems);
        Assert.True(viewModel.HasHistory);
    }
}
