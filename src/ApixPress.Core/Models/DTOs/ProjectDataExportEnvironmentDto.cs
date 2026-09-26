namespace ApixPress.App.Models.DTOs;

/// <summary>备份包中的项目环境及变量；导入端按空集合兼容旧备份缺字段的情况。</summary>
public sealed class ProjectDataExportEnvironmentDto
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public int SortOrder { get; init; }

    public List<ProjectDataExportVariableDto> Variables { get; init; } = [];
}

/// <summary>备份包中的环境变量；只保留恢复所需的键值与启用状态。</summary>
public sealed class ProjectDataExportVariableDto
{
    public string Key { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public bool IsEnabled { get; init; } = true;
}
