using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels.Base;
using Azrng.Core.Results;

namespace ApixPress.App.ViewModels;

public partial class ResponseSectionViewModel : ViewModelBase
{
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
        BodyText = r.Content;
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
}
