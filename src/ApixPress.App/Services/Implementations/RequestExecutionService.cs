using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using Azrng.Core;
using Azrng.Core.DependencyInjection;
using Azrng.Core.Json;
using Azrng.Core.Results;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace ApixPress.App.Services.Implementations;

public sealed partial class RequestExecutionService : IRequestExecutionService, ITransientDependency
{
    internal const int ResponsePreviewByteLimit = 1024 * 1024;
    internal const int ActualRequestBodyPreviewCharacterLimit = 256 * 1024;
    internal const int RequestBodyByteLimit = 8 * 1024 * 1024;
    private const int MaxSharedHttpClients = 16;
    private static readonly ConcurrentDictionary<RequestClientOptions, SharedHttpClientEntry> SharedHttpClients = new();
    private static long _lastClientCleanupTicks;

    private readonly IAppShellSettingsService _appShellSettingsService;
    private readonly IEnvironmentVariableService _environmentVariableService;
    private readonly IJsonSerializer _serializer;
    private readonly Func<bool, bool, int, HttpClient> _httpClientFactory;

    public RequestExecutionService(
        IEnvironmentVariableService environmentVariableService,
        IAppShellSettingsService appShellSettingsService,
        IJsonSerializer serializer)
        : this(environmentVariableService, appShellSettingsService, serializer, GetOrCreateHttpClient)
    {
    }

    internal RequestExecutionService(
        IEnvironmentVariableService environmentVariableService,
        IAppShellSettingsService appShellSettingsService,
        IJsonSerializer serializer,
        Func<bool, bool, int, HttpClient> httpClientFactory)
    {
        _environmentVariableService = environmentVariableService;
        _appShellSettingsService = appShellSettingsService;
        _serializer = serializer;
        _httpClientFactory = httpClientFactory;
    }

    public Task<IResultModel<ResponseSnapshotDto>> SendAsync(
        RequestSnapshotDto request,
        ProjectEnvironmentDto environment,
        CancellationToken cancellationToken)
    {
        return PerfLog.MeasureAsync("请求·发送总耗时", () => SendCoreAsync(request, environment, cancellationToken));
    }

