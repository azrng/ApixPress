using ApixPress.App.Models.DTOs;

namespace ApixPress.App.Services.Interfaces;

/// <summary>把筛选排序后的项目文档模型渲染为单个自包含 HTML 文档；不访问数据库、不写文件。</summary>
public interface IProjectDataHtmlRenderer
{
    string Render(ProjectDataDocumentModelDto model);
}
