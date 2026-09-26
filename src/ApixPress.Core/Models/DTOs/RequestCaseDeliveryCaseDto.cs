namespace ApixPress.App.Models.DTOs;

public sealed class RequestCaseDeliveryCaseDto
{
    public string SourceId { get; init; } = string.Empty;
    public string ParentInterfaceSourceId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string GroupName { get; init; } = string.Empty;
    public string FolderPath { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = [];
    public string Description { get; init; } = string.Empty;
    public RequestSnapshotDto RequestSnapshot { get; init; } = new();
    public DateTime UpdatedAt { get; init; }
}
