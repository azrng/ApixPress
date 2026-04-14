namespace ApixPress.App.Models.Entities;

public sealed class RequestCaseEntity
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string EntryType { get; set; } = "quick-request";
    public string Name { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string ParentId { get; set; } = string.Empty;
    public string TagsJson { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RequestSnapshotJson { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
