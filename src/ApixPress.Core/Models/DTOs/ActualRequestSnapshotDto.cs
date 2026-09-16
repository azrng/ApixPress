namespace ApixPress.App.Models.DTOs;

/// <summary>
/// 实际发送的请求快照：记录变量替换、路径与查询参数合并后的最终请求形态，用于调试区「实际请求」展示
/// </summary>
public sealed class ActualRequestSnapshotDto
{
    public string Method { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    /// <summary>实际携带的请求头（请求头 + 内容头），不含传输层自动补的 Host、Accept-Encoding 等</summary>
    public List<ResponseHeaderDto> Headers { get; init; } = [];

    /// <summary>请求体媒体类型（如 application/json），无请求体时为空</summary>
    public string BodyContentType { get; init; } = string.Empty;

    /// <summary>变量替换后的请求体文本；二进制/文件类请求体不采集</summary>
    public string BodyText { get; init; } = string.Empty;

    public bool HasBody { get; init; }
}
