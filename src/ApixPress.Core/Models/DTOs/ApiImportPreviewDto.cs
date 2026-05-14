namespace ApixPress.App.Models.DTOs;

public sealed class ApiImportPreviewDto
{
    public string DocumentName { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public string SourceValue { get; init; } = string.Empty;
    public int TotalEndpointCount { get; init; }
    public int NewEndpointCount { get; init; }
    public int ConflictCount { get; init; }
    public List<ApiImportConflictDto> ConflictItems { get; init; } = [];
}
