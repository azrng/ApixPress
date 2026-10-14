using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels;
using FakeRequestCaseService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeRequestCaseService;

namespace ApixPress.App.Tests.ViewModels;

public sealed partial class ProjectTabViewModelTests
{
    [Fact]
    public async Task LoadWorkspaceItem_ShouldSharePendingDetailForRepeatedClicks()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeRequestCaseService { DetailLoadGate = gate };
        service.Cases.Add(CreatePerformanceRequest("first", loaded: true));
        using var viewModel = CreateViewModel(new FakeApiWorkspaceService(), service);
        var item = new ExplorerItemViewModel { SourceCase = CreatePerformanceRequest("first", loaded: false) };
        try
        {
            await viewModel.Catalog.LoadWorkspaceItem(item);
            await WaitUntilAsync(() => service.GetDetailCallCount == 1);
            await viewModel.Catalog.LoadWorkspaceItem(item);
            await Task.Delay(30);
            Assert.Equal(1, service.GetDetailCallCount);
            gate.SetResult(true);
            await WaitUntilAsync(() => viewModel.ConfigTab.RequestBody == "first-body");
        }
        finally
        {
            gate.TrySetResult(true);
        }
    }

    [Fact]
    public async Task LoadWorkspaceItem_ShouldNotOverwriteEditsWhenDetailArrives()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeRequestCaseService { DetailLoadGate = gate };
        service.Cases.Add(CreatePerformanceRequest("first", loaded: true));
        using var viewModel = CreateViewModel(new FakeApiWorkspaceService(), service);
        var item = new ExplorerItemViewModel { SourceCase = CreatePerformanceRequest("first", loaded: false) };
        await viewModel.Catalog.LoadWorkspaceItem(item);
        await WaitUntilAsync(() => service.GetDetailCallCount == 1);
        viewModel.ConfigTab.RequestBody = "用户刚编辑的内容";
        gate.SetResult(true);
        await WaitUntilAsync(() => item.SourceCase.HasLoadedDetail);
        Assert.Equal("用户刚编辑的内容", viewModel.ConfigTab.RequestBody);
        Assert.True(viewModel.ActiveWorkspaceTab?.HasUnsavedChanges);
    }

    [Fact]
    public async Task LoadWorkspaceItem_ShouldCancelOldDetailWhenSwitchingInterfaces()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeRequestCaseService { DetailLoadGate = gate };
        service.Cases.Add(CreatePerformanceRequest("first", loaded: true));
        using var viewModel = CreateViewModel(new FakeApiWorkspaceService(), service);
        var first = new ExplorerItemViewModel { SourceCase = CreatePerformanceRequest("first", loaded: false) };
        await viewModel.Catalog.LoadWorkspaceItem(first);
        await WaitUntilAsync(() => service.GetDetailCallCount == 1);
        var oldToken = service.LastDetailCancellationToken;

        await viewModel.Catalog.LoadWorkspaceItem(new ExplorerItemViewModel
        {
            SourceCase = CreatePerformanceRequest("second", loaded: true)
        });
        Assert.True(oldToken.IsCancellationRequested);
        gate.SetResult(true);
        Assert.Equal("second-body", viewModel.ConfigTab.RequestBody);
        Assert.False(first.SourceCase.HasLoadedDetail);
    }

    [Fact]
    public async Task DisposingWorkspace_ShouldCancelPendingDetail()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeRequestCaseService { DetailLoadGate = gate };
        using var viewModel = CreateViewModel(new FakeApiWorkspaceService(), service);
        await viewModel.Catalog.LoadWorkspaceItem(new ExplorerItemViewModel
        {
            SourceCase = CreatePerformanceRequest("first", loaded: false)
        });
        await WaitUntilAsync(() => service.GetDetailCallCount == 1);
        var token = service.LastDetailCancellationToken;
        viewModel.Dispose();

        Assert.True(token.IsCancellationRequested);
        gate.SetResult(true);
    }

    [Fact]
    public async Task Editor_ShouldBuildDocumentSnippetOnlyWhenPreviewOpens()
    {
        using var viewModel = CreateViewModel(new FakeApiWorkspaceService());
        await viewModel.Catalog.LoadWorkspaceItem(new ExplorerItemViewModel
        {
            SourceCase = CreatePerformanceRequest("first", loaded: true)
        });

        Assert.Empty(viewModel.Editor.CurrentHttpDocumentCurlSnippet);
        viewModel.Editor.ShowHttpDocumentPreviewModeCommand.Execute(null);
        Assert.Contains("first-body", viewModel.Editor.CurrentHttpDocumentCurlSnippet);
    }

    [Fact]
    public async Task Editor_ShouldPublishOnlyCompleteSnapshotPreviewDuringNavigation()
    {
        using var viewModel = CreateViewModel(new FakeApiWorkspaceService());
        await viewModel.Catalog.LoadWorkspaceItem(new ExplorerItemViewModel
        {
            SourceCase = CreatePerformanceRequest("first", loaded: true)
        });
        viewModel.Editor.ShowHttpDocumentPreviewModeCommand.Execute(null);
        var snippets = new List<string>();
        viewModel.Editor.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ProjectRequestEditorViewModel.CurrentHttpDocumentCurlSnippet))
            {
                snippets.Add(viewModel.Editor.CurrentHttpDocumentCurlSnippet);
            }
        };

        await viewModel.Catalog.LoadWorkspaceItem(new ExplorerItemViewModel
        {
            SourceCase = CreatePerformanceRequest("second", loaded: true)
        });
        await WaitUntilAsync(() => viewModel.Editor.CurrentHttpDocumentCurlSnippet.Contains("second-body"));

        Assert.Single(snippets);
        Assert.Contains("second-header", snippets[0]);
        Assert.DoesNotContain("first-body", snippets[0]);
    }

    private static RequestCaseDto CreatePerformanceRequest(string id, bool loaded)
    {
        return new RequestCaseDto
        {
            Id = id,
            ProjectId = "project-1",
            EntryType = ProjectTabRequestEntryTypes.HttpInterface,
            Name = id,
            HasLoadedDetail = loaded,
            RequestSnapshot = new RequestSnapshotDto
            {
                Name = id,
                Method = "POST",
                Url = $"/{id}",
                BodyMode = loaded ? BodyModes.RawText : BodyModes.None,
                BodyContent = loaded ? $"{id}-body" : string.Empty,
                Headers = loaded ? [new RequestKeyValueDto { Name = "X-Test", Value = $"{id}-header" }] : []
            }
        };
    }
}
