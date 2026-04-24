using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels.Base;
using Azrng.Core.Results;
using System.Text.Encodings.Web;
using System.Text.Json;

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
    private int selectedResponseTab;

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
        HasResponse = true;
        ShowPlaceholder = false;

        if (!result.IsSuccess || result.Data is null)
        {
            StatusText = "请求失败";
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
    }

    public void Reset()
    {
        HasResponse = false;
        ShowPlaceholder = true;
        StatusText = string.Empty;
        DurationText = string.Empty;
        SizeText = string.Empty;
        BodyText = string.Empty;
        HeadersText = string.Empty;
    }

    partial void OnStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(StatusBadgeClass));
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
        if (string.IsNullOrWhiteSpace(response.Content) || !IsJsonResponse(response.Headers))
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
}
