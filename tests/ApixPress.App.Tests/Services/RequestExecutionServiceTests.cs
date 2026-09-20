using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Implementations;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.Tests.ViewModels;
using Azrng.Core.Results;
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
    public void BuildUrl_ShouldSkipDisabledQueryParameters()
    {
        var request = new RequestSnapshotDto
        {
            Method = "GET",
            Url = "https://api.example.com/users",
            QueryParameters =
            [
                new RequestKeyValueDto { Name = "enabled", Value = "1", IsEnabled = true },
                new RequestKeyValueDto { Name = "disabled", Value = "0", IsEnabled = false }
            ]
        };

        var url = RequestExecutionService.BuildUrl(request, "https://api.example.com", new Dictionary<string, string>());

        Assert.Equal("https://api.example.com/users?enabled=1", url);
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
    public async Task SendAsync_ShouldSkipDisabledHeaders()
    {
        var handler = new CapturingHttpMessageHandler();
        using var client = new HttpClient(handler);
        var service = new RequestExecutionService(
            new ViewModelSharedTestDoubles.FakeEnvironmentVariableService(),
            new FakeAppShellSettingsService(),
            serializer: null!,
            (_, _, _) => client);

        var result = await service.SendAsync(
            new RequestSnapshotDto
            {
                Method = "GET",
                Url = "https://api.example.com/users",
                Headers =
                [
                    new RequestKeyValueDto { Name = "X-Enabled", Value = "yes", IsEnabled = true },
                    new RequestKeyValueDto { Name = "X-Disabled", Value = "no", IsEnabled = false }
                ]
            },
            new ProjectEnvironmentDto { Id = "env-1" },
            CancellationToken.None);

        Assert.NotNull(result.Data);
        Assert.Contains("X-Enabled", handler.HeaderNames);
        Assert.DoesNotContain("X-Disabled", handler.HeaderNames);
    }

    [Fact]
    public async Task SendAsync_ShouldCaptureActualRequestSnapshot()
    {
        var handler = new CapturingHttpMessageHandler();
        using var client = new HttpClient(handler);
        var service = new RequestExecutionService(
            new ViewModelSharedTestDoubles.FakeEnvironmentVariableService(),
            new FakeAppShellSettingsService(),
            serializer: null!,
            (_, _, _) => client);

        var result = await service.SendAsync(
            new RequestSnapshotDto
            {
                Method = "post",
                Url = "{{baseUrl}}/orders",
                BodyMode = BodyModes.RawJson,
                BodyContent = "{\"name\":\"order\"}",
                Headers =
                [
                    new RequestKeyValueDto { Name = "X-Token", Value = "{{baseUrl}}", IsEnabled = true },
                    new RequestKeyValueDto { Name = "X-Skip", Value = "0", IsEnabled = false }
                ]
            },
            new ProjectEnvironmentDto { Id = "env-1", BaseUrl = "https://api.example.com" },
            CancellationToken.None);

        var actual = result.Data!.ActualRequest;
        Assert.NotNull(actual);
        Assert.Equal("POST", actual!.Method);
        Assert.Equal("https://api.example.com/orders", actual.Url);
        Assert.Contains(actual.Headers, header => header.Name == "X-Token" && header.Value == "https://api.example.com");
        Assert.DoesNotContain(actual.Headers, header => header.Name == "X-Skip");
        Assert.Contains(actual.Headers, header => header.Name == "Content-Type");
        Assert.True(actual.HasBody);
        Assert.Equal("application/json", actual.BodyContentType);
        Assert.Equal("{\"name\":\"order\"}", actual.BodyText);
    }

    [Fact]
    public async Task SendAsync_ShouldRejectOversizedTextRequestBodyBeforeTransport()
    {
        var handler = new CapturingHttpMessageHandler();
        using var client = new HttpClient(handler);
        var service = new RequestExecutionService(
            new ViewModelSharedTestDoubles.FakeEnvironmentVariableService(),
            new FakeAppShellSettingsService(),
            serializer: null!,
            (_, _, _) => client);

        var result = await service.SendAsync(
            new RequestSnapshotDto
            {
                Method = "POST",
                Url = "https://api.example.com/upload",
                BodyMode = BodyModes.RawText,
                BodyContent = new string('x', RequestExecutionService.RequestBodyByteLimit + 1)
            },
            new ProjectEnvironmentDto { Id = "env-1" },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("8 MB", result.Message);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task SendAsync_ShouldLimitActualRequestBodySnapshot()
    {
        var handler = new CapturingHttpMessageHandler();
        using var client = new HttpClient(handler);
        var service = new RequestExecutionService(
            new ViewModelSharedTestDoubles.FakeEnvironmentVariableService(),
            new FakeAppShellSettingsService(),
            serializer: null!,
            (_, _, _) => client);

        var result = await service.SendAsync(
            new RequestSnapshotDto
            {
                Method = "POST",
                Url = "https://api.example.com/upload",
                BodyMode = BodyModes.RawText,
                BodyContent = new string('x', RequestExecutionService.ActualRequestBodyPreviewCharacterLimit + 1024)
            },
            new ProjectEnvironmentDto { Id = "env-1" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RequestExecutionService.ActualRequestBodyPreviewCharacterLimit, result.Data!.ActualRequest!.BodyText.Length);
        Assert.Equal(RequestExecutionService.ActualRequestBodyPreviewCharacterLimit + 1024, handler.LastBodyText!.Length);
    }

    [Fact]
    public async Task SendAsync_ShouldMarkActualRequestWithoutBody_WhenBodyModeIsNone()
    {
        var handler = new CapturingHttpMessageHandler();
        using var client = new HttpClient(handler);
        var service = new RequestExecutionService(
            new ViewModelSharedTestDoubles.FakeEnvironmentVariableService(),
            new FakeAppShellSettingsService(),
            serializer: null!,
            (_, _, _) => client);

        var result = await service.SendAsync(
            new RequestSnapshotDto
            {
                Method = "GET",
                Url = "https://api.example.com/users"
            },
            new ProjectEnvironmentDto { Id = "env-1" },
            CancellationToken.None);

        var actual = result.Data!.ActualRequest;
        Assert.NotNull(actual);
        Assert.False(actual!.HasBody);
        Assert.Equal(string.Empty, actual.BodyText);
        Assert.Equal(string.Empty, actual.BodyContentType);
    }

    [Fact]
    public async Task SendAsync_ShouldKeepActualRequestWhenTransportFails()
    {
        using var client = new HttpClient(new ThrowingHttpMessageHandler());
        var service = new RequestExecutionService(
            new ViewModelSharedTestDoubles.FakeEnvironmentVariableService(),
            new FakeAppShellSettingsService(),
            serializer: null!,
            (_, _, _) => client);

        var result = await service.SendAsync(
            new RequestSnapshotDto
            {
                Method = "POST",
                Url = "https://api.example.com/orders",
                BodyMode = BodyModes.RawJson,
                BodyContent = "{\"name\":\"中文\"}"
            },
            new ProjectEnvironmentDto { Id = "env-1" },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("https://api.example.com/orders", result.Data!.ActualRequest!.Url);
        Assert.Contains("中文", result.Data.ActualRequest.BodyText);
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

    [Theory]
    [InlineData("image/png", false)]
    [InlineData("application/octet-stream", false)]
    [InlineData("application/pdf", false)]
    [InlineData("application/json", true)]
    [InlineData("text/plain", true)]
    [InlineData("", true)]
    public void IsPreviewableTextContentType_ShouldReturnExpectedResult(string mediaType, bool expected)
    {
        var normalizedMediaType = string.IsNullOrWhiteSpace(mediaType) ? null : mediaType;

        var actual = RequestExecutionService.IsPreviewableTextContentType(normalizedMediaType);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ReadResponseContentPreviewAsync_ShouldSkipBodyForBinaryResponse()
    {
        using var content = new ByteArrayContent(new byte[RequestExecutionService.ResponsePreviewByteLimit * 2]);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Headers.ContentLength = RequestExecutionService.ResponsePreviewByteLimit * 2;

        var preview = await RequestExecutionService.ReadResponseContentPreviewAsync(content, shouldCaptureBodyPreview: false, CancellationToken.None);

        Assert.Equal(string.Empty, preview.Content);
        Assert.Equal(0, preview.CapturedSizeBytes);
        Assert.Equal(RequestExecutionService.ResponsePreviewByteLimit * 2, preview.SizeBytes);
        Assert.False(preview.IsTruncated);
    }

    [Fact]
    public async Task SendAsync_MultipartFormData_ShouldSendTextAndFilePartsWithFilename()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tempFile, new byte[] { 1, 2, 3 });
            var handler = new CapturingHttpMessageHandler();
            using var client = new HttpClient(handler);
            var service = new RequestExecutionService(
                new ViewModelSharedTestDoubles.FakeEnvironmentVariableService(),
                new FakeAppShellSettingsService(),
                serializer: null!,
                (_, _, _) => client);

            var fileField = new FormFieldDto
            {
                Name = "file",
                FieldType = FormFieldTypes.File,
                Files = [new FormFieldFileDto { Path = tempFile, Name = "a.txt", SizeBytes = 3 }]
            };
            var result = await service.SendAsync(
                new RequestSnapshotDto
                {
                    Method = "POST",
                    Url = "https://api.example.com/upload",
                    BodyMode = BodyModes.FormData,
                    BodyContent = FormFieldContentCodec.Encode(
                    [
                        new FormFieldDto { Name = "title", Value = "hello" },
                        fileField,
                        new FormFieldDto { Name = "skipped", Value = "x", IsEnabled = false }
                    ]),
                    FormFields =
                    [
                        new FormFieldDto { Name = "title", Value = "hello" },
                        fileField,
                        new FormFieldDto { Name = "skipped", Value = "x", IsEnabled = false }
                    ]
                },
                new ProjectEnvironmentDto { Id = "env-1" },
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            var body = handler.LastBodyText ?? string.Empty;
            // multipart 结构：boundary 分隔行 + 文件字段的 name/filename disposition
            //（.NET 序列化 disposition 值不加引号；外层 Content-Type 由传输层注入，handler 内拿不到）
            Assert.StartsWith("--", body);
            Assert.Contains("hello", body);
            Assert.Contains("name=file", body);
            Assert.Contains("filename=a.txt", body);
            Assert.DoesNotContain("skipped", body);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SendAsync_BinaryMode_ShouldSendFileBytesAsOctetStreamBody()
    {
        var payload = new byte[] { 0xFF, 0x00, 0xAB };
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tempFile, payload);
            var handler = new CapturingHttpMessageHandler();
            using var client = new HttpClient(handler);
            var service = new RequestExecutionService(
                new ViewModelSharedTestDoubles.FakeEnvironmentVariableService(),
                new FakeAppShellSettingsService(),
                serializer: null!,
                (_, _, _) => client);

            var result = await service.SendAsync(
                new RequestSnapshotDto
                {
                    Method = "POST",
                    Url = "https://api.example.com/blob",
                    BodyMode = BodyModes.Binary,
                    BodyContent = tempFile
                },
                new ProjectEnvironmentDto { Id = "env-1" },
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("application/octet-stream", handler.LastBodyContentType);
            Assert.Equal(payload, handler.LastBodyBytes);

            // 实际请求面板按文件标记展示，而不是暴露原始路径细节
            Assert.Contains("[二进制文件]", result.Data!.ActualRequest!.BodyText);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SendAsync_BinaryMode_MissingFile_ShouldFailWithReadableMessage()
    {
        var handler = new CapturingHttpMessageHandler();
        using var client = new HttpClient(handler);
        var service = new RequestExecutionService(
            new ViewModelSharedTestDoubles.FakeEnvironmentVariableService(),
            new FakeAppShellSettingsService(),
            serializer: null!,
            (_, _, _) => client);

        var result = await service.SendAsync(
            new RequestSnapshotDto
            {
                Method = "POST",
                Url = "https://api.example.com/blob",
                BodyMode = BodyModes.Binary,
                BodyContent = @"C:\__apixpress_not_exist__.bin"
            },
            new ProjectEnvironmentDto { Id = "env-1" },
            CancellationToken.None);

        // 文件缺失不抛未处理异常，转为用户可读的失败结果
        Assert.False(result.IsSuccess);
        Assert.Contains("上传文件不存在", result.Message);
    }

    [Fact]
    public async Task SendAsync_FormDataWithEmptyBodyContent_ShouldStillSendFormFields()
    {
        // BodyContent 为空但 FormFields 携带有效字段时不得静默丢请求体
        var handler = new CapturingHttpMessageHandler();
        using var client = new HttpClient(handler);
        var service = new RequestExecutionService(
            new ViewModelSharedTestDoubles.FakeEnvironmentVariableService(),
            new FakeAppShellSettingsService(),
            serializer: null!,
            (_, _, _) => client);

        var result = await service.SendAsync(
            new RequestSnapshotDto
            {
                Method = "POST",
                Url = "https://api.example.com/upload",
                BodyMode = BodyModes.FormData,
                FormFields =
                [
                    new FormFieldDto { Name = "only", Value = "field" }
                ]
            },
            new ProjectEnvironmentDto { Id = "env-1" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("--", handler.LastBodyText);
        Assert.Contains("field", handler.LastBodyText);
    }

    private sealed class FakeAppShellSettingsService : IAppShellSettingsService
    {
        public Task<IResultModel<AppShellSettingsDto>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IResultModel<AppShellSettingsDto>>(ResultModel<AppShellSettingsDto>.Success(new AppShellSettingsDto()));

        public Task<IResultModel<AppShellSettingsDto>> SaveAsync(AppShellSettingsDto settings, CancellationToken cancellationToken) =>
            Task.FromResult<IResultModel<AppShellSettingsDto>>(ResultModel<AppShellSettingsDto>.Success(settings));
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public IReadOnlyList<string> HeaderNames { get; private set; } = [];

        public HttpRequestMessage? LastRequest { get; private set; }

        /// <summary>请求体在 SendAsync 内读取（请求对象随发送结束被 Dispose，事后无法回读）。</summary>
        public string? LastBodyText { get; private set; }

        public byte[]? LastBodyBytes { get; private set; }

        public string? LastBodyContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            HeaderNames = request.Headers.Select(header => header.Key).ToArray();
            if (request.Content is not null)
            {
                // 请求体只可读取一次（流式内容），单次读 byte 后派生文本；multipart 的 boundary
                // 由传输层发送时注入，handler 内拿不到外层 Content-Type
                LastBodyBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                LastBodyText = System.Text.Encoding.UTF8.GetString(LastBodyBytes);
                LastBodyContentType = request.Content.Headers.ContentType?.MediaType;
            }

            return await Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("网络不可达");
        }
    }
}
