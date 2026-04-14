namespace ApixPress.App.Models.DTOs;

public sealed class ResponseSnapshotDto
{
    public int? StatusCode { get; init; }
    public long DurationMs { get; init; }
    public long SizeBytes { get; init; }
    public string Content { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public string RequestSummary { get; init; } = string.Empty;
    public List<ResponseHeaderDto> Headers { get; init; } = [];
}