    private async Task<IResultModel<ResponseSnapshotDto>> SendCoreAsync(
        RequestSnapshotDto request,
        ProjectEnvironmentDto environment,
        CancellationToken cancellationToken)
    {
        ActualRequestSnapshotDto? actualRequest = null;
        try
        {
            var shellSettingsResult = await _appShellSettingsService.LoadAsync(cancellationToken);
            var shellSettings = shellSettingsResult.Data ?? new AppShellSettingsDto();
            var activeVariables = new Dictionary<string, string>(
                await _environmentVariableService.GetActiveDictionaryAsync(environment.Id, cancellationToken),
                StringComparer.OrdinalIgnoreCase);
            var resolvedBaseUrl = ReplaceVariables(environment.BaseUrl, activeVariables);
            if (!string.IsNullOrWhiteSpace(resolvedBaseUrl))
            {
                activeVariables["baseUrl"] = resolvedBaseUrl;
            }

            var finalUrl = BuildUrl(request, resolvedBaseUrl, activeVariables);
            var ignoreSslErrors = request.IgnoreSslErrors || !shellSettings.ValidateSslCertificate;
            var httpClient = _httpClientFactory(
                ignoreSslErrors,
                shellSettings.AutoFollowRedirects,
                shellSettings.RequestTimeoutMilliseconds);
            using var message = new HttpRequestMessage(new HttpMethod(request.Method), finalUrl);

            if (shellSettings.SendNoCacheHeader && !message.Headers.Contains("Cache-Control"))
            {
                message.Headers.CacheControl = new CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true
                };
            }

            message.Content = BuildHttpContent(request, activeVariables);

            foreach (var header in request.Headers.Where(item => item.IsEnabled && !string.IsNullOrWhiteSpace(item.Name)))
            {
                var value = ReplaceVariables(header.Value, activeVariables);
                if (!message.Headers.TryAddWithoutValidation(header.Name, value))
                {
                    message.Content ??= new StringContent(string.Empty);
                    message.Content.Headers.TryAddWithoutValidation(header.Name, value);
                }
            }

            actualRequest = CaptureActualRequest(request, message, finalUrl, activeVariables);

            var stopwatch = Stopwatch.StartNew();
            using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            stopwatch.Stop();

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            var shouldCaptureBodyPreview = IsPreviewableTextContentType(mediaType);
            var contentPreview = await ReadResponseContentPreviewAsync(response.Content, cancellationToken);
            var headers = response.Headers.Concat(response.Content.Headers)
                .SelectMany(header => header.Value.Select(value => new ResponseHeaderDto
                {
                    Name = header.Key,
                    Value = value
                }))
                .ToList();

            return ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto
            {
                StatusCode = (int)response.StatusCode,
                DurationMs = stopwatch.ElapsedMilliseconds,
                SizeBytes = contentPreview.SizeBytes,
                CapturedSizeBytes = contentPreview.CapturedSizeBytes,
                IsContentTruncated = contentPreview.IsTruncated,
                IsBodyPreviewAvailable = shouldCaptureBodyPreview,
                Content = contentPreview.Content,
                Headers = headers,
                RequestSummary = $"{request.Method.ToUpperInvariant()} {finalUrl}",
                ActualRequest = actualRequest
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return BuildFailureResult("请求已取消。", "request_cancelled", actualRequest);
        }
        catch (TaskCanceledException exception)
        {
            return BuildFailureResult($"请求超时：{exception.Message}", "request_timeout", actualRequest);
        }
        catch (HttpRequestException exception)
        {
            return BuildFailureResult($"请求发送失败：{exception.Message}", "request_http_failed", actualRequest);
        }
        catch (Exception exception)
        {
            return BuildFailureResult($"请求发送失败：{exception.Message}", "request_send_failed", actualRequest);
        }
    }

    private static IResultModel<ResponseSnapshotDto> BuildFailureResult(
        string message,
        string code,
        ActualRequestSnapshotDto? actualRequest)
    {
        if (actualRequest is null)
        {
            return ResultModel<ResponseSnapshotDto>.Failure(message, code);
        }

        // Azrng 的 Failure 工厂不承载 Data；请求已经组装完成时保留失败结果中的实际请求快照。
        return new ResultModel<ResponseSnapshotDto>
        {
            IsSuccess = false,
            Code = code,
            Message = message,
            Data = new ResponseSnapshotDto { ActualRequest = actualRequest, ErrorMessage = message }
        };
    }

    private static HttpContent? BuildHttpContent(RequestSnapshotDto request, IReadOnlyDictionary<string, string> activeVariables)
    {
        if (request.BodyMode == BodyModes.None)
        {
            return null;
        }

        // FormData 允许 BodyContent 为空但 FormFields 携带有效字段（手填快照/旧数据修复场景），不静默丢请求体
        var hasSendableFormFields = request.BodyMode == BodyModes.FormData && request.FormFields
            .Any(field => field.IsEnabled && !string.IsNullOrWhiteSpace(field.Name));
        if (string.IsNullOrWhiteSpace(request.BodyContent) && !hasSendableFormFields)
        {
            return null;
        }

        var bodyContent = ReplaceVariables(request.BodyContent, activeVariables);
        if (request.BodyMode != BodyModes.FormData && request.BodyMode != BodyModes.Binary)
        {
            EnsureTextRequestBodySize(bodyContent);
        }

        return request.BodyMode switch
        {
            BodyModes.RawJson => new StringContent(bodyContent, Encoding.UTF8, "application/json"),
            BodyModes.RawXml => new StringContent(bodyContent, Encoding.UTF8, "application/xml"),
            BodyModes.RawText => new StringContent(bodyContent, Encoding.UTF8, "text/plain"),
            BodyModes.FormUrlEncoded => new FormUrlEncodedContent(
                ParseKeyValuePairs(bodyContent)),
            BodyModes.FormData => BuildFormDataContent(request, bodyContent, activeVariables),
            BodyModes.Binary => BuildBinaryFileContent(bodyContent),
            _ => new StringContent(bodyContent, Encoding.UTF8, "text/plain")
        };
    }

