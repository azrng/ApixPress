namespace ApixPress.App.Models.DTOs;

/// <summary>按范围筛选并稳定排序后的项目文档导出模型，供 HTML/Markdown 渲染器共用。</summary>
public sealed class ProjectDataDocumentModelDto
{
    public string ProjectName { get; init; } = string.Empty;

    public string ProjectDescription { get; init; } = string.Empty;

    public DateTime ExportedAt { get; init; }

    public List<ProjectDataDocumentInterfaceDto> Interfaces { get; init; } = [];
}

/// <summary>文档中的接口条目：接口定义加上其下请求用例示例。</summary>
public sealed class ProjectDataDocumentInterfaceDto
{
    public string FolderPath { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public RequestSnapshotDto Snapshot { get; init; } = new();

    public List<ProjectDataDocumentCaseDto> Cases { get; init; } = [];
}

/// <summary>文档中的请求用例示例：只读参考，不可恢复导入。</summary>
public sealed class ProjectDataDocumentCaseDto
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public RequestSnapshotDto Snapshot { get; init; } = new();
}
