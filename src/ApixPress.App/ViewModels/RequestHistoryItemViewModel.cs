using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;
using System.Windows.Input;

namespace ApixPress.App.ViewModels;

public partial class RequestHistoryItemViewModel : ViewModelBase
{
    // 大请求体快照全文进 TextBox 会拖慢排版：预览截断，与响应侧展示策略保持一致
    private const int MaxRequestBodyPreviewCharacters = 100 * 1024;

    public required string Id { get; init; }
    public required string Method { get; init; }
    public required string Url { get; init; }
    public required DateTime Timestamp { get; init; }

    /// <summary>右键菜单「重新发送」命令，直接注入行项以跨 ContextMenu PopupRoot 保持绑定可用。</summary>
    public ICommand? ResendCommand { get; set; }

    /// <summary>右键菜单「删除」命令，直接注入行项以跨 ContextMenu PopupRoot 保持绑定可用。</summary>
    public ICommand? DeleteCommand { get; set; }

    [ObservableProperty]
    private bool hasResponse;

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private string durationText = string.Empty;

    [ObservableProperty]
    private string sizeText = string.Empty;

    public RequestSnapshotDto RequestSnapshot { get; set; } = new();

    [ObservableProperty]
    private ResponseSnapshotDto? responseSnapshot;

    public bool HasLoadedDetail { get; private set; }

    public string MethodBadgeClass => Method switch
    {
        "GET" => "HttpMethodTextLabel HttpMethodGet",
        "POST" => "HttpMethodTextLabel HttpMethodPost",
        "PUT" => "HttpMethodTextLabel HttpMethodPut",
        "DELETE" => "HttpMethodTextLabel HttpMethodDelete",
        "PATCH" => "HttpMethodTextLabel HttpMethodPatch",
        _ => "HttpMethodTextLabel HttpMethodOther"
    };

    public string StatusBadgeClass =>
        !HasResponse ? "Light Danger" :
        int.TryParse(StatusText, out var code) ? code switch
        {
            >= 200 and < 300 => "Light Success",
            >= 300 and < 400 => "Light Secondary",
            >= 400 and < 500 => "Light Warning",
            _ => "Light Danger"
        } : "Light Danger";

    public string TimestampText => Timestamp.ToLocalTime().ToString("MM-dd HH:mm");

    public bool HasQueryParameters => RequestSnapshot.QueryParameters.Count > 0;
    public bool HasPathParameters => RequestSnapshot.PathParameters.Count > 0;
    public bool HasRequestParameters => HasQueryParameters || HasPathParameters;
    public bool HasRequestHeaders => RequestSnapshot.Headers.Count > 0;
    public bool HasRequestBody => !string.IsNullOrWhiteSpace(RequestSnapshot.BodyContent);

    public string RequestBodyDisplayText
    {
        get
        {
            var body = RequestSnapshot.BodyContent;
            if (string.IsNullOrEmpty(body) || body.Length <= MaxRequestBodyPreviewCharacters)
            {
                return body;
            }

            return $"{body[..MaxRequestBodyPreviewCharacters]}\n……（快照过长，仅展示前 {MaxRequestBodyPreviewCharacters} 字符）";
        }
    }

    partial void OnHasResponseChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusBadgeClass));
    }

    partial void OnStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(StatusBadgeClass));
    }

    public void ApplyDetail(RequestHistoryItemDto detail)
    {
        RequestSnapshot = detail.RequestSnapshot;
        ResponseSnapshot = detail.ResponseSnapshot;
        HasLoadedDetail = true;
        OnPropertyChanged(nameof(RequestSnapshot));
        OnPropertyChanged(nameof(HasQueryParameters));
        OnPropertyChanged(nameof(HasPathParameters));
        OnPropertyChanged(nameof(HasRequestParameters));
        OnPropertyChanged(nameof(HasRequestHeaders));
        OnPropertyChanged(nameof(HasRequestBody));
        OnPropertyChanged(nameof(RequestBodyDisplayText));
    }
}
