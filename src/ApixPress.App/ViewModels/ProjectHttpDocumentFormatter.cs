using System.Text;
using ApixPress.App.Models.DTOs;

namespace ApixPress.App.ViewModels;

public static class ProjectHttpDocumentFormatter
{
    public static string BuildUrl(
        string requestUrl,
        string currentBaseUrl,
        IEnumerable<RequestParameterItemViewModel> queryParameters)
    {
        var path = requestUrl.Trim();
        string resolvedUrl;
        if (string.IsNullOrWhiteSpace(path))
        {
            resolvedUrl = string.IsNullOrWhiteSpace(currentBaseUrl)
                ? "未配置 BaseUrl / 未填写路径"
                : $"{currentBaseUrl.TrimEnd('/')}/";
        }
        else if (Uri.TryCreate(path, UriKind.Absolute, out _))
        {
            resolvedUrl = path;
        }
        else if (string.IsNullOrWhiteSpace(currentBaseUrl))
        {
            resolvedUrl = $"未配置 BaseUrl {path}";
        }
        else
        {
            resolvedUrl = $"{currentBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
        }

        var queryString = string.Join("&", queryParameters
            .Where(item => item.IsEnabled && !string.IsNullOrWhiteSpace(item.Name))
            .Select(item =>
                $"{Uri.EscapeDataString(item.Name.Trim())}={Uri.EscapeDataString((item.Value ?? string.Empty).Trim())}"));

        if (string.IsNullOrWhiteSpace(queryString))
        {
            return resolvedUrl;
        }

        return resolvedUrl.Contains('?', StringComparison.Ordinal)
            ? $"{resolvedUrl}&{queryString}"
            : $"{resolvedUrl}?{queryString}";
    }

    public static string BuildCurlSnippet(
        string method,
        string requestUrl,
        string currentBaseUrl,
        RequestConfigTabViewModel configTab)
    {
        var resolvedUrl = ResolveSnippetUrl(requestUrl, currentBaseUrl, configTab.QueryParameters);

        var builder = new StringBuilder();
        builder.Append("curl --request ")
            .Append(method)
            .Append(" \"")
            .Append(EscapeShellDoubleQuotedValue(resolvedUrl))
            .Append('"');

        foreach (var header in configTab.Headers.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            builder.Append(" \\\n  --header \"")
                .Append(EscapeShellDoubleQuotedValue(header.Name.Trim()))
                .Append(": ")
                .Append(EscapeShellDoubleQuotedValue((header.Value ?? string.Empty).Trim()))
                .Append('"');
        }

        var bodyContent = ResolveBodyContent(configTab);
        if (!string.IsNullOrWhiteSpace(bodyContent))
        {
            builder.Append(" \\\n  --data-raw \"")
                .Append(EscapeShellDoubleQuotedValue(bodyContent))
                .Append('"');
        }

        return builder.ToString();
    }

    public static string BuildWgetSnippet(
        string method,
        string requestUrl,
        string currentBaseUrl,
        RequestConfigTabViewModel configTab)
    {
        var resolvedUrl = ResolveSnippetUrl(requestUrl, currentBaseUrl, configTab.QueryParameters);
        var builder = new StringBuilder();
        builder.Append("wget --method=")
            .Append(method)
            .Append(" \\\n  -O -");

        foreach (var header in configTab.Headers.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            builder.Append(" \\\n  --header=\"")
                .Append(EscapeShellDoubleQuotedValue(header.Name.Trim()))
                .Append(": ")
                .Append(EscapeShellDoubleQuotedValue((header.Value ?? string.Empty).Trim()))
                .Append('"');
        }

        var bodyContent = ResolveBodyContent(configTab);
        if (!string.IsNullOrWhiteSpace(bodyContent))
        {
            builder.Append(" \\\n  --body-data=\"")
                .Append(EscapeShellDoubleQuotedValue(bodyContent))
                .Append('"');
        }

        builder.Append(" \\\n  \"")
            .Append(EscapeShellDoubleQuotedValue(resolvedUrl))
            .Append('"');

        return builder.ToString();
    }

    public static string BuildPowerShellSnippet(
        string method,
        string requestUrl,
        string currentBaseUrl,
        RequestConfigTabViewModel configTab)
    {
        var resolvedUrl = ResolveSnippetUrl(requestUrl, currentBaseUrl, configTab.QueryParameters);
        var headers = configTab.Headers.Where(item => !string.IsNullOrWhiteSpace(item.Name)).ToList();
        var bodyContent = ResolveBodyContent(configTab);
        var builder = new StringBuilder();

        if (headers.Count > 0)
        {
            builder.AppendLine("$headers = @{");
            foreach (var header in headers)
            {
                builder.Append("    \"")
                    .Append(EscapePowerShellDoubleQuotedValue(header.Name.Trim()))
                    .Append("\" = \"")
                    .Append(EscapePowerShellDoubleQuotedValue((header.Value ?? string.Empty).Trim()))
                    .AppendLine("\"");
            }

            builder.AppendLine("}");
        }

        builder.Append("Invoke-WebRequest `\n")
            .Append("  -Method ")
            .Append(method)
            .Append(" `\n")
            .Append("  -Uri \"")
            .Append(EscapePowerShellDoubleQuotedValue(resolvedUrl))
            .Append('"');

        if (headers.Count > 0)
        {
            builder.Append(" `\n  -Headers $headers");
        }

        if (!string.IsNullOrWhiteSpace(bodyContent))
        {
            builder.Append(" `\n  -Body \"")
                .Append(EscapePowerShellDoubleQuotedValue(bodyContent))
                .Append('"');
        }

        return builder.ToString();
    }

    public static string ResolveBodyContent(RequestConfigTabViewModel configTab)
    {
        if (configTab.SelectedBodyMode is BodyModes.FormData or BodyModes.FormUrlEncoded)
        {
            return string.Join("&", configTab.FormFields
                .Where(item => item.IsEnabled && !string.IsNullOrWhiteSpace(item.Name))
                .Select(item =>
                    $"{Uri.EscapeDataString(item.Name.Trim())}={Uri.EscapeDataString((item.Value ?? string.Empty).Trim())}"));
        }

        return configTab.HasBodyContent ? configTab.RequestBody.Trim() : string.Empty;
    }

    private static string ResolveSnippetUrl(
        string requestUrl,
        string currentBaseUrl,
        IEnumerable<RequestParameterItemViewModel> queryParameters)
    {
        var url = BuildUrl(requestUrl, currentBaseUrl, queryParameters);
        return url.StartsWith("未配置", StringComparison.OrdinalIgnoreCase)
            ? requestUrl.Trim()
            : url;
    }

    private static string EscapeShellDoubleQuotedValue(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string EscapePowerShellDoubleQuotedValue(string value)
    {
        return value.Replace("`", "``", StringComparison.Ordinal)
            .Replace("\"", "`\"", StringComparison.Ordinal);
    }
}
