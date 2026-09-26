namespace ApixPress.App.Models.DTOs;

public sealed class ProjectDataExportResultDto
{
    /// <summary>实际输出目标：备份/HTML 为文件路径，Markdown 为输出目录路径。</summary>
    public string OutputPath { get; init; } = string.Empty;

    public ProjectDataExportFormat Format { get; init; } = ProjectDataExportFormat.Backup;

    public int InterfaceCount { get; init; }

    public int TestCaseCount { get; init; }

    public int EnvironmentCount { get; init; }

    public int VariableCount { get; init; }

    public int HistoryCount { get; init; }

    /// <summary>Markdown 输出的文件数（索引 + 目录文档）；单文件格式为 1。</summary>
    public int FileCount { get; init; }
}
