namespace ApixPress.App.Models.DTOs;

public sealed class ProjectDataExportRequestDto
{
    public string ProjectId { get; init; } = string.Empty;

    public string ProjectName { get; init; } = string.Empty;

    public string ProjectDescription { get; init; } = string.Empty;

    /// <summary>导出格式；默认备份，HTML/Markdown 输出只读接口文档。</summary>
    public ProjectDataExportFormat Format { get; init; } = ProjectDataExportFormat.Backup;

    // 备份四项范围；HTML/Markdown 固定导出接口与目录及请求用例示例，不读取这些开关
    public bool IncludeInterfaces { get; init; } = true;

    public bool IncludeRequestCases { get; init; } = true;

    public bool IncludeEnvironments { get; init; } = true;

    public bool IncludeRequestHistory { get; init; }

    /// <summary>备份与 HTML 的目标文件完整路径。</summary>
    public string OutputFilePath { get; init; } = string.Empty;

    /// <summary>Markdown 的输出父目录；服务会在其中创建输出目录。</summary>
    public string OutputDirectoryPath { get; init; } = string.Empty;
}
