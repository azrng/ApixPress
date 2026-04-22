using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels;
using Azrng.Core.Results;

namespace ApixPress.App.Tests.ViewModels;

public sealed class ResponseSectionViewModelTests
{
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
}