    /// <summary>
    /// 组装 multipart 请求体：快照 FormFields 优先（文本 + 文件 part），旧快照无 FormFields 时
    /// 回退按 URL 编码串组装纯文本 part，与既有行为保持一致。
    /// </summary>
    private static MultipartFormDataContent BuildFormDataContent(
        RequestSnapshotDto request,
        string encodedBodyContent,
        IReadOnlyDictionary<string, string> activeVariables)
    {
        var fields = request.FormFields.Count > 0
            ? request.FormFields
            : FormFieldContentCodec.Parse(encodedBodyContent);

        var content = new MultipartFormDataContent();
        var hasPart = false;
        var textBodyBytes = 0L;
        foreach (var field in fields.Where(item => item.IsEnabled && !string.IsNullOrWhiteSpace(item.Name)))
        {
            if (field.FieldType == FormFieldTypes.File)
            {
                foreach (var file in field.Files)
                {
                    var part = new StreamContent(OpenFileForUpload(file.Path));
                    part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    // 三参 Add 才会写入 filename 到 Content-Disposition，服务端靠它识别上传文件名
                    content.Add(part, field.Name, file.Name);
                    hasPart = true;
                }
            }
            else
            {
                var fieldValue = ReplaceVariables(field.Value, activeVariables);
                textBodyBytes = checked(textBodyBytes + Encoding.UTF8.GetByteCount(fieldValue));
                EnsureTextRequestBodySize(textBodyBytes);
                content.Add(new StringContent(fieldValue), field.Name);
                hasPart = true;
            }
        }

        // 字段解码为空（例如手工清空）时保持旧行为：按 URL 编码串兜底组装，不发出空 multipart
        if (!hasPart)
        {
            foreach (var pair in ParseKeyValuePairs(encodedBodyContent))
            {
                content.Add(new StringContent(pair.Value), pair.Key);
            }
        }

        return content;
    }

    private static void EnsureTextRequestBodySize(string body)
    {
        EnsureTextRequestBodySize(Encoding.UTF8.GetByteCount(body));
    }

    private static void EnsureTextRequestBodySize(long byteCount)
    {
        if (byteCount > RequestBodyByteLimit)
        {
            throw new InvalidOperationException("文本请求体超过 8 MB 限制，请缩小内容或改用文件上传。");
        }
    }

    /// <summary>按路径构建单个文件 part；文件缺失抛业务可读异常，由发送入口统一转为失败结果。</summary>
    private static StreamContent BuildBinaryFileContent(string path)
    {
        var content = new StreamContent(OpenFileForUpload(path));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return content;
    }

    private static FileStream OpenFileForUpload(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new InvalidOperationException($"上传文件不存在：{path}");
        }

        // FileShare.Read 允许文件同时被其他程序打开；流随 HttpRequestMessage.Dispose 一并释放
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    /// <summary>
    /// 采集实际发送的请求：URL 已完成变量替换与参数合并，Headers 为消息上真实携带的
    /// 请求头与内容头（不含传输层发送时才注入的 Host、Accept-Encoding 等）。
    /// </summary>
    private static ActualRequestSnapshotDto CaptureActualRequest(
        RequestSnapshotDto request,
        HttpRequestMessage message,
        string finalUrl,
        IReadOnlyDictionary<string, string> activeVariables)
    {
        var headers = new List<ResponseHeaderDto>();
        headers.AddRange(EnumerateActualHeaders(message.Headers));
        if (message.Content is not null)
        {
            headers.AddRange(EnumerateActualHeaders(message.Content.Headers));
        }

        return new ActualRequestSnapshotDto
        {
            Method = request.Method.ToUpperInvariant(),
            Url = finalUrl,
            Headers = headers,
            BodyContentType = message.Content?.Headers.ContentType?.MediaType ?? string.Empty,
            BodyText = message.Content is null ? string.Empty : ResolveActualBodyText(request, activeVariables),
            HasBody = message.Content is not null
        };
    }

