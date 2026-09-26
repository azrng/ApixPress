using ApixPress.App.Models.DTOs;

namespace ApixPress.App.Services.Interfaces;

/// <summary>把筛选排序后的项目文档模型渲染为 Markdown 文件计划（索引 + 每目录一个文件）；不访问数据库、不写文件。</summary>
public interface IProjectDataMarkdownRenderer
{
    IReadOnlyList<ProjectDataMarkdownFileDto> Render(ProjectDataDocumentModelDto model);
}
