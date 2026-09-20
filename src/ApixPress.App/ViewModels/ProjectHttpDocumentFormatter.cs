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

        // multipart 用 -F 表达（文件字段带 @ 路径）；Binary 用 --data-binary 携带文件
        if (configTab.SelectedBodyMode == BodyModes.FormData)
        {
            foreach (var field in configTab.FormFields.Where(item => item.IsEnabled && !string.IsNullOrWhiteSpace(item.Name)))
            {
                if (field.FieldType == FormFieldTypes.File)
                {
                    foreach (var file in field.Files)
                    {
                        builder.Append(" \\\n  --form \"")
                            .Append(EscapeShellDoubleQuotedValue(field.Name.Trim()))
                            .Append("=@")
                            .Append(EscapeShellDoubleQuotedValue(file.Path))
                            .Append('"');
                    }
                }
                else
                {
                    builder.Append(" \\\n  --form \"")
                        .Append(EscapeShellDoubleQuotedValue(field.Name.Trim()))
                        .Append('=')
                        .Append(EscapeShellDoubleQuotedValue((field.Value ?? string.Empty).Trim()))
                        .Append('"');
                }
            }

            return builder.ToString();
        }

        if (configTab.SelectedBodyMode == BodyModes.Binary && !string.IsNullOrWhiteSpace(configTab.RequestBody))
        {
            builder.Append(" \\\n  --data-binary \"@")
                .Append(EscapeShellDoubleQuotedValue(configTab.RequestBody.Trim()))
                .Append('"');
            return builder.ToString();
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

        // wget 无内建 multipart 表达：含文件字段时给出可读提示而非误导性代码；Binary 走 --body-file
        if (configTab.SelectedBodyMode == BodyModes.FormData
            && configTab.FormFields.Any(item => item.IsEnabled
                && item.FieldType == FormFieldTypes.File
                && item.Files.Count > 0))
        {
            builder.Append(" \\\n  # wget 不支持 multipart 文件上传，请改用 curl 生成的命令");
            builder.Append(" \\\n  \"")
                .Append(EscapeShellDoubleQuotedValue(resolvedUrl))
                .Append('"');
            return builder.ToString();
        }

        if (configTab.SelectedBodyMode == BodyModes.Binary && !string.IsNullOrWhiteSpace(configTab.RequestBody))
        {
            builder.Append(" \\\n  --body-file=\"")
                .Append(EscapeShellDoubleQuotedValue(configTab.RequestBody.Trim()))
                .Append('"');
        }
        else
        {
            var bodyContent = ResolveBodyContent(configTab);
            if (!string.IsNullOrWhiteSpace(bodyContent))
            {
                builder.Append(" \\\n  --body-data=\"")
                    .Append(EscapeShellDoubleQuotedValue(bodyContent))
                    .Append('"');
            }
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

        // multipart 走 -Form（PowerShell 6+，文件字段用 Get-Item）；Binary 走 -InFile
        if (configTab.SelectedBodyMode == BodyModes.FormData
            && configTab.FormFields.Any(item => item.IsEnabled && !string.IsNullOrWhiteSpace(item.Name)))
        {
            builder.Append(" `\n  -Form @{");
            foreach (var field in configTab.FormFields.Where(item => item.IsEnabled && !string.IsNullOrWhiteSpace(item.Name)))
            {
                if (field.FieldType == FormFieldTypes.File)
                {
                    foreach (var file in field.Files)
                    {
                        builder.Append("\n    \"")
                            .Append(EscapePowerShellDoubleQuotedValue(field.Name.Trim()))
                            .Append("\" = (Get-Item \"")
                            .Append(EscapePowerShellDoubleQuotedValue(file.Path))
                            .Append("\")");
                    }
                }
                else
                {
                    builder.Append("\n    \"")
                        .Append(EscapePowerShellDoubleQuotedValue(field.Name.Trim()))
                        .Append("\" = \"")
                        .Append(EscapePowerShellDoubleQuotedValue((field.Value ?? string.Empty).Trim()))
                        .Append("\"");
                }
            }

            builder.Append("\n  }");
            return builder.ToString();
        }

        if (configTab.SelectedBodyMode == BodyModes.Binary && !string.IsNullOrWhiteSpace(configTab.RequestBody))
        {
            builder.Append(" `\n  -InFile \"")
                .Append(EscapePowerShellDoubleQuotedValue(configTab.RequestBody.Trim()))
                .Append('"');
            return builder.ToString();
        }

        var bodyContent = ResolveBodyContent(configTab);
        if (!string.IsNullOrWhiteSpace(bodyContent))
        {
            builder.Append(" `\n  -Body \"")
                .Append(EscapePowerShellDoubleQuotedValue(bodyContent))
                .Append('"');
        }

        return builder.ToString();
    }

    public static string BuildCSharpHttpClientSnippet(
        string method,
        string requestUrl,
        string currentBaseUrl,
        RequestConfigTabViewModel configTab)
    {
        var resolvedUrl = ResolveSnippetUrl(requestUrl, currentBaseUrl, configTab.QueryParameters);
        var builder = new StringBuilder();
        builder.AppendLine("using System.Net.Http;");
        builder.AppendLine("using System.Text;");
        builder.AppendLine();
        builder.AppendLine("using var httpClient = new HttpClient();");
        builder.Append("using var request = new HttpRequestMessage(HttpMethod.")
            .Append(ToHttpMethodProperty(method))
            .Append(", \"")
            .Append(EscapeCSharpString(resolvedUrl))
            .AppendLine("\");");

        var contentTypeHeader = configTab.Headers
            .FirstOrDefault(item => string.Equals(item.Name?.Trim(), "Content-Type", StringComparison.OrdinalIgnoreCase));

        foreach (var header in configTab.Headers.Where(item =>
                     !string.IsNullOrWhiteSpace(item.Name)
                     && !string.Equals(item.Name.Trim(), "Content-Type", StringComparison.OrdinalIgnoreCase)))
        {
            builder.Append("request.Headers.TryAddWithoutValidation(\"")
                .Append(EscapeCSharpString(header.Name.Trim()))
                .Append("\", \"")
                .Append(EscapeCSharpString((header.Value ?? string.Empty).Trim()))
                .AppendLine("\");");
        }

        AppendCSharpContent(builder, configTab, contentTypeHeader?.Value);

        builder.AppendLine();
        builder.AppendLine("using var response = await httpClient.SendAsync(request);");
        builder.AppendLine("var responseBody = await response.Content.ReadAsStringAsync();");

        return builder.ToString().TrimEnd();
    }

    public static string ResolveBodyContent(RequestConfigTabViewModel configTab)
    {
        // Binary 的请求体是文件路径，正文口径由各生成器以文件参数单独表达，不作为文本正文返回
        if (configTab.SelectedBodyMode is BodyModes.Binary)
        {
            return string.Empty;
        }

        if (configTab.SelectedBodyMode is BodyModes.FormData or BodyModes.FormUrlEncoded)
        {
            return string.Join("&", configTab.FormFields
                .Where(item => item.IsEnabled && !string.IsNullOrWhiteSpace(item.Name) && item.FieldType != FormFieldTypes.File)
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

    private static void AppendCSharpContent(StringBuilder builder, RequestConfigTabViewModel configTab, string? contentTypeHeaderValue)
    {
        // Binary 模式：请求体是文件流而非文本（路径经 EscapeCSharpString 翻倍反斜杠，须放普通字符串而非 verbatim）
        if (configTab.SelectedBodyMode == BodyModes.Binary && !string.IsNullOrWhiteSpace(configTab.RequestBody))
        {
            builder.Append("request.Content = new StreamContent(File.OpenRead(\"")
                .Append(EscapeCSharpString(configTab.RequestBody.Trim()))
                .AppendLine("\"));");
            return;
        }

        if (configTab.SelectedBodyMode is BodyModes.FormUrlEncoded)
        {
            builder.AppendLine("request.Content = new FormUrlEncodedContent(new Dictionary<string, string>");
            builder.AppendLine("{");
            foreach (var field in configTab.FormFields.Where(item => item.IsEnabled && !string.IsNullOrWhiteSpace(item.Name)))
            {
                builder.Append("    [\"")
                    .Append(EscapeCSharpString(field.Name.Trim()))
                    .Append("\"] = \"")
                    .Append(EscapeCSharpString((field.Value ?? string.Empty).Trim()))
                    .AppendLine("\",");
            }

            builder.AppendLine("});");
            return;
        }

        if (configTab.SelectedBodyMode is BodyModes.FormData)
        {
            builder.AppendLine("var formData = new MultipartFormDataContent();");
            foreach (var field in configTab.FormFields.Where(item => item.IsEnabled && !string.IsNullOrWhiteSpace(item.Name)))
            {
                if (field.FieldType == FormFieldTypes.File)
                {
                    foreach (var file in field.Files)
                    {
                        // 三参 Add 写入 filename 到 Content-Disposition，与服务端上传识别口径一致
                        builder.Append("formData.Add(new StreamContent(File.OpenRead(\"")
                            .Append(EscapeCSharpString(file.Path))
                            .Append("\")), \"")
                            .Append(EscapeCSharpString(field.Name.Trim()))
                            .Append("\", \"")
                            .Append(EscapeCSharpString(file.Name))
                            .AppendLine("\");");
                    }
                }
                else
                {
                    builder.Append("formData.Add(new StringContent(\"")
                        .Append(EscapeCSharpString((field.Value ?? string.Empty).Trim()))
                        .Append("\"), \"")
                        .Append(EscapeCSharpString(field.Name.Trim()))
                        .AppendLine("\");");
                }
            }

            builder.AppendLine("request.Content = formData;");
            return;
        }

        var bodyContent = ResolveBodyContent(configTab);
        if (string.IsNullOrWhiteSpace(bodyContent))
        {
            return;
        }

        var mediaType = string.IsNullOrWhiteSpace(contentTypeHeaderValue)
            ? "application/json"
            : contentTypeHeaderValue.Trim();

        builder.Append("request.Content = new StringContent(\"")
            .Append(EscapeCSharpString(bodyContent))
            .Append("\", Encoding.UTF8, \"")
            .Append(EscapeCSharpString(mediaType))
            .AppendLine("\");");
    }

    private static string ToHttpMethodProperty(string method)
    {
        return method.Trim().ToUpperInvariant() switch
        {
            "GET" => "Get",
            "POST" => "Post",
            "PUT" => "Put",
            "DELETE" => "Delete",
            "PATCH" => "Patch",
            "HEAD" => "Head",
            "OPTIONS" => "Options",
            _ => "Get"
        };
    }

    private static string EscapeCSharpString(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
