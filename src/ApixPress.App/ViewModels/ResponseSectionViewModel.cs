using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Helpers;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels.Base;
using Azrng.Core.Results;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Linq;
using Avalonia.Threading;

namespace ApixPress.App.ViewModels;

public partial class ResponseSectionViewModel : ViewModelBase
{
    private const int MaxSynchronousFormattedBodyLength = 256 * 1024;
    private const int MaxSynchronousFormatThreshold = 64 * 1024;
    private const int MaxSearchableBodyLength = 1024 * 1024;
    // 小正文即时搜索保持零感知延迟；超过该长度（最长 1MB）的循环扫描合并进 250ms 防抖
    private const int InstantSearchMaxBodyLength = 16 * 1024;
    private const int MaxDisplayBodyCharacters = 256 * 1024;
    private const int MaxActualRequestBodyCharacters = 256 * 1024;
    private const string FormattingBodyPlaceholder = "正在格式化响应正文...";

    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private CancellationTokenSource? _formatBodyCancellationTokenSource;
    private CancellationTokenSource? _bodySearchDebounceCancellationTokenSource;
    private int _formatBodyGeneration;
    // 当前提示的严重度：info / warning / danger，用于横幅左侧色条与标题着色
    private string _noticeSeverity = "info";

    public bool IsNoticeDanger => ShowResponseNotice && _noticeSeverity == "danger";

    public bool IsNoticeWarning => ShowResponseNotice && _noticeSeverity == "warning";

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

    /// <summary>响应 Headers 只读键值行（对齐原型 kv-grid），每次响应整体替换</summary>
    public IReadOnlyList<ResponseHeaderRowViewModel> HeaderRows { get; private set; } = [];

    public bool HasHeaderRows => HeaderRows.Count > 0;

    [ObservableProperty]
    private string actualRequestMethod = string.Empty;

    [ObservableProperty]
    private string actualRequestUrl = string.Empty;

    [ObservableProperty]
    private string actualRequestBodyContentType = string.Empty;

    [ObservableProperty]
    private string actualRequestBodyText = string.Empty;

    /// <summary>实际请求 Header 只读键值行，每次响应整体替换</summary>
    public IReadOnlyList<ResponseHeaderRowViewModel> ActualRequestHeaderRows { get; private set; } = [];

    public bool HasActualRequestHeaderRows => ActualRequestHeaderRows.Count > 0;

    /// <summary>是否有可展示的实际请求数据；失败结果与旧历史记录没有该数据</summary>
    public bool HasActualRequest => !string.IsNullOrWhiteSpace(ActualRequestUrl);

    public bool HasActualRequestBody => !string.IsNullOrEmpty(ActualRequestBodyText);

    public bool HasActualRequestBodyContentType => !string.IsNullOrEmpty(ActualRequestBodyContentType);

    /// <summary>方法徽章配色沿用既有 Light* 惯例（GET 绿 / DELETE 红等），对齐用例列表的方法徽章</summary>
    public string ActualRequestMethodBadgeClass => ActualRequestMethod switch
    {
        "GET" => "HttpMethodTextLabel HttpMethodGet",
        "POST" => "HttpMethodTextLabel HttpMethodPost",
        "PUT" => "HttpMethodTextLabel HttpMethodPut",
        "DELETE" => "HttpMethodTextLabel HttpMethodDelete",
        "PATCH" => "HttpMethodTextLabel HttpMethodPatch",
        _ => "HttpMethodTextLabel HttpMethodOther"
    };

    [ObservableProperty]
    private bool showResponseNotice;

    /// <summary>失败结果（网络/超时/取消）或 5xx 服务端错误时为 true：横幅显示「重试」按钮一键重发。</summary>
    [ObservableProperty]
    private bool canRetryRequest;

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

    /// <summary>当前展示正文中所有匹配的字符偏移，供查看器高亮。</summary>
    public IReadOnlyList<int> BodySearchMatchOffsets { get; private set; } = [];

    public bool HasBodySearchMatches => BodySearchMatchOffsets.Count > 0;

    public int CurrentBodySearchMatchOffset => _bodySearchMatchIndex >= 0 && _bodySearchMatchIndex < BodySearchMatchOffsets.Count
        ? BodySearchMatchOffsets[_bodySearchMatchIndex] : -1;

    private int _bodySearchMatchIndex = -1;

    /// <summary>定位下一个匹配，末尾回到第一个。</summary>
    [RelayCommand(CanExecute = nameof(HasBodySearchMatches))]
    private void NextBodySearchMatch() => MoveBodySearchMatch(1);

    /// <summary>定位上一个匹配，开头回到最后一个。</summary>
    [RelayCommand(CanExecute = nameof(HasBodySearchMatches))]
    private void PreviousBodySearchMatch() => MoveBodySearchMatch(-1);

