namespace ApixPress.App.Models.DTOs;

public sealed class ProjectDataExportResultDto
{
    public string FilePath { get; init; } = string.Empty;
    public int InterfaceCount { get; init; }
    public int TestCaseCount { get; init; }
}
