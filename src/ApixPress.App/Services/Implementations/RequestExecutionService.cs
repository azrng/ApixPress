using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using Azrng.Core;
using Azrng.Core.DependencyInjection;
using Azrng.Core.Json;
using Azrng.Core.Results;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace ApixPress.App.Services.Implementations;

public sealed partial class RequestExecutionService : IRequestExecutionService, ITransientDependency
{
    internal const int ResponsePreviewByteLimit = 1024 * 1024;
    private static readonly ConcurrentDictionary<RequestClientOptions, HttpClient> SharedHttpClients = new();

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

    public async Task<IResultModel<ResponseSnapshotDto>> SendAsync(
        RequestSnapshotDto request,
        ProjectEnvironmentDto environment,
        CancellationToken cancellationToken)
    {
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

            foreach (var header in request.Headers.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
            {
                var value = ReplaceVariables(header.Value, activeVariables);
                if (!message.Headers.TryAddWithoutValidation(header.Name, value))
                {
                    message.Content ??= new StringContent(string.Empty);
                    message.Content.Headers.TryAddWithoutValidation(header.Name, value);
                }
            }

            var stopwatch = Stopwatch.StartNew();
            using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            stopwatch.Stop();

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
                Content = contentPreview.Content,
                Headers = headers,
                RequestSummary = $"{request.Method.ToUpperInvariant()} {finalUrl}"
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<ResponseSnapshotDto>.Failure("请求已取消。", "request_cancelled");
        }
        catch (TaskCanceledException exception)
        {
            return ResultModel<ResponseSnapshotDto>.Failure($"请求超时：{exception.Message}", "request_timeout");
        }
        catch (HttpRequestException exception)
        {
            return ResultModel<ResponseSnapshotDto>.Failure($"请求发送失败：{exception.Message}", "request_http_failed");
        }
        catch (Exception exception)
        {
            return ResultModel<ResponseSnapshotDto>.Failure($"请求发送失败：{exception.Message}", "request_send_failed");
        }
    }

    private static HttpContent? BuildHttpContent(RequestSnapshotDto request, IReadOnlyDictionary<string, string> activeVariables)
    {
        if (string.IsNullOrWhiteSpace(request.BodyContent) || request.BodyMode == BodyModes.None)
        {
            return null;
        }

        var bodyContent = ReplaceVariables(request.BodyContent, activeVariables);
        return request.BodyMode switch
        {
            BodyModes.RawJson => new StringContent(bodyContent, Encoding.UTF8, "application/json"),
            BodyModes.RawXml => new StringContent(bodyContent, Encoding.UTF8, "application/xml"),
            BodyModes.RawText => new StringContent(bodyContent, Encoding.UTF8, "text/plain"),
            BodyModes.FormUrlEncoded => new FormUrlEncodedContent(
                ParseKeyValuePairs(bodyContent)),
            BodyModes.FormData => BuildMultipartContent(bodyContent),
            _ => new StringContent(bodyContent, Encoding.UTF8, "text/plain")
        };
    }

    private static MultipartFormDataContent BuildMultipartContent(string encodedPairs)
    {
        var content = new MultipartFormDataContent();
        foreach (var pair in ParseKeyValuePairs(encodedPairs))
        {
            content.Add(new StringContent(pair.Value), pair.Key);
        }
        return content;
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
            url = url.Replace($":{pathParameter.Name}", value, StringComparison.OrdinalIgnoreCase);
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
        foreach (var queryParameter in request.QueryParameters.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
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

    private static HttpClient GetOrCreateHttpClient(bool ignoreSslErrors, bool allowAutoRedirect, int timeoutMilliseconds)
    {
        var options = new RequestClientOptions(ignoreSslErrors, allowAutoRedirect, timeoutMilliseconds);
        return SharedHttpClients.GetOrAdd(options, static key =>
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = key.AllowAutoRedirect
            };
            if (key.IgnoreSslErrors)
            {
                handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            }

            var client = new HttpClient(handler, disposeHandler: true);
            client.Timeout = key.TimeoutMilliseconds <= 0
                ? Timeout.InfiniteTimeSpan
                : TimeSpan.FromMilliseconds(key.TimeoutMilliseconds);
            return client;
        });
    }



    [GeneratedRegex("\\{\\{\\s*([\\w.-]+)\\s*\\}\\}")]
    private static partial Regex VariableRegex();

    internal readonly record struct ResponseContentPreviewResult(
        string Content,
        long SizeBytes,
        long CapturedSizeBytes,
        bool IsTruncated);

    private readonly record struct RequestClientOptions(bool IgnoreSslErrors, bool AllowAutoRedirect, int TimeoutMilliseconds);
}
