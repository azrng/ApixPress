using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Implementations;
using System.Net.Http.Headers;

namespace ApixPress.App.Tests.Services;

public sealed class RequestExecutionServiceTests
{
    [Fact]
    public void BuildUrl_ShouldReplaceVariablesAndAppendParameters()
    {
        var request = new RequestSnapshotDto
        {
            Method = "GET",
            Url = "{{baseUrl}}/users/{id}",
            PathParameters =
            [
                new RequestKeyValueDto { Name = "id", Value = "42" }
            ],
            QueryParameters =
            [
                new RequestKeyValueDto { Name = "expand", Value = "{{scope}}" }
            ]
        };

        var url = RequestExecutionService.BuildUrl(request, "https://api.example.com", new Dictionary<string, string>
        {
            ["scope"] = "profile"
        });

        Assert.Equal("https://api.example.com/users/42?expand=profile", url);
    }

    [Fact]
    public void BuildUrl_ShouldCombineRelativePathWithEnvironmentBaseUrl()
    {
        var request = new RequestSnapshotDto
        {
            Method = "POST",
            Url = "/orders"
        };

        var url = RequestExecutionService.BuildUrl(request, "https://api.example.com", new Dictionary<string, string>());

        Assert.Equal("https://api.example.com/orders", url);
    }

    [Fact]
    public void ReplaceVariables_ShouldKeepUnknownPlaceholders()
    {
        var result = RequestExecutionService.ReplaceVariables("Bearer {{token}} {{missing}}", new Dictionary<string, string>
        {
            ["token"] = "abc123"
        });

        Assert.Equal("Bearer abc123 {{missing}}", result);
    }

    [Fact]
    public async Task ReadResponseContentPreviewAsync_ShouldCapLargeResponseBody()
    {
        var oversizedBody = new string('a', RequestExecutionService.ResponsePreviewByteLimit + 8192);
        using var content = new StringContent(oversizedBody);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain")
        {
            CharSet = "utf-8"
        };

        var preview = await RequestExecutionService.ReadResponseContentPreviewAsync(content, CancellationToken.None);

        Assert.True(preview.IsTruncated);
        Assert.Equal(RequestExecutionService.ResponsePreviewByteLimit, preview.CapturedSizeBytes);
        Assert.True(preview.SizeBytes > preview.CapturedSizeBytes);
        Assert.Equal(RequestExecutionService.ResponsePreviewByteLimit, preview.Content.Length);
    }

    [Fact]
    public async Task ReadResponseContentPreviewAsync_ShouldKeepFullBodyWhenSmallResponse()
    {
        const string body = "{\"ok\":true}";
        using var content = new StringContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
        {
            CharSet = "utf-8"
        };

        var preview = await RequestExecutionService.ReadResponseContentPreviewAsync(content, CancellationToken.None);

        Assert.False(preview.IsTruncated);
        Assert.Equal(body, preview.Content);
        Assert.Equal(preview.SizeBytes, preview.CapturedSizeBytes);
    }
}
