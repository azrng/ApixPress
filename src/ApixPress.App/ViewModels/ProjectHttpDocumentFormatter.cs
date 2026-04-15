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
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
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
        var url = BuildUrl(requestUrl, currentBaseUrl, configTab.QueryParameters);
        var resolvedUrl = url.StartsWith("未配置", StringComparison.OrdinalIgnoreCase)
            ? requestUrl.Trim()
            : url;

        var builder = new StringBuilder();
        builder.Append("curl --request ")
            .Append(method)
            .Append(" \"")
            .Append(EscapeCurlValue(resolvedUrl))
            .Append('"');

        foreach (var header in configTab.Headers.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            builder.Append(" \\\n  --header \"")
                .Append(EscapeCurlValue(header.Name.Trim()))
                .Append(": ")
                .Append(EscapeCurlValue((header.Value ?? string.Empty).Trim()))
                .Append('"');
        }

        var bodyContent = ResolveBodyContent(configTab);
        if (!string.IsNullOrWhiteSpace(bodyContent))
        {
            builder.Append(" \\\n  --data-raw \"")
                .Append(EscapeCurlValue(bodyContent))
                .Append('"');
        }

        return builder.ToString();
    }

    public static string ResolveBodyContent(RequestConfigTabViewModel configTab)
    {
        if (configTab.SelectedBodyMode is BodyModes.FormData or BodyModes.FormUrlEncoded)
        {
            return string.Join("&", configTab.FormFields
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .Select(item =>
                    $"{Uri.EscapeDataString(item.Name.Trim())}={Uri.EscapeDataString((item.Value ?? string.Empty).Trim())}"));
        }

        return configTab.HasBodyContent ? configTab.RequestBody.Trim() : string.Empty;
    }

    private static string EscapeCurlValue(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
