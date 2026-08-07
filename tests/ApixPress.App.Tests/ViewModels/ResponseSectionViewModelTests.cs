using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels;
using Azrng.Core.Results;

namespace ApixPress.App.Tests.ViewModels;

public sealed class ResponseSectionViewModelTests
{
    [Fact]
    public void BeginLoading_ShouldHidePlaceholderUntilRequestCompletes_WhenNoResponseExists()
    {
        var viewModel = new ResponseSectionViewModel();

        viewModel.BeginLoading("正在发送 HTTP 接口请求...");

        Assert.True(viewModel.IsLoading);
        Assert.False(viewModel.ShowPlaceholder);
        Assert.False(viewModel.HasResponse);
        Assert.Equal("正在发送 HTTP 接口请求...", viewModel.LoadingText);

        viewModel.EndLoading();

        Assert.False(viewModel.IsLoading);
        Assert.True(viewModel.ShowPlaceholder);
        Assert.False(viewModel.HasResponse);
    }

    [Fact]
    public void BeginLoading_ShouldKeepExistingResponseVisibleBehindLoading()
    {
        var viewModel = new ResponseSectionViewModel();
        viewModel.ApplyResult(
            ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto
            {
                StatusCode = 200,
                DurationMs = 12,
                SizeBytes = 2,
                Content = "{}"
            }),
            new RequestSnapshotDto());

        viewModel.BeginLoading();

