using ApixPress.App.Models.DTOs;

namespace ApixPress.App.Services.Interfaces;

public sealed class RequestHistoryItemDto
{
    public string Id { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public bool HasResponse { get; init; }
    public int? StatusCode { get; init; }
    public long DurationMs { get; init; }
    public long SizeBytes { get; init; }
    public RequestSnapshotDto RequestSnapshot { get; init; } = new();
    public ResponseSnapshotDto? ResponseSnapshot { get; init; }
}
