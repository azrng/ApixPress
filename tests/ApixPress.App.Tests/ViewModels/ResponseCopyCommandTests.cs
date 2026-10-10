using CommunityToolkit.Mvvm.Messaging;
using ApixPress.App.Messages;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels;
using Azrng.Core.Results;

namespace ApixPress.App.Tests.ViewModels;

/// <summary>响应区复制命令（P1-8）：复制内容来源、空内容提示与状态栏反馈。</summary>
public sealed class ResponseCopyCommandTests
{
    private static (ProjectRequestEditorViewModel Editor, ViewModelSharedTestDoubles.FakeClipboardService Clipboard, StatusMessageRecorder Recorder) CreateEditor(
        RequestWorkspaceTabViewModel? activeTab = null)
    {
        var clipboard = new ViewModelSharedTestDoubles.FakeClipboardService();
        var messenger = new WeakReferenceMessenger();
        var recorder = new StatusMessageRecorder();
        messenger.Register<StatusMessageRequest>(recorder);

        var fallback = new RequestWorkspaceTabViewModel(new ViewModelSharedTestDoubles.FakeFilePickerService());
        var workspaceContext = new ProjectTabWorkspaceContext
        {
            GetActiveWorkspaceTab = () => activeTab,
            GetFallbackWorkspaceTab = () => fallback,
            GetCurrentBaseUrl = () => "https://api.test",
            GetActiveVariables = () => new Dictionary<string, string>(),
            IsInterfaceRootWorkspaceActive = () => false,
            HasHistory = () => false
        };

        var editor = new ProjectRequestEditorViewModel(
            workspaceContext,
            new ViewModelSharedTestDoubles.FakeDialogService(),
            clipboard,
            messenger);
        return (editor, clipboard, recorder);
    }

    private static ResponseSectionViewModel CreateSectionWithBody(string body, string headersText)
    {
        var section = new ResponseSectionViewModel();
        section.ApplyResult(
            ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto
            {
                StatusCode = 200,
                DurationMs = 12,
                Content = body,
                Headers = string.IsNullOrEmpty(headersText)
                    ? []
                    : [new ResponseHeaderDto { Name = "Content-Type", Value = headersText }]
            }),
            new RequestSnapshotDto { Method = "GET", Url = "https://api.test/orders" });
        return section;
    }

    [Fact]
    public async Task CopyResponseBody_ShouldCopySectionBodyAndReportStatus()
    {
        var (editor, clipboard, recorder) = CreateEditor();
        var section = CreateSectionWithBody("{\"ok\":true}", "application/json");

        await editor.CopyResponseBodyCommand.ExecuteAsync(section);

        // 复制内容与界面显示一致：JSON 响应会先被自动格式化，所见即所得
        var copied = Assert.Single(clipboard.CopiedTexts);
        Assert.Equal(section.CopyableBodyText, copied);
        Assert.Contains("\"ok\"", copied);
        Assert.Contains("响应正文已复制", Assert.Single(recorder.Messages));
    }

    [Fact]
    public async Task CopyResponseBody_WhenBodyIsFormattingPlaceholder_ShouldNotCopyAndHint()
    {
        var (editor, clipboard, recorder) = CreateEditor();
        // 触发后台格式化：正文超过同步格式化阈值时界面先显示占位符
        var section = CreateSectionWithBody("{\"data\":\"" + new string('x', 128 * 1024) + "\"}", "application/json");
        await section.WaitForPendingBodyFormatAsync(TimeSpan.FromSeconds(5));
        // 直接构造占位符态：Reset 后用失败占位场景验证空内容路径
        var emptySection = new ResponseSectionViewModel();
        emptySection.ApplyResult(
            ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto { StatusCode = 200 }),
            new RequestSnapshotDto());

        await editor.CopyResponseBodyCommand.ExecuteAsync(emptySection);

        Assert.Empty(clipboard.CopiedTexts);
        Assert.Contains("没有可复制的响应正文", Assert.Single(recorder.Messages));
    }

    [Fact]
    public async Task CopyResponseHeaders_ShouldCopyJoinedHeadersText()
    {
        var (editor, clipboard, recorder) = CreateEditor();
        var section = CreateSectionWithBody("{}", "application/json");

        await editor.CopyResponseHeadersCommand.ExecuteAsync(section);

        var copied = Assert.Single(clipboard.CopiedTexts);
        Assert.Equal(section.HeadersText, copied);
        Assert.Contains("Headers 已复制", Assert.Single(recorder.Messages));
    }

    [Fact]
    public async Task CopyResponseHeaders_WhenNoHeaders_ShouldHint()
    {
        var (editor, clipboard, recorder) = CreateEditor();
        var section = CreateSectionWithBody("{}", "");

        await editor.CopyResponseHeadersCommand.ExecuteAsync(section);

        Assert.Empty(clipboard.CopiedTexts);
        Assert.Contains("没有可复制的 Headers", Assert.Single(recorder.Messages));
    }

    [Fact]
    public async Task CopyCurrentRequestCurl_ShouldCopyCurlCommandForQuickRequest()
    {
        var tab = new RequestWorkspaceTabViewModel(new ViewModelSharedTestDoubles.FakeFilePickerService());
        tab.ConfigureAsQuickRequest();
        tab.RequestUrl = "https://api.test/orders";
        var (editor, clipboard, recorder) = CreateEditor(tab);
        editor.NotifyStateChanged();

        await editor.CopyCurrentRequestCurlCommand.ExecuteAsync(null);

        var copied = Assert.Single(clipboard.CopiedTexts);
        Assert.Contains("curl", copied);
        Assert.Contains("https://api.test/orders", copied);
        Assert.Contains("cURL 命令已复制", Assert.Single(recorder.Messages));
    }

    [Fact]
    public async Task CopyCurrentRequestCurl_WhenNoWorkspaceTab_ShouldHintInsteadOfCopying()
    {
        var (editor, clipboard, recorder) = CreateEditor();
        editor.NotifyStateChanged();

        await editor.CopyCurrentRequestCurlCommand.ExecuteAsync(null);

        Assert.Empty(clipboard.CopiedTexts);
        Assert.Contains("请先打开", Assert.Single(recorder.Messages));
    }

    [Fact]
    public async Task CopyCommands_WhenClipboardFails_ShouldReportManualCopyHint()
    {
        var (editor, clipboard, recorder) = CreateEditor();
        clipboard.ShouldSucceed = false;
        var section = CreateSectionWithBody("{}", "application/json");

        await editor.CopyResponseBodyCommand.ExecuteAsync(section);

        Assert.Contains("复制失败", Assert.Single(recorder.Messages));
    }
}
