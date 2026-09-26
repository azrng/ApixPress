using ApixPress.App.Services.Interfaces;

namespace ApixPress.App.Models.DTOs;

public sealed class ProjectDataExportPackageDto
{
    public const string CurrentSchemaVersion = "apixpress.project-data.v1";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public DateTime ExportedAt { get; init; }
    public ProjectDataExportProjectDto Project { get; init; } = new();
    public List<ProjectDataExportEntryDto> Interfaces { get; init; } = [];
    public List<ProjectDataExportEntryDto> TestCases { get; init; } = [];

    // T063 起的可选范围字段：旧备份缺失时按空集合兼容，v1 导入链路继续生效
    public List<ProjectDataExportEnvironmentDto> Environments { get; init; } = [];

    public List<RequestHistoryItemDto> History { get; init; } = [];
}
