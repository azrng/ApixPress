namespace ApixPress.App.Models.DTOs;

public sealed class ProjectDataExportEntryDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string GroupName { get; init; } = string.Empty;
    public string FolderPath { get; init; } = string.Empty;
    public string ParentId { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = [];
    public string Description { get; init; } = string.Empty;
    public RequestSnapshotDto RequestSnapshot { get; init; } = new();
    public DateTime UpdatedAt { get; init; }
}
