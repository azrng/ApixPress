namespace ApixPress.App.Models.Entities;

public sealed class RequestHistoryEntity
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string RequestSnapshotJson { get; set; } = string.Empty;
    public string ResponseSnapshotJson { get; set; } = string.Empty;
}
