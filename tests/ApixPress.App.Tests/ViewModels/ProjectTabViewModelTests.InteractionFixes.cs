using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels;
using Azrng.Core.Results;
using FakeRequestExecutionService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeRequestExecutionService;
using FakeRequestHistoryService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeRequestHistoryService;
using FakeRequestCaseService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeRequestCaseService;
using FakeDialogService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeDialogService;

namespace ApixPress.App.Tests.ViewModels;

public sealed partial class ProjectTabViewModelTests
{
    [Fact]
    public async Task SaveExistingQuickRequest_ShouldUpdateWithoutOpeningDialog()
    {
        var cases = new FakeRequestCaseService();
        var dialogs = new FakeDialogService();
        using var project = CreateViewModel(new FakeApiWorkspaceService(), requestCaseService: cases, dialogService: dialogs);
        await project.InitializeAsync();
        var tab = project.Workspace.CreateWorkspaceTab(true);
        tab.ApplySavedRequest(new RequestCaseDto { Id = "quick", EntryType = "quick-request", Name = "探活", RequestSnapshot = new() { Name = "探活", Url = "https://example.test" } });
        tab.RequestUrl = "https://example.test/updated";

        await project.SaveCaseCommand.ExecuteAsync(null);

        var saved = Assert.Single(cases.Cases);
        Assert.Equal("quick", saved.Id);
        Assert.Equal("https://example.test/updated", saved.RequestSnapshot.Url);
        Assert.False(tab.HasUnsavedChanges);
    }

    [Fact]
    public async Task EmptyQuickRequestName_ShouldKeepDialogAndExposeFieldError()
    {
        using var project = CreateViewModel(new FakeApiWorkspaceService());
        await project.InitializeAsync();
        project.Workspace.OpenQuickRequestWorkspaceCommand.Execute(null);
        project.QuickRequestSave.DraftName = " ";
        await project.QuickRequestSave.ConfirmSaveCommand.ExecuteAsync(null);
        Assert.True(project.QuickRequestSave.HasNameError);
        project.QuickRequestSave.DraftName = "探活";
        Assert.False(project.QuickRequestSave.HasNameError);
    }

    [Fact]
    public async Task SaveQuickRequestAs_ShouldCreateNewRecordEvenWithSameName()
    {
        var cases = new FakeRequestCaseService();
        using var project = CreateViewModel(new FakeApiWorkspaceService(), requestCaseService: cases);
        await project.InitializeAsync();
        var original = new RequestCaseDto { Id = "original", ProjectId = "project-1", EntryType = "quick-request", Name = "探活", RequestSnapshot = new() { Name = "探活", Url = "https://example.test" } };
        cases.Cases.Add(original);
        var tab = project.Workspace.CreateWorkspaceTab(true);
        tab.ApplySavedRequest(original);

        project.Workflow.SaveQuickRequestAsCommand.Execute(null);
        await project.QuickRequestSave.ConfirmSaveCommand.ExecuteAsync(null);

        Assert.Equal(2, cases.Cases.Count);
        Assert.NotEqual(original.Id, tab.EditingQuickRequestId);
        Assert.Contains(original, cases.Cases);
    }

    [Fact]
    public async Task InvalidQuickRequestUrl_ShouldExposeAndClearInlineError()
    {
        using var project = CreateViewModel(new FakeApiWorkspaceService());
        await project.InitializeAsync();
        project.Workspace.OpenQuickRequestWorkspaceCommand.Execute(null);
        var tab = project.ActiveWorkspaceTab!;
        tab.RequestUrl = "/relative";
        await project.SendRequestCommand.ExecuteAsync(null);
        Assert.True(tab.HasUrlValidationError);
        tab.RequestUrl = "https://example.test";
        Assert.False(tab.HasUrlValidationError);
    }

    [Fact]
    public async Task SendingDifferentTabs_ShouldKeepFirstRunningAndCancelOnlyActiveTab()
    {
        var pending = new TaskCompletionSource<IResultModel<ResponseSnapshotDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var execution = new FakeRequestExecutionService { PendingSendResult = pending };
        using var project = CreateViewModel(new FakeApiWorkspaceService(), requestExecutionService: execution);
        await project.InitializeAsync();
        project.Workspace.OpenQuickRequestWorkspaceCommand.Execute(null);
        var first = project.ActiveWorkspaceTab!;
        first.RequestUrl = "https://example.test/first";
        var firstSend = project.SendRequestCommand.ExecuteAsync(null);
        var second = project.Workspace.CreateWorkspaceTab(true);
        second.ConfigureAsQuickRequest();
        second.RequestUrl = "https://example.test/second";
        var secondSend = project.SendRequestCommand.ExecuteAsync(null);
        Assert.Equal(2, execution.SendCallCount);
        Assert.True(first.ResponseSection.IsLoading);
        Assert.True(second.ResponseSection.IsLoading);
        project.CancelRequestCommand.Execute(null);
        await secondSend;
        Assert.True(first.ResponseSection.IsLoading);
        Assert.True(project.IsBusy);
        pending.SetResult(ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto { StatusCode = 200 }));
        await firstSend;
        Assert.False(project.IsBusy);
    }

    [Fact]
    public async Task SendingSameTabTwice_ShouldSendOnlyOnce()
    {
        var pending = new TaskCompletionSource<IResultModel<ResponseSnapshotDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var execution = new FakeRequestExecutionService { PendingSendResult = pending };
        using var project = CreateViewModel(new FakeApiWorkspaceService(), requestExecutionService: execution);
        await project.InitializeAsync();
        project.Workspace.OpenQuickRequestWorkspaceCommand.Execute(null);
        project.ActiveWorkspaceTab!.RequestUrl = "https://example.test";
        var sending = project.SendRequestCommand.ExecuteAsync(null);
        await project.SendRequestCommand.ExecuteAsync(null);
        Assert.Equal(1, execution.SendCallCount);
        pending.SetResult(ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto { StatusCode = 200 }));
        await sending;
    }

    [Fact]
    public async Task FailedNetworkRequest_ShouldKeepRequestAndErrorInHistory()
    {
        var pending = new TaskCompletionSource<IResultModel<ResponseSnapshotDto>>();
        pending.SetResult(ResultModel<ResponseSnapshotDto>.Failure("连接失败", "request_http_failed"));
        var history = new FakeRequestHistoryService();
        using var project = CreateViewModel(new FakeApiWorkspaceService(), requestExecutionService: new FakeRequestExecutionService { PendingSendResult = pending }, requestHistoryService: history);
        await project.InitializeAsync();
        project.Workspace.OpenQuickRequestWorkspaceCommand.Execute(null);
        project.ActiveWorkspaceTab!.RequestUrl = "https://example.test/failure";
        await project.SendRequestCommand.ExecuteAsync(null);
        var item = Assert.Single(history.Items);
        Assert.Equal("https://example.test/failure", item.RequestSnapshot.Url);
        Assert.Equal("连接失败", item.ResponseSnapshot!.ErrorMessage);
        var response = new ResponseSectionViewModel();
        response.ApplyResult(ResultModel<ResponseSnapshotDto>.Success(item.ResponseSnapshot), item.RequestSnapshot);
        Assert.Equal("请求失败", response.ResponseNoticeTitle);
        Assert.Contains("连接失败", response.BodyText);
    }
}
