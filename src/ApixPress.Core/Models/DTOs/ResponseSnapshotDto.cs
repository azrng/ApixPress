namespace ApixPress.App.Models.DTOs;

public sealed class ResponseSnapshotDto
{
    public int? StatusCode { get; init; }
    public long DurationMs { get; init; }
    public long SizeBytes { get; init; }
    public long CapturedSizeBytes { get; init; }
    public bool IsContentTruncated { get; init; }
    public bool IsBodyPreviewAvailable { get; init; } = true;
    public string Content { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public string RequestSummary { get; init; } = string.Empty;
    public List<ResponseHeaderDto> Headers { get; init; } = [];

    /// <summary>实际发送的请求快照；仅请求成功发出时填充，历史记录与失败结果可能为空</summary>
    public ActualRequestSnapshotDto? ActualRequest { get; init; }
}
