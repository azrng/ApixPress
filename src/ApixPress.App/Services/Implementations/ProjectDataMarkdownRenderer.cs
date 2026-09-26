using System.Text;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.DependencyInjection;

namespace ApixPress.App.Services.Implementations;

/// <summary>生成 Markdown 文件计划：README 索引 + 每个接口目录一个 .md；文件名做路径字符清理并保持稳定排序。</summary>
public sealed class ProjectDataMarkdownRenderer : IProjectDataMarkdownRenderer, ITransientDependency
{
    private const string IndexFileName = "README.md";
    private const string RootFolderDisplayName = "根目录";

    public IReadOnlyList<ProjectDataMarkdownFileDto> Render(ProjectDataDocumentModelDto model)
    {
        var files = new List<ProjectDataMarkdownFileDto>();
        var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { IndexFileName };

        var folderGroups = model.Interfaces
            .GroupBy(item => string.IsNullOrWhiteSpace(item.FolderPath) ? RootFolderDisplayName : item.FolderPath)
            .ToList();

        // 先为每个目录分配稳定且不冲突的文件名，索引和正文共用同一份映射
        var fileNamesByFolder = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in folderGroups)
        {
            fileNamesByFolder[group.Key] = AllocateFileName(group.Key, usedFileNames);
        }

        files.Add(new ProjectDataMarkdownFileDto
        {
            RelativePath = IndexFileName,
            Content = BuildIndexContent(model, folderGroups, fileNamesByFolder)
        });

        foreach (var group in folderGroups)
        {
            files.Add(new ProjectDataMarkdownFileDto
            {
                RelativePath = fileNamesByFolder[group.Key],
                Content = BuildFolderContent(group.Key, group)
            });
        }

