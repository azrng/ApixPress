namespace ApixPress.App.Models.DTOs;

public sealed class ProjectDataExportRequestDto
{
    public string ProjectId { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string ProjectDescription { get; init; } = string.Empty;
    public string OutputFilePath { get; init; } = string.Empty;
}