    /// <summary>
    /// 实际请求体展示口径：FormData 含文件字段时 BodyContent 是 JSON 编码，对用户无意义，
    /// 改按「字段=值 / 文件名」汇总；Binary 展示文件路径标记；其余模式保持变量替换后的原文。
    /// </summary>
    private static string ResolveActualBodyText(RequestSnapshotDto request, IReadOnlyDictionary<string, string> activeVariables)
    {
        if (request.BodyMode == BodyModes.FormData && request.FormFields.Count > 0)
        {
            var lines = request.FormFields
                .Where(field => field.IsEnabled && !string.IsNullOrWhiteSpace(field.Name))
                .Select(field => field.FieldType == FormFieldTypes.File
                    ? $"{field.Name}={string.Join(", ", field.Files.Select(file => file.Name))}（文件）"
                    : $"{field.Name}={ReplaceVariables(field.Value, activeVariables)}");
            return LimitActualRequestBodyText(string.Join("\n", lines));
        }

        if (request.BodyMode == BodyModes.Binary && !string.IsNullOrWhiteSpace(request.BodyContent))
        {
            return LimitActualRequestBodyText($"[二进制文件] {request.BodyContent}");
        }

        return LimitActualRequestBodyText(ReplaceVariables(request.BodyContent, activeVariables));
    }

    private static string LimitActualRequestBodyText(string body)
    {
        return body.Length <= ActualRequestBodyPreviewCharacterLimit
            ? body
            : body[..ActualRequestBodyPreviewCharacterLimit];
    }

    private static IEnumerable<ResponseHeaderDto> EnumerateActualHeaders(HttpHeaders headers)
    {
        return headers.SelectMany(header => header.Value.Select(value => new ResponseHeaderDto
        {
            Name = header.Key,
            Value = value
        }));
    }

    private static IEnumerable<KeyValuePair<string, string>> ParseKeyValuePairs(string encoded)
    {
        foreach (var pair in encoded.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            yield return new KeyValuePair<string, string>(key, value);
        }
    }

    /// <summary>
    /// 替换 ":name" 形式的路径参数。仅当令牌后跟串尾或路径/查询边界符时才替换，
    /// 避免 ":id" 误伤 ":identity" 这类前缀相同的参数名。
    /// </summary>
    private static string ReplaceColonPathParameter(string url, string name, string value)
    {
        var token = $":{name}";
        var builder = new System.Text.StringBuilder(url.Length + value.Length);
        var searchIndex = 0;
        while (true)
        {
            var found = url.IndexOf(token, searchIndex, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                builder.Append(url, searchIndex, url.Length - searchIndex);
                break;
            }

            var nextIndex = found + token.Length;
            var isBoundary = nextIndex >= url.Length || url[nextIndex] is '/' or '?' or '&' or '#';
            builder.Append(url, searchIndex, found - searchIndex);
            if (isBoundary)
            {
                builder.Append(value);
            }
            else
            {
                builder.Append(url, found, nextIndex - found);
            }

            searchIndex = nextIndex;
        }

        return builder.ToString();
    }

    public static string BuildUrl(RequestSnapshotDto request, string baseUrl, IReadOnlyDictionary<string, string> variables)
    {
        var effectiveVariables = new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(baseUrl) && !effectiveVariables.ContainsKey("baseUrl"))
        {
            effectiveVariables["baseUrl"] = baseUrl;
        }

        var url = ReplaceVariables(request.Url, effectiveVariables);

