using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels.Base;
using Azrng.Core.Results;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Linq;

namespace ApixPress.App.ViewModels;

public partial class ResponseSectionViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [ObservableProperty]
    private bool hasResponse;

    [ObservableProperty]
    private bool showPlaceholder = true;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string loadingText = "正在发送请求...";

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private string durationText = string.Empty;

    [ObservableProperty]
    private string sizeText = string.Empty;

    [ObservableProperty]
    private string bodyText = string.Empty;

    [ObservableProperty]
    private string headersText = string.Empty;

    [ObservableProperty]
    private bool showResponseNotice;

    [ObservableProperty]
    private string responseNoticeTitle = string.Empty;

    [ObservableProperty]
    private string responseNoticeText = string.Empty;

    [ObservableProperty]
    private string bodySearchText = string.Empty;

    [ObservableProperty]
    private string bodySearchResultText = string.Empty;

    [ObservableProperty]
    private int selectedResponseTab;

    public bool HasBodySearchMatches => !string.IsNullOrWhiteSpace(BodySearchResultText);

    public string StatusBadgeClass
    {
        get
        {
            if (string.IsNullOrWhiteSpace(StatusText))
            {
                return "Light Secondary";
            }

            if (StatusText == "请求失败")
            {
                return "Light Danger";
            }

            if (StatusText.StartsWith("HTTP ", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(StatusText["HTTP ".Length..], out var code))
            {
                return code switch
                {
                    >= 200 and < 300 => "Light Success",
                    >= 300 and < 400 => "Light Secondary",
                    >= 400 and < 500 => "Light Warning",
                    _ => "Light Danger"
                };
            }

            return "Light Primary";
        }
    }

    public void ApplyResult(IResultModel<ResponseSnapshotDto> result, RequestSnapshotDto request)
    {
        IsLoading = false;
        HasResponse = true;
        ShowPlaceholder = false;

        if (!result.IsSuccess || result.Data is null)
        {
            ApplyFailureNotice(result.Code, result.Message);
            StatusText = ResponseNoticeTitle;
            DurationText = string.Empty;
            SizeText = string.Empty;
            BodyText = result.Message;
            HeadersText = string.Empty;
            return;
        }

        var r = result.Data;
        StatusText = r.StatusCode is { } code ? $"HTTP {code}" : "请求完成";
        DurationText = $"{r.DurationMs} ms";
        SizeText = FormatResponseSizeText(r);
        BodyText = BuildDisplayBody(r);
        HeadersText = string.Join(Environment.NewLine, r.Headers.Select(h => $"{h.Name}: {h.Value}"));
        ApplyStatusNotice(r.StatusCode);
    }

    public void Reset()
    {
        IsLoading = false;
        HasResponse = false;
        ShowPlaceholder = true;
        StatusText = string.Empty;
        DurationText = string.Empty;
        SizeText = string.Empty;
        BodyText = string.Empty;
        HeadersText = string.Empty;
        ClearResponseNotice();
        BodySearchText = string.Empty;
        BodySearchResultText = string.Empty;
    }

    public void BeginLoading(string? loadingText = null)
    {
        LoadingText = string.IsNullOrWhiteSpace(loadingText)
            ? "正在发送请求..."
            : loadingText.Trim();
        IsLoading = true;

        if (!HasResponse)
        {
            ShowPlaceholder = false;
        }
    }

    public void EndLoading()
    {
        IsLoading = false;

        if (!HasResponse)
        {
            ShowPlaceholder = true;
        }
    }

    partial void OnStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(StatusBadgeClass));
    }

    partial void OnBodyTextChanged(string value)
    {
        UpdateBodySearchResult();
    }

    partial void OnBodySearchTextChanged(string value)
    {
        UpdateBodySearchResult();
    }

    partial void OnBodySearchResultTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasBodySearchMatches));
    }

    private void ApplyFailureNotice(string? code, string message)
    {
        ShowResponseNotice = true;
        ResponseNoticeTitle = code switch
        {
            "request_cancelled" => "请求已取消",
            "request_timeout" => "请求超时",
            "request_http_failed" => "网络请求失败",
            "request_send_failed" => "请求发送失败",
            _ => "请求失败"
        };
        ResponseNoticeText = string.IsNullOrWhiteSpace(message)
            ? ResolveFailureHint(code)
            : $"{message} {ResolveFailureHint(code)}".Trim();
    }

    private void ApplyStatusNotice(int? statusCode)
    {
        if (statusCode is null or < 300)
        {
            ClearResponseNotice();
            return;
        }

        ShowResponseNotice = true;
        ResponseNoticeTitle = statusCode switch
        {
            >= 300 and < 400 => $"重定向响应 HTTP {statusCode}",
            >= 400 and < 500 => $"客户端错误 HTTP {statusCode}",
            >= 500 => $"服务端错误 HTTP {statusCode}",
            _ => $"HTTP {statusCode}"
        };
        ResponseNoticeText = statusCode switch
        {
            >= 300 and < 400 => "服务器返回重定向响应，可检查是否关闭了自动跳转或目标地址是否变化。",
            >= 400 and < 500 => "请求已发送但服务器拒绝处理，请检查地址、参数、鉴权 Header 或请求体。",
            >= 500 => "服务器处理请求时发生错误，请结合响应正文和服务端日志排查。",
            _ => "请求已完成，但响应状态需要关注。"
        };
    }

    private void ClearResponseNotice()
    {
        ShowResponseNotice = false;
        ResponseNoticeTitle = string.Empty;
        ResponseNoticeText = string.Empty;
    }

    private static string ResolveFailureHint(string? code)
    {
        return code switch
        {
            "request_cancelled" => "可以调整参数后重新发送。",
            "request_timeout" => "请检查接口是否可达，或在通用设置中调整请求超时时间。",
            "request_http_failed" => "请检查网络、DNS、代理、证书或目标服务状态。",
            "request_send_failed" => "请检查请求地址、环境 BaseUrl、变量替换结果和请求配置。",
            _ => string.Empty
        };
    }

    private void UpdateBodySearchResult()
    {
        if (string.IsNullOrWhiteSpace(BodySearchText) || string.IsNullOrEmpty(BodyText))
        {
            BodySearchResultText = string.Empty;
            return;
        }

        var count = 0;
        var startIndex = 0;
        while (startIndex < BodyText.Length)
        {
            var index = BodyText.IndexOf(BodySearchText, startIndex, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                break;
            }

            count++;
            startIndex = index + Math.Max(BodySearchText.Length, 1);
        }

        BodySearchResultText = count == 0 ? "未找到匹配" : $"{count} 处匹配";
    }

    private static string BuildDisplayBody(ResponseSnapshotDto response)
    {
        if (!response.IsBodyPreviewAvailable)
        {
            return BuildUnavailablePreviewNotice(response);
        }

        var formattedBody = FormatResponseBody(response);
        if (!response.IsContentTruncated)
        {
            return formattedBody;
        }

        var notice = response.SizeBytes > response.CapturedSizeBytes
            ? $"[响应体过大，当前仅展示前 {UiFormatHelper.FormatBytes(response.CapturedSizeBytes)}，完整响应约 {UiFormatHelper.FormatBytes(response.SizeBytes)}。]"
            : $"[响应体过大，当前仅展示前 {UiFormatHelper.FormatBytes(response.CapturedSizeBytes)}，完整大小未知。]";
        return string.IsNullOrWhiteSpace(formattedBody)
            ? notice
            : $"{formattedBody}{Environment.NewLine}{Environment.NewLine}{notice}";
    }

    private static string FormatResponseSizeText(ResponseSnapshotDto response)
    {
        if (!response.IsContentTruncated)
        {
            return UiFormatHelper.FormatBytes(response.SizeBytes);
        }

        return response.SizeBytes > response.CapturedSizeBytes
            ? $"{UiFormatHelper.FormatBytes(response.SizeBytes)}（仅展示前 {UiFormatHelper.FormatBytes(response.CapturedSizeBytes)}）"
            : $"至少 {UiFormatHelper.FormatBytes(response.CapturedSizeBytes)}（仅展示前 {UiFormatHelper.FormatBytes(response.CapturedSizeBytes)}）";
    }

    private static string BuildUnavailablePreviewNotice(ResponseSnapshotDto response)
    {
        var contentType = response.Headers.FirstOrDefault(header =>
            string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))?.Value;
        var summary = response.SizeBytes > 0
            ? UiFormatHelper.FormatBytes(response.SizeBytes)
            : "未知大小";
        return string.IsNullOrWhiteSpace(contentType)
            ? $"[当前响应为非文本内容，未加载正文预览。大小：{summary}]"
            : $"[当前响应为非文本内容，未加载正文预览。类型：{contentType}；大小：{summary}]";
    }

    private static string FormatResponseBody(ResponseSnapshotDto response)
    {
        if (string.IsNullOrWhiteSpace(response.Content))
        {
            return response.Content;
        }

        if (IsXmlResponse(response.Headers))
        {
            try
            {
                return XDocument.Parse(response.Content).ToString();
            }
            catch (Exception) when (response.Content.Length > 0)
            {
                return response.Content;
            }
        }

        if (!IsJsonResponse(response.Headers))
        {
            return response.Content;
        }

        try
        {
            using var document = JsonDocument.Parse(response.Content);
            return JsonSerializer.Serialize(document.RootElement, PrettyJsonOptions);
        }
        catch (JsonException)
        {
            return response.Content;
        }
    }

    private static bool IsJsonResponse(IEnumerable<ResponseHeaderDto> headers)
    {
        var contentType = headers.FirstOrDefault(header =>
            string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))?.Value;
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var mediaType = contentType.Split(';', 2, StringSplitOptions.TrimEntries)[0];
        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
               || mediaType.Equals("text/json", StringComparison.OrdinalIgnoreCase)
               || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsXmlResponse(IEnumerable<ResponseHeaderDto> headers)
    {
        var contentType = headers.FirstOrDefault(header =>
            string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))?.Value;
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var mediaType = contentType.Split(';', 2, StringSplitOptions.TrimEntries)[0];
        return mediaType.Equals("application/xml", StringComparison.OrdinalIgnoreCase)
               || mediaType.Equals("text/xml", StringComparison.OrdinalIgnoreCase)
               || mediaType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase);
    }
}
