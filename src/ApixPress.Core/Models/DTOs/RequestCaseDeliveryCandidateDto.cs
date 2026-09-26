namespace ApixPress.App.Models.DTOs;

public sealed class RequestCaseDeliveryCandidateDto
{
    public string Id { get; init; } = string.Empty;
    public string ParentId { get; init; } = string.Empty;
    public string EntryType { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string FolderPath { get; init; } = string.Empty;
    public bool IsSelected { get; init; }
    public bool IsAutoIncluded { get; init; }
}