        foreach (var pathParameter in request.PathParameters.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            var value = Uri.EscapeDataString(ReplaceVariables(pathParameter.Value, effectiveVariables));
            url = url.Replace($"{{{pathParameter.Name}}}", value, StringComparison.OrdinalIgnoreCase);
            url = ReplaceColonPathParameter(url, pathParameter.Name, value);
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException("当前环境未配置 BaseUrl，无法发送相对路径请求。");
            }

            url = $"{baseUrl.TrimEnd('/')}/{url.TrimStart('/')}";
        }

        var builder = new UriBuilder(url);
        var queryItems = ParseQuery(builder.Query);
        foreach (var queryParameter in request.QueryParameters.Where(item => item.IsEnabled && !string.IsNullOrWhiteSpace(item.Name)))
        {
            queryItems[queryParameter.Name] = ReplaceVariables(queryParameter.Value, effectiveVariables);
        }

        builder.Query = string.Join("&", queryItems.Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));
        return builder.Uri.ToString();
    }

    public static string ReplaceVariables(string input, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        return VariableRegex().Replace(input, match =>
        {
            var key = match.Groups[1].Value;
            return variables.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var cleanQuery = query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(cleanQuery))
        {
            return result;
        }

        foreach (var pair in cleanQuery.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            result[key] = value;
        }

        return result;
    }

    internal static async Task<ResponseContentPreviewResult> ReadResponseContentPreviewAsync(HttpContent content, CancellationToken cancellationToken)
    {
        return await ReadResponseContentPreviewAsync(
            content,
            IsPreviewableTextContentType(content.Headers.ContentType?.MediaType),
            cancellationToken);
    }

    internal static async Task<ResponseContentPreviewResult> ReadResponseContentPreviewAsync(
        HttpContent content,
        bool shouldCaptureBodyPreview,
        CancellationToken cancellationToken)
    {
        if (!shouldCaptureBodyPreview)
        {
            return new ResponseContentPreviewResult(
                string.Empty,
                content.Headers.ContentLength ?? 0,
                0,
                false);
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var detectionLimit = ResponsePreviewByteLimit + 1;
        using var previewBuffer = new MemoryStream(Math.Min(ResponsePreviewByteLimit, (int)Math.Min(content.Headers.ContentLength ?? ResponsePreviewByteLimit, ResponsePreviewByteLimit)));
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(81920);

        var storedBytes = 0;
        var streamEnded = false;
        try
        {
            while (storedBytes < detectionLimit)
            {
                var read = await stream.ReadAsync(rentedBuffer.AsMemory(0, rentedBuffer.Length), cancellationToken);
                if (read == 0)
                {
                    streamEnded = true;
                    break;
                }

                var bytesToWrite = Math.Min(read, detectionLimit - storedBytes);
                previewBuffer.Write(rentedBuffer, 0, bytesToWrite);
                storedBytes += bytesToWrite;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }

        var capturedSizeBytes = Math.Min(storedBytes, ResponsePreviewByteLimit);
        var isTruncated = !streamEnded || storedBytes > ResponsePreviewByteLimit;
        var totalSizeBytes = content.Headers.ContentLength ?? capturedSizeBytes;
        if (content.Headers.ContentLength is long declaredLength && declaredLength > capturedSizeBytes)
        {
            isTruncated = true;
        }

        var previewBytes = previewBuffer.ToArray();
        if (previewBytes.Length > capturedSizeBytes)
        {
            Array.Resize(ref previewBytes, (int)capturedSizeBytes);
        }

        var encoding = ResolveEncoding(content.Headers.ContentType?.CharSet);
        return new ResponseContentPreviewResult(
            encoding.GetString(previewBytes),
            totalSizeBytes,
            capturedSizeBytes,
            isTruncated);
    }

    private static Encoding ResolveEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset.Trim('"'));
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    internal static bool IsPreviewableTextContentType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return true;
        }

        if (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
               || mediaType.Equals("text/json", StringComparison.OrdinalIgnoreCase)
               || mediaType.Equals("application/xml", StringComparison.OrdinalIgnoreCase)
               || mediaType.Equals("text/xml", StringComparison.OrdinalIgnoreCase)
               || mediaType.Equals("application/javascript", StringComparison.OrdinalIgnoreCase)
               || mediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
               || mediaType.Equals("application/graphql-response+json", StringComparison.OrdinalIgnoreCase)
               || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)
               || mediaType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase);
    }

    private static void CleanupExpiredClientsIfNeeded()
    {
        var nowTicks = DateTimeOffset.UtcNow.UtcTicks;
        var cleanupInterval = TimeSpan.FromMinutes(5).Ticks;
        var lastTicks = Interlocked.Read(ref _lastClientCleanupTicks);

        if (nowTicks - lastTicks < cleanupInterval)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _lastClientCleanupTicks, nowTicks, lastTicks) != lastTicks)
        {
            return;
        }

        if (SharedHttpClients.Count <= MaxSharedHttpClients)
        {
            return;
        }

        var overflowCount = SharedHttpClients.Count - MaxSharedHttpClients;
        var keysToRemove = SharedHttpClients
            .OrderBy(item => item.Value.LastAccessedAt)
            .Take(overflowCount)
            .Select(item => item.Key)
            .ToList();
        foreach (var key in keysToRemove)
        {
            // 只从字典移除，不主动 Dispose：进行中的请求可能仍持有该 client，
            // 直接 Dispose 会导致请求中途 ObjectDisposedException；未引用后交给 GC 终结回收。
            if (SharedHttpClients.TryRemove(key, out _))
            {
            }
        }
    }

    /// <summary>根据全局设置创建或复用 HTTP 客户端。</summary>
    internal static HttpClient GetOrCreateHttpClient(bool ignoreSslErrors, bool allowAutoRedirect, int timeoutMilliseconds)
    {
        CleanupExpiredClientsIfNeeded();

        var options = new RequestClientOptions(ignoreSslErrors, allowAutoRedirect, timeoutMilliseconds);
        var entry = SharedHttpClients.GetOrAdd(options, static key =>
        {
            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = key.AllowAutoRedirect,
                // 与 Postman/浏览器一致：按 Content-Encoding 透明解压（gzip/deflate/br），
                // 否则压缩响应会以二进制乱码进入响应区
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                // 定期回收连接池，让 DNS 变更生效；同一 client 长期复用不再缓存过期连接
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            };
            if (key.IgnoreSslErrors)
            {
                handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            }

            var client = new HttpClient(handler, disposeHandler: true);
            // 全局设置以 0 明确表示不限时；负数仍视为非法值并使用 120 秒兜底。
            client.Timeout = key.TimeoutMilliseconds switch
            {
                0 => Timeout.InfiniteTimeSpan,
                < 0 => TimeSpan.FromSeconds(120),
                _ => TimeSpan.FromMilliseconds(Math.Min(key.TimeoutMilliseconds, 600_000))
            };
            return new SharedHttpClientEntry(client);
        });

        entry.Touch();
        return entry.Client;
    }



    [GeneratedRegex("\\{\\{\\s*([\\w.-]+)\\s*\\}\\}")]
    private static partial Regex VariableRegex();

    internal readonly record struct ResponseContentPreviewResult(
        string Content,
        long SizeBytes,
        long CapturedSizeBytes,
        bool IsTruncated);

    private readonly record struct RequestClientOptions(bool IgnoreSslErrors, bool AllowAutoRedirect, int TimeoutMilliseconds);

    private sealed class SharedHttpClientEntry
    {
        public SharedHttpClientEntry(HttpClient client)
        {
            Client = client;
            Touch();
        }

        public HttpClient Client { get; }
        public DateTimeOffset LastAccessedAt { get; private set; }

        public void Touch()
        {
            LastAccessedAt = DateTimeOffset.UtcNow;
        }
    }
}
