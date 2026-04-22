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
        SizeText = UiFormatHelper.FormatBytes(r.SizeBytes);
        BodyText = FormatResponseBody(r);
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
