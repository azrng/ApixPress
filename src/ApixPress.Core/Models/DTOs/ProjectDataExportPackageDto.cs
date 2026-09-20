namespace ApixPress.App.Models.DTOs;

public sealed class ProjectDataExportPackageDto
{
    public const string CurrentSchemaVersion = "apixpress.project-data.v1";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public DateTime ExportedAt { get; init; }
    public ProjectDataExportProjectDto Project { get; init; } = new();
    public List<ProjectDataExportEntryDto> Interfaces { get; init; } = [];
    public List<ProjectDataExportEntryDto> TestCases { get; init; } = [];
}
