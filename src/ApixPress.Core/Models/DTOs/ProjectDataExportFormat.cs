namespace ApixPress.App.Models.DTOs;

/// <summary>项目数据导出格式：备份用于恢复，HTML/Markdown 用于发布只读接口文档。</summary>
public enum ProjectDataExportFormat
{
    Backup = 0,
    Html = 1,
    Markdown = 2
}

/// <summary>项目数据导出格式的序列化与界面取值约定。</summary>
public static class ProjectDataExportFormats
{
    public const string Backup = "backup";
    public const string Html = "html";
    public const string Markdown = "markdown";

    public static string ToToken(ProjectDataExportFormat format)
    {
        return format switch
        {
            ProjectDataExportFormat.Html => Html,
            ProjectDataExportFormat.Markdown => Markdown,
            _ => Backup
        };
    }

    public static ProjectDataExportFormat FromToken(string? token)
    {
        return token switch
        {
            Html => ProjectDataExportFormat.Html,
            Markdown => ProjectDataExportFormat.Markdown,
            _ => ProjectDataExportFormat.Backup
        };
    }
}
