namespace ApixPress.App.Models.DTOs;

public sealed class ApiDocumentDto
{
    public string Id { get; init; } = string.Empty;
    public string ProjectId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public string SourceValue { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public DateTime ImportedAt { get; init; }
}
