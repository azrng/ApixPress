namespace ApixPress.App.Models.DTOs;

public sealed class RequestCaseDto
{
    public string Id { get; init; } = string.Empty;
    public string ProjectId { get; init; } = string.Empty;
    public string EntryType { get; init; } = "quick-request";
    public string Name { get; init; } = string.Empty;
    public string GroupName { get; init; } = "默认分组";
    public string FolderPath { get; init; } = string.Empty;
    public string ParentId { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = [];
    public string Description { get; init; } = string.Empty;
    public RequestSnapshotDto RequestSnapshot { get; init; } = new();
    public DateTime UpdatedAt { get; init; }
}