    private void MoveBodySearchMatch(int step)
    {
        if (!HasBodySearchMatches) return;
        _bodySearchMatchIndex = (_bodySearchMatchIndex + step + BodySearchMatchOffsets.Count) % BodySearchMatchOffsets.Count;
        SelectedResponseTab = 0;
        BodySearchResultText = $"{_bodySearchMatchIndex + 1} / {BodySearchMatchOffsets.Count}";
        OnPropertyChanged(nameof(CurrentBodySearchMatchOffset));
    }

    /// <summary>复制命令的正文来源：格式化占位符期间没有可复制内容，其余（含失败时的错误正文）直接复制界面文本。</summary>
    public string CopyableBodyText =>
        string.Equals(BodyText, FormattingBodyPlaceholder, StringComparison.Ordinal) ? string.Empty : BodyText;

    public bool HasCopyableHeaders => !string.IsNullOrWhiteSpace(HeadersText);

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
        // 大响应时 BodyText 写入与 JSON 格式化是响应区卡顿的主要来源，整段计时
        PerfLog.Measure("响应·结果渲染", () => ApplyResultCore(result, request));
    }

    private void ApplyResultCore(IResultModel<ResponseSnapshotDto> result, RequestSnapshotDto request)
    {
        CancelPendingBodyFormat();
        IsLoading = false;
        HasResponse = true;
        ShowPlaceholder = false;

        if (!result.IsSuccess || result.Data is null || !string.IsNullOrWhiteSpace(result.Data.ErrorMessage))
        {
            var error = !string.IsNullOrWhiteSpace(result.Data?.ErrorMessage) ? result.Data.ErrorMessage : result.Message;
            ApplyFailureNotice(result.Code, error);
            StatusText = ResponseNoticeTitle;
            DurationText = string.Empty;
            SizeText = string.Empty;
            BodyText = error;
            HeadersText = string.Empty;
            SetHeaderRows([]);
            ApplyActualRequest(result.Data?.ActualRequest ?? BuildFallbackActualRequest(request));
            return;
        }

        var r = result.Data;
        StatusText = r.StatusCode is { } code ? $"HTTP {code}" : "请求完成";
        DurationText = $"{r.DurationMs} ms";
        SizeText = FormatResponseSizeText(r);
        HeadersText = string.Join(Environment.NewLine, r.Headers.Select(h => $"{h.Name}: {h.Value}"));
        SetHeaderRows(r.Headers
            .Select(h => new ResponseHeaderRowViewModel(h.Name, h.Value))
            .ToArray());
        ApplyActualRequest(r.ActualRequest);
        ApplyStatusNotice(r.StatusCode);

        if (r.Content is { Length: > MaxSynchronousFormatThreshold })
        {
            BodyText = FormattingBodyPlaceholder;
            _ = FormatBodyInBackgroundAsync(r, ++_formatBodyGeneration);
        }
        else
        {
            BodyText = BuildDisplayBody(r);
        }
    }

    public async Task WaitForPendingBodyFormatAsync(TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (DateTime.UtcNow < deadline)
        {
            if (!string.Equals(BodyText, FormattingBodyPlaceholder, StringComparison.Ordinal)
                || _formatBodyCancellationTokenSource is null)
            {
                return;
            }

            await Task.Delay(20);
        }
    }

    private async Task FormatBodyInBackgroundAsync(ResponseSnapshotDto response, int generation)
    {
        var cancellationToken = CancellationTokenSourceHelper
            .Refresh(ref _formatBodyCancellationTokenSource)
            .Token;

        try
        {
            var body = await Task.Run(() => BuildDisplayBody(response), cancellationToken);
            if (cancellationToken.IsCancellationRequested || generation != _formatBodyGeneration || IsDisposed)
            {
                return;
            }

            void ApplyBody()
            {
                if (IsDisposed || generation != _formatBodyGeneration)
                {
                    return;
                }

                BodyText = body;
            }

            if (Avalonia.Application.Current is null)
            {
                ApplyBody();
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(ApplyBody);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public void Reset()
    {
        CancelPendingBodyFormat();
        IsLoading = false;
        HasResponse = false;
        ShowPlaceholder = true;
        StatusText = string.Empty;
        DurationText = string.Empty;
        SizeText = string.Empty;
        BodyText = string.Empty;
        HeadersText = string.Empty;
        SetHeaderRows([]);
        ClearActualRequest();
        ClearResponseNotice();
        BodySearchText = string.Empty;
        BodySearchResultText = string.Empty;
    }

    public void BeginLoading(string? loadingText = null)
    {
        CancelPendingBodyFormat();
        LoadingText = string.IsNullOrWhiteSpace(loadingText)
            ? "正在发送请求..."
            : loadingText.Trim();
        IsLoading = true;
        // 正在发送时收起重试入口，避免叠加重复请求；结果到达后由 ApplyResult 重新定状态
        CanRetryRequest = false;

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

    protected override void DisposeManaged()
    {
        CancelPendingBodyFormat();
        CancellationTokenSourceHelper.CancelAndDispose(ref _bodySearchDebounceCancellationTokenSource);
    }

    private void CancelPendingBodyFormat()
    {
        _formatBodyGeneration++;
        CancellationTokenSourceHelper.CancelAndDispose(ref _formatBodyCancellationTokenSource);
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

    private async Task DebounceBodySearchAsync()
    {
        var cts = CancellationTokenSourceHelper.Refresh(ref _bodySearchDebounceCancellationTokenSource);
        try
        {
            await Task.Delay(250, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (IsDisposed)
        {
            return;
        }

        UpdateBodySearchResult();
    }

    partial void OnBodySearchResultTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasBodySearchMatches));
    }

    private void ApplyFailureNotice(string? code, string message)
    {
        ShowResponseNotice = true;
        CanRetryRequest = true;
        _noticeSeverity = "danger";
        OnPropertyChanged(nameof(IsNoticeDanger));
        OnPropertyChanged(nameof(IsNoticeWarning));
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
        // 5xx（网关/服务端故障）与 transport 级失败一样提供重试；3xx/4xx 是请求本身的问题，重发无意义
        CanRetryRequest = statusCode >= 500;
        _noticeSeverity = statusCode switch
        {
            >= 300 and < 400 => "warning",
            >= 400 and < 500 => "warning",
            >= 500 => "danger",
            _ => "info"
        };
        OnPropertyChanged(nameof(IsNoticeDanger));
        OnPropertyChanged(nameof(IsNoticeWarning));
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
        CanRetryRequest = false;
        _noticeSeverity = "info";
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

    private void SetHeaderRows(IReadOnlyList<ResponseHeaderRowViewModel> rows)
    {
        HeaderRows = rows;
        OnPropertyChanged(nameof(HeaderRows));
        OnPropertyChanged(nameof(HasHeaderRows));
    }

    private void ApplyActualRequest(ActualRequestSnapshotDto? actualRequest)
    {
        if (actualRequest is null)
        {
            ClearActualRequest();
            return;
        }

        ActualRequestMethod = actualRequest.Method;
        ActualRequestUrl = actualRequest.Url;
        ActualRequestBodyContentType = actualRequest.BodyContentType;
        ActualRequestBodyText = LimitActualRequestBodyText(actualRequest.BodyText);
        SetActualRequestHeaderRows(actualRequest.Headers
            .Select(h => new ResponseHeaderRowViewModel(h.Name, h.Value))
            .ToArray());
    }

    private static ActualRequestSnapshotDto? BuildFallbackActualRequest(RequestSnapshotDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Method) && string.IsNullOrWhiteSpace(request.Url))
        {
            return null;
        }

        var headers = request.Headers
            .Where(header => header.IsEnabled && !string.IsNullOrWhiteSpace(header.Name))
            .Select(header => new ResponseHeaderDto
            {
                Name = header.Name,
                Value = header.Value
            })
            .ToList();

        var bodyContentType = request.BodyMode switch
        {
            BodyModes.RawJson => "application/json",
            BodyModes.RawXml => "application/xml",
            BodyModes.RawText => "text/plain",
            BodyModes.FormUrlEncoded => "application/x-www-form-urlencoded",
            BodyModes.FormData => "multipart/form-data",
            BodyModes.Binary => "application/octet-stream",
            _ => string.Empty
        };

        return new ActualRequestSnapshotDto
        {
            Method = request.Method.ToUpperInvariant(),
            Url = request.Url,
            Headers = headers,
            BodyContentType = bodyContentType,
            BodyText = request.BodyMode == BodyModes.Binary
                ? $"[二进制文件] {request.BodyContent}"
                : request.BodyContent,
            HasBody = request.BodyMode != BodyModes.None
                       && (!string.IsNullOrWhiteSpace(request.BodyContent) || request.FormFields.Count > 0)
        };
    }

    private void ClearActualRequest()
    {
        ActualRequestMethod = string.Empty;
        ActualRequestUrl = string.Empty;
        ActualRequestBodyContentType = string.Empty;
        ActualRequestBodyText = string.Empty;
        SetActualRequestHeaderRows([]);
    }

    private static string LimitActualRequestBodyText(string body)
    {
        if (string.IsNullOrEmpty(body) || body.Length <= MaxActualRequestBodyCharacters)
        {
            return body;
        }

        return $"{body[..MaxActualRequestBodyCharacters]}{Environment.NewLine}{Environment.NewLine}[请求体过大，界面仅展示前 {UiFormatHelper.FormatBytes(MaxActualRequestBodyCharacters)}。]";
    }

    private void SetActualRequestHeaderRows(IReadOnlyList<ResponseHeaderRowViewModel> rows)
    {
        ActualRequestHeaderRows = rows;
        OnPropertyChanged(nameof(ActualRequestHeaderRows));
        OnPropertyChanged(nameof(HasActualRequestHeaderRows));
    }

    partial void OnActualRequestUrlChanged(string value)
    {
        OnPropertyChanged(nameof(HasActualRequest));
    }

    partial void OnActualRequestMethodChanged(string value)
    {
        OnPropertyChanged(nameof(ActualRequestMethodBadgeClass));
    }

    partial void OnActualRequestBodyTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasActualRequestBody));
    }

    partial void OnActualRequestBodyContentTypeChanged(string value)
    {
        OnPropertyChanged(nameof(HasActualRequestBodyContentType));
    }

    private void UpdateBodySearchResult()
    {
        CancellationTokenSourceHelper.CancelAndDispose(ref _bodySearchDebounceCancellationTokenSource);
        SetBodySearchMatches([]);
        if (string.IsNullOrWhiteSpace(BodySearchText) || string.IsNullOrEmpty(BodyText)
            || string.Equals(BodyText, FormattingBodyPlaceholder, StringComparison.Ordinal))
        {
            BodySearchResultText = string.Empty;
            return;
        }

        if (BodyText.Length > MaxSearchableBodyLength)
        {
            BodySearchResultText = "正文较大，已跳过全文搜索";
            return;
        }

        // 大正文（>16KB）的全文字符合并进 250ms 防抖，避免每键在 UI 线程循环扫描；
        // 小正文即时搜索保持零感知延迟
        if (BodyText.Length > InstantSearchMaxBodyLength)
        {
            _ = DebounceCountBodySearchMatchesAsync();
            return;
        }

        CountBodySearchMatches();
    }

    private async Task DebounceCountBodySearchMatchesAsync()
    {
        var cts = CancellationTokenSourceHelper.Refresh(ref _bodySearchDebounceCancellationTokenSource);
        try
        {
            await Task.Delay(250, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (IsDisposed)
        {
            return;
        }

        CountBodySearchMatches();
    }

    private void CountBodySearchMatches()
    {
        if (string.IsNullOrWhiteSpace(BodySearchText)) return;
        var matches = new List<int>();
        var startIndex = 0;
        while (startIndex < BodyText.Length)
        {
            var index = BodyText.IndexOf(BodySearchText, startIndex, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                break;
            }

            matches.Add(index);
            startIndex = index + Math.Max(BodySearchText.Length, 1);
        }

        SetBodySearchMatches(matches);
        BodySearchResultText = matches.Count == 0 ? "未找到匹配" : $"1 / {matches.Count}";
        if (matches.Count > 0) SelectedResponseTab = 0;
    }

    private void SetBodySearchMatches(IReadOnlyList<int> matches)
    {
        BodySearchMatchOffsets = matches;
        _bodySearchMatchIndex = matches.Count > 0 ? 0 : -1;
        BodySearchResultText = string.Empty;
        OnPropertyChanged(nameof(BodySearchMatchOffsets));
        OnPropertyChanged(nameof(CurrentBodySearchMatchOffset));
        OnPropertyChanged(nameof(HasBodySearchMatches));
        NextBodySearchMatchCommand.NotifyCanExecuteChanged();
        PreviousBodySearchMatchCommand.NotifyCanExecuteChanged();
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
            return LimitDisplayBody(formattedBody);
        }

        var notice = response.SizeBytes > response.CapturedSizeBytes
            ? $"[响应体过大，当前仅展示前 {UiFormatHelper.FormatBytes(response.CapturedSizeBytes)}，完整响应约 {UiFormatHelper.FormatBytes(response.SizeBytes)}。]"
            : $"[响应体过大，当前仅展示前 {UiFormatHelper.FormatBytes(response.CapturedSizeBytes)}，完整大小未知。]";
        var bodyWithNotice = string.IsNullOrWhiteSpace(formattedBody)
            ? notice
            : $"{formattedBody}{Environment.NewLine}{Environment.NewLine}{notice}";
        return LimitDisplayBody(bodyWithNotice);
    }

    private static string LimitDisplayBody(string body)
    {
        if (string.IsNullOrEmpty(body) || body.Length <= MaxDisplayBodyCharacters)
        {
            return body;
        }

        return $"{body[..MaxDisplayBodyCharacters]}{Environment.NewLine}{Environment.NewLine}[界面仅展示前 {UiFormatHelper.FormatBytes(MaxDisplayBodyCharacters)}，避免大文本导致卡顿。]";
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

        if (response.Content.Length > MaxSynchronousFormattedBodyLength)
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
