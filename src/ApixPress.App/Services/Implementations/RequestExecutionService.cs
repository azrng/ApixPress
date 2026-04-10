using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using Azrng.Core;
using Azrng.Core.DependencyInjection;
using Azrng.Core.Json;
using Azrng.Core.Results;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace ApixPress.App.Services.Implementations;

public sealed partial class RequestExecutionService : IRequestExecutionService, ITransientDependency
{
    private readonly IEnvironmentVariableService _environmentVariableService;
    private readonly IJsonSerializer _serializer;

    public RequestExecutionService(IEnvironmentVariableService environmentVariableService, IJsonSerializer serializer)
    {
        _environmentVariableService = environmentVariableService;
        _serializer = serializer;
    }

    public async Task<IResultModel<ResponseSnapshotDto>> SendAsync(
        RequestSnapshotDto request,
        string environmentName,
        CancellationToken cancellationToken)
    {
        try
        {
            var activeVariables = await _environmentVariableService.GetActiveDictionaryAsync(environmentName, cancellationToken);
            var finalUrl = BuildUrl(request, activeVariables);
            using var httpClient = CreateHttpClient(request.IgnoreSslErrors);
            using var message = new HttpRequestMessage(new HttpMethod(request.Method), finalUrl);

            foreach (var header in request.Headers.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
            {
                var value = ReplaceVariables(header.Value, activeVariables);
                if (!message.Headers.TryAddWithoutValidation(header.Name, value))
                {
                    message.Content ??= new StringContent(string.Empty);
                    message.Content.Headers.TryAddWithoutValidation(header.Name, value);
                }
            }

            if (!string.IsNullOrWhiteSpace(request.BodyContent) && request.BodyMode != BodyModes.None)
            {
                var bodyContent = ReplaceVariables(request.BodyContent, activeVariables);
                message.Content = request.BodyMode switch
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

            var stopwatch = Stopwatch.StartNew();
            using var response = await httpClient.SendAsync(message, cancellationToken);
            stopwatch.Stop();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var sizeBytes = Encoding.UTF8.GetByteCount(content);
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
                SizeBytes = sizeBytes,
               // Content = TryFormatJson(content),
                Headers = headers,
                RequestSummary = $"{request.Method.ToUpperInvariant()} {finalUrl}"
            });
        }
        catch (Exception exception)
        {
            return ResultModel<ResponseSnapshotDto>.Failure($"请求发送失败：{exception.Message}", "request_send_failed");
        }
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

    public static string BuildUrl(RequestSnapshotDto request, IReadOnlyDictionary<string, string> variables)
    {
        var url = ReplaceVariables(request.Url, variables);

        foreach (var pathParameter in request.PathParameters.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            var value = Uri.EscapeDataString(ReplaceVariables(pathParameter.Value, variables));
            url = url.Replace($"{{{pathParameter.Name}}}", value, StringComparison.OrdinalIgnoreCase);
            url = url.Replace($":{pathParameter.Name}", value, StringComparison.OrdinalIgnoreCase);
        }

        var builder = new UriBuilder(url);
        var queryItems = ParseQuery(builder.Query);
        foreach (var queryParameter in request.QueryParameters.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            queryItems[queryParameter.Name] = ReplaceVariables(queryParameter.Value, variables);
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

    private static HttpClient CreateHttpClient(bool ignoreSslErrors)
    {
        var handler = new HttpClientHandler();
        if (ignoreSslErrors)
        {
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }

        return new HttpClient(handler, disposeHandler: true);
    }



    [GeneratedRegex("\\{\\{\\s*([\\w.-]+)\\s*\\}\\}")]
    private static partial Regex VariableRegex();
}
