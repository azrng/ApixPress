namespace ApixPress.App.Models.Entities;

public sealed class ApiDocumentEntity
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceValue { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
}