        return files;
    }

    private static string BuildIndexContent(
        ProjectDataDocumentModelDto model,
        List<IGrouping<string, ProjectDataDocumentInterfaceDto>> folderGroups,
        Dictionary<string, string> fileNamesByFolder)
    {
        var builder = new StringBuilder();
        builder.Append("# ").AppendLine(model.ProjectName);
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(model.ProjectDescription))
        {
            builder.AppendLine(model.ProjectDescription);
            builder.AppendLine();
        }

        builder.Append("> 导出时间：").AppendLine(model.ExportedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        builder.AppendLine();

        if (folderGroups.Count == 0)
        {
            builder.AppendLine("当前项目暂无接口。");
            return builder.ToString();
        }

        builder.AppendLine("## 接口目录");
        builder.AppendLine();
        foreach (var group in folderGroups)
        {
            builder.Append("- **").Append(group.Key).AppendLine("**");
            foreach (var item in group)
            {
                builder.Append("  - [").Append(item.Snapshot.Method.ToUpperInvariant()).Append(' ')
                    .Append(item.Snapshot.Url).Append(" · ").Append(item.Name)
                    .Append("](").Append(fileNamesByFolder[group.Key]).AppendLine(")");
            }
        }

        builder.AppendLine();
        builder.AppendLine("本文档由 ApixPress 生成，仅供阅读与复制参考；请求用例的可恢复导入请使用请求用例交付包。");
        return builder.ToString();
    }

    private static string BuildFolderContent(string folderName, IEnumerable<ProjectDataDocumentInterfaceDto> interfaces)
    {
        var builder = new StringBuilder();
        builder.Append("# ").AppendLine(folderName);
        builder.AppendLine();
        foreach (var item in interfaces)
        {
            AppendInterface(builder, item);
        }

        return builder.ToString();
    }

    private static void AppendInterface(StringBuilder builder, ProjectDataDocumentInterfaceDto item)
    {
        builder.Append("## ").Append(item.Snapshot.Method.ToUpperInvariant()).Append(' ').Append(item.Snapshot.Url)
            .Append(" · ").AppendLine(item.Name);
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            builder.AppendLine(item.Description);
            builder.AppendLine();
        }

        AppendParametersTable(builder, "查询参数", item.Snapshot.QueryParameters);
        AppendParametersTable(builder, "路径参数", item.Snapshot.PathParameters);
        AppendParametersTable(builder, "请求头", item.Snapshot.Headers);
        AppendBody(builder, item.Snapshot);

        if (item.Cases.Count > 0)
        {
            builder.AppendLine("### 请求用例示例");
            builder.AppendLine();
            foreach (var testCase in item.Cases)
            {
                builder.Append("#### ").AppendLine(testCase.Name);
                builder.AppendLine();
                if (!string.IsNullOrWhiteSpace(testCase.Description))
                {
                    builder.AppendLine(testCase.Description);
                    builder.AppendLine();
                }

                builder.Append("`").Append(testCase.Snapshot.Method.ToUpperInvariant()).Append(' ')
                    .Append(testCase.Snapshot.Url).AppendLine("`");
                builder.AppendLine();
                AppendParametersTable(builder, "查询参数", testCase.Snapshot.QueryParameters);
                AppendParametersTable(builder, "路径参数", testCase.Snapshot.PathParameters);
                AppendParametersTable(builder, "请求头", testCase.Snapshot.Headers);
                AppendBody(builder, testCase.Snapshot);
            }
        }
    }

    private static void AppendParametersTable(StringBuilder builder, string title, List<RequestKeyValueDto> parameters)
    {
        var enabled = parameters.Where(item => item.IsEnabled).ToList();
        if (enabled.Count == 0)
        {
            return;
        }

        builder.Append("**").Append(title).AppendLine("**");
        builder.AppendLine();
        builder.AppendLine("| 名称 | 值 |");
        builder.AppendLine("| --- | --- |");
        foreach (var parameter in enabled)
        {
            builder.Append("| ").Append(EscapeCell(parameter.Name))
                .Append(" | ").Append(EscapeCell(string.IsNullOrWhiteSpace(parameter.Value) ? "-" : parameter.Value))
                .AppendLine(" |");
        }

        builder.AppendLine();
    }

    private static void AppendBody(StringBuilder builder, RequestSnapshotDto snapshot)
    {
        switch (snapshot.BodyMode)
        {
            case BodyModes.RawJson:
            case BodyModes.RawXml:
            case BodyModes.RawText:
                if (!string.IsNullOrWhiteSpace(snapshot.BodyContent))
                {
                    var language = snapshot.BodyMode switch
                    {
                        BodyModes.RawJson => "json",
                        BodyModes.RawXml => "xml",
                        _ => "text"
                    };
                    builder.AppendLine($"```{language}");
                    builder.AppendLine(snapshot.BodyContent);
                    builder.AppendLine("```");
                    builder.AppendLine();
                }
                break;
            case BodyModes.FormData:
            case BodyModes.FormUrlEncoded:
                AppendParametersTable(builder, "表单字段", snapshot.FormFields.Select(field => new RequestKeyValueDto
                {
                    Name = field.Name,
                    Value = field.Value,
                    IsEnabled = field.IsEnabled
                }).ToList());
                break;
            case BodyModes.Binary:
                builder.AppendLine("二进制请求体不在文档中展示。");
                builder.AppendLine();
                break;
        }
    }

    private static string AllocateFileName(string folderName, HashSet<string> usedFileNames)
    {
        // 目录路径分段清理后用“-”拼接为单层文件名，避免嵌套目录；清理后为空时退回稳定展示名
        var segments = folderName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var baseName = string.Join("-", segments.Select(SanitizeFileName));
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = RootFolderDisplayName;
        }

        var candidate = $"{baseName}.md";
        var suffix = 2;
        while (!usedFileNames.Add(candidate))
        {
            candidate = $"{baseName}-{suffix++}.md";
        }

        return candidate;
    }

    private static string SanitizeFileName(string segment)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(segment.Select(character => invalidChars.Contains(character) ? '-' : character).ToArray()).Trim();
        return cleaned.Length == 0 ? string.Empty : cleaned;
    }

    private static string EscapeCell(string text)
    {
        return text.Replace("|", "\\|").ReplaceLineEndings(" ");
    }
}
