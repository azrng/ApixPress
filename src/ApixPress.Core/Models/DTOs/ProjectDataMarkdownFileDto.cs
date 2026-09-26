namespace ApixPress.App.Models.DTOs;

/// <summary>Markdown 渲染产物：相对输出目录的文件路径与完整内容。</summary>
public sealed class ProjectDataMarkdownFileDto
{
    public string RelativePath { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;
}
