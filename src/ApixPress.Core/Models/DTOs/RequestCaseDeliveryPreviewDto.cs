namespace ApixPress.App.Models.DTOs;

public sealed class RequestCaseDeliveryPreviewDto
{
    public string FilePath { get; init; } = string.Empty;
    public string SourceProjectId { get; init; } = string.Empty;
    public string SourceProjectName { get; init; } = string.Empty;
    public string SchemaVersion { get; init; } = string.Empty;
    public int InterfaceCount { get; init; }
    public int RequestCaseCount { get; init; }
    public int EnvironmentCount { get; init; }
    public int VariableCount { get; init; }
    public int NewCount { get; init; }
    public int UpdateCount { get; init; }
    public int SkipCount { get; init; }
    public int DuplicateCount { get; init; }
    public int OrphanCount { get; init; }
    public List<RequestCaseDeliveryPreviewItemDto> Items { get; init; } = [];
}
