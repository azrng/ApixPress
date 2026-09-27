using CommunityToolkit.Mvvm.Messaging;
using ApixPress.App.Messages;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Tests.ViewModels;

/// <summary>收集 StatusMessageRequest 的测试记录器：验证保存结果经状态栏通道可见。</summary>
internal sealed class StatusMessageRecorder : IRecipient<StatusMessageRequest>
{
    public List<string> Messages { get; } = [];

    public void Receive(StatusMessageRequest message) => Messages.Add(message.Message);
}

public sealed class SaveFailureStatusMessageTests
{
    private static (WeakReferenceMessenger Messenger, StatusMessageRecorder Recorder) CreateStatusChannel()
    {
        var messenger = new WeakReferenceMessenger();
        var recorder = new StatusMessageRecorder();
        messenger.Register<StatusMessageRequest>(recorder);
        return (messenger, recorder);
    }

    [Fact]
    public async Task EnvironmentPanel_SaveEnvironment_ShouldReportFailureWithReason()
    {
        var service = new ViewModelSharedTestDoubles.FakeEnvironmentVariableService
        {
            ShouldFailEnvironmentSave = true,
            EnvironmentSaveFailureMessage = "数据库不可写"
        };
        var (messenger, recorder) = CreateStatusChannel();
        var viewModel = new EnvironmentPanelViewModel(service, messenger);
        await viewModel.LoadProjectAsync("proj-1");

        await viewModel.SaveEnvironmentCommand.ExecuteAsync(viewModel.SelectedEnvironment);

        var message = Assert.Single(recorder.Messages);
        Assert.Contains("环境「开发」保存失败", message);
        Assert.Contains("数据库不可写", message);
    }

    [Fact]
    public async Task EnvironmentPanel_SaveEnvironment_ShouldReportSuccess()
    {
        var service = new ViewModelSharedTestDoubles.FakeEnvironmentVariableService();
        var (messenger, recorder) = CreateStatusChannel();
        var viewModel = new EnvironmentPanelViewModel(service, messenger);
        await viewModel.LoadProjectAsync("proj-1");

        await viewModel.SaveEnvironmentCommand.ExecuteAsync(viewModel.SelectedEnvironment);

        var message = Assert.Single(recorder.Messages);
        Assert.Contains("环境「开发」已保存", message);
    }

    [Fact]
    public async Task EnvironmentPanel_SaveAllEnvironments_ShouldSummarizeTotalFailure()
    {
        var service = new ViewModelSharedTestDoubles.FakeEnvironmentVariableService
        {
            ShouldFailEnvironmentSave = true
        };
        var (messenger, recorder) = CreateStatusChannel();
        var viewModel = new EnvironmentPanelViewModel(service, messenger);
        await viewModel.LoadProjectAsync("proj-1");

        await viewModel.SaveAllEnvironmentsCommand.ExecuteAsync(null);

        // 单环境项目全败：应出现单环境失败原因 + 汇总两条，而不是静默返回
        Assert.Contains(recorder.Messages, message => message.Contains("环境「开发」保存失败"));
        Assert.Contains(recorder.Messages, message => message.Contains("环境保存全部失败"));
    }

    [Fact]
    public async Task UseCasesPanel_SaveCase_ShouldReportFailureWithReason()
    {
        var service = new ViewModelSharedTestDoubles.FakeRequestCaseService
        {
            ShouldFailSave = true,
            SaveFailureMessage = "约束冲突"
        };
        var (messenger, recorder) = CreateStatusChannel();
        var viewModel = new UseCasesPanelViewModel(service, messenger: messenger);
        viewModel.SetProjectContext("proj-1");

        await viewModel.SaveCaseAsync(new RequestSnapshotDto
        {
            Name = "冒烟用例",
            Method = "GET",
            Url = "http://localhost:5180/echo"
        });

        var message = Assert.Single(recorder.Messages);
        Assert.Contains("保存用例「冒烟用例」失败", message);
        Assert.Contains("约束冲突", message);
    }
}
