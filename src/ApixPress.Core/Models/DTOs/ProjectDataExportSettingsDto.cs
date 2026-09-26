namespace ApixPress.App.Models.DTOs;

/// <summary>项目级导出偏好：四项备份范围、最近格式与最近输出目录；不保存敏感数据。</summary>
public sealed class ProjectDataExportSettingsDto
{
    /// <summary>最近一次选择的导出格式 token（backup/html/markdown）。</summary>
    public string Format { get; init; } = ProjectDataExportFormats.Backup;

    public bool IncludeInterfaces { get; init; } = true;

    public bool IncludeRequestCases { get; init; } = true;

    public bool IncludeEnvironments { get; init; } = true;

    /// <summary>请求历史默认不导出，与原型 09 的默认勾选一致。</summary>
    public bool IncludeRequestHistory { get; init; }

    public string LastOutputDirectory { get; init; } = string.Empty;
}