        Assert.True(viewModel.IsLoading);
        Assert.True(viewModel.HasResponse);
        Assert.False(viewModel.ShowPlaceholder);
        Assert.Equal("HTTP 200", viewModel.StatusText);
    }

    [Fact]
    public void ApplyResult_ShouldShowTimeoutNotice_WhenRequestTimesOut()
    {
        var viewModel = new ResponseSectionViewModel();

        viewModel.ApplyResult(
            ResultModel<ResponseSnapshotDto>.Failure("请求超时。", "request_timeout"),
            new RequestSnapshotDto());

        Assert.True(viewModel.ShowResponseNotice);
        Assert.Equal("请求超时", viewModel.ResponseNoticeTitle);
        Assert.Contains("请求超时", viewModel.BodyText);
        Assert.Contains("超时时间", viewModel.ResponseNoticeText);
    }

    [Fact]
    public void BodySearchText_ShouldUpdateMatchCount()
    {
        var viewModel = new ResponseSectionViewModel();
        viewModel.ApplyResult(
            ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto
            {
                StatusCode = 200,
                DurationMs = 12,
                SizeBytes = 32,
                Content = "{\"name\":\"order\",\"type\":\"Order\"}",
                Headers =
                [
                    new ResponseHeaderDto
                    {
                        Name = "Content-Type",
                        Value = "application/json"
                    }
                ]
            }),
            new RequestSnapshotDto());

        viewModel.BodySearchText = "order";

        Assert.Equal("2 处匹配", viewModel.BodySearchResultText);
        Assert.True(viewModel.HasBodySearchMatches);
    }

    [Fact]
    public void BodySearchText_ShouldSkipMatchCount_WhenBodyIsTooLarge()
    {
        var viewModel = new ResponseSectionViewModel
        {
            BodyText = new string('a', 1024 * 1024 + 1)
        };

        viewModel.BodySearchText = "a";

        Assert.Equal("正文较大，已跳过全文搜索", viewModel.BodySearchResultText);
        Assert.True(viewModel.HasBodySearchMatches);
    }

    [Fact]
    public void ApplyResult_ShouldFormatIndentedBody_WhenContentTypeIsApplicationJson()
    {
        var viewModel = new ResponseSectionViewModel();

        viewModel.ApplyResult(
            ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto
            {
                StatusCode = 200,
                DurationMs = 244,
                SizeBytes = 230,
                Content = "{\"data\":[{\"id\":1,\"name\":\"organ\"}],\"isSuccess\":true}",
                Headers =
                [
                    new ResponseHeaderDto
                    {
                        Name = "Content-Type",
                        Value = "application/json; charset=utf-8"
                    }
                ]
            }),
            new RequestSnapshotDto());

        Assert.Contains(Environment.NewLine, viewModel.BodyText);
        Assert.Contains("\"data\": [", viewModel.BodyText);
        Assert.Contains("\"isSuccess\": true", viewModel.BodyText);
    }

    [Fact]
    public async Task ApplyResult_ShouldKeepLargeJsonBodyUnformattedAndLimitDisplay()
    {
        var viewModel = new ResponseSectionViewModel();
        var rawContent = "{\"data\":\"" + new string('a', 256 * 1024 + 1) + "\"}";

        viewModel.ApplyResult(
            ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto
            {
                StatusCode = 200,
                DurationMs = 20,
                SizeBytes = rawContent.Length,
                Content = rawContent,
                Headers =
                [
                    new ResponseHeaderDto
                    {
                        Name = "Content-Type",
                        Value = "application/json"
                    }
                ]
            }),
            new RequestSnapshotDto());

        await viewModel.WaitForPendingBodyFormatAsync();

        Assert.StartsWith(rawContent[..(256 * 1024)], viewModel.BodyText);
        Assert.Contains("界面仅展示前", viewModel.BodyText);
        Assert.DoesNotContain(rawContent[^32..], viewModel.BodyText);
    }

    [Fact]
    public async Task ApplyResult_ShouldCancelStaleBackgroundFormat_WhenNewResultArrives()
    {
        var viewModel = new ResponseSectionViewModel();
        var largeContent = "{\"data\":\"" + new string('x', 80 * 1024) + "\"}";

        viewModel.ApplyResult(
            ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto
            {
                StatusCode = 200,
                DurationMs = 10,
                SizeBytes = largeContent.Length,
                Content = largeContent,
                Headers =
                [
                    new ResponseHeaderDto
                    {
                        Name = "Content-Type",
                        Value = "application/json"
                    }
                ]
            }),
            new RequestSnapshotDto());

        viewModel.ApplyResult(
            ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto
            {
                StatusCode = 201,
                DurationMs = 5,
                SizeBytes = 2,
                Content = "{}",
                Headers =
                [
                    new ResponseHeaderDto
                    {
                        Name = "Content-Type",
                        Value = "application/json"
                    }
                ]
            }),
            new RequestSnapshotDto());

        await viewModel.WaitForPendingBodyFormatAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("HTTP 201", viewModel.StatusText);
        Assert.Equal("{}", viewModel.BodyText);
    }

    [Fact]
    public void ApplyResult_ShouldFormatIndentedBody_WhenContentTypeUsesPlusJsonSuffix()
    {
        var viewModel = new ResponseSectionViewModel();

        viewModel.ApplyResult(
            ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto
            {
                StatusCode = 200,
                DurationMs = 10,
                SizeBytes = 32,
                Content = "{\"title\":\"Bad Request\",\"status\":400}",
                Headers =
                [
                    new ResponseHeaderDto
                    {
                        Name = "Content-Type",
                        Value = "application/problem+json"
                    }
                ]
            }),
            new RequestSnapshotDto());

        Assert.Contains(Environment.NewLine, viewModel.BodyText);
        Assert.Contains("\"title\": \"Bad Request\"", viewModel.BodyText);
        Assert.Contains("\"status\": 400", viewModel.BodyText);
    }

    [Fact]
    public void ApplyResult_ShouldKeepChineseCharacters_WhenJsonResponseContainsUnicodeEscapes()
    {
        var viewModel = new ResponseSectionViewModel();

        viewModel.ApplyResult(
            ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto
            {
                StatusCode = 400,
                DurationMs = 2,
                SizeBytes = 92,
                Content = "{\"isSuccess\":false,\"message\":\"\\u53c2\\u6570\\u683c\\u5f0f\\u4e0d\\u5bf9\"}",
                Headers =
                [
                    new ResponseHeaderDto
                    {
                        Name = "Content-Type",
                        Value = "application/json; charset=utf-8"
                    }
                ]
            }),
            new RequestSnapshotDto());

        Assert.Contains("\"message\": \"参数格式不对\"", viewModel.BodyText);
        Assert.DoesNotContain("\\u53c2", viewModel.BodyText);
    }

    [Fact]
    public void ApplyResult_ShouldKeepOriginalBody_WhenJsonResponseBodyIsInvalid()
    {
        var viewModel = new ResponseSectionViewModel();
        const string rawContent = "{\"data\":";

        viewModel.ApplyResult(
            ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto
            {
                StatusCode = 200,
                DurationMs = 10,
                SizeBytes = 8,
                Content = rawContent,
                Headers =
                [
                    new ResponseHeaderDto
                    {
                        Name = "Content-Type",
                        Value = "application/json"
                    }
                ]
            }),
            new RequestSnapshotDto());

        Assert.Equal(rawContent, viewModel.BodyText);
    }

    [Fact]
    public void ApplyResult_ShouldFormatIndentedBody_WhenContentTypeIsXml()
    {
        var viewModel = new ResponseSectionViewModel();

        viewModel.ApplyResult(
            ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto
            {
                StatusCode = 200,
                DurationMs = 8,
                SizeBytes = 32,
                Content = "<root><item id=\"1\" /></root>",
                Headers =
                [
                    new ResponseHeaderDto
                    {
                        Name = "Content-Type",
                        Value = "application/xml"
                    }
                ]
            }),
            new RequestSnapshotDto());

        Assert.Contains(Environment.NewLine, viewModel.BodyText);
        Assert.Contains("<item id=\"1\" />", viewModel.BodyText);
    }

    [Fact]
    public void ApplyResult_ShouldAppendPreviewNotice_WhenResponseBodyIsTruncated()
    {
        var viewModel = new ResponseSectionViewModel();

        viewModel.ApplyResult(
            ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto
            {
                StatusCode = 200,
                DurationMs = 32,
                SizeBytes = 2 * 1024 * 1024,
                CapturedSizeBytes = 1024 * 1024,
                IsContentTruncated = true,
                Content = new string('a', 32),
                Headers =
                [
                    new ResponseHeaderDto
                    {
                        Name = "Content-Type",
                        Value = "text/plain"
                    }
                ]
            }),
            new RequestSnapshotDto());

        Assert.Contains("响应体过大", viewModel.BodyText);
        Assert.Contains("仅展示前 1 MB", viewModel.SizeText);
    }

    [Fact]
    public void ApplyResult_ShouldShowUnavailablePreviewNotice_WhenBodyPreviewIsDisabled()
    {
        var viewModel = new ResponseSectionViewModel();

        viewModel.ApplyResult(
            ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto
            {
                StatusCode = 200,
                DurationMs = 16,
                SizeBytes = 5 * 1024 * 1024,
                IsBodyPreviewAvailable = false,
                Headers =
                [
                    new ResponseHeaderDto
                    {
                        Name = "Content-Type",
                        Value = "image/png"
                    }
                ]
            }),
            new RequestSnapshotDto());

        Assert.Contains("非文本内容", viewModel.BodyText);
        Assert.Contains("image/png", viewModel.BodyText);
        Assert.Equal("5 MB", viewModel.SizeText);
    }
}
