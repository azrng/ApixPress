using System.Net;
using System.Text;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.DependencyInjection;

namespace ApixPress.App.Services.Implementations;

/// <summary>生成自包含静态 HTML 接口文档：内嵌样式、按目录分组、接口定义与请求用例示例。</summary>
public sealed class ProjectDataHtmlRenderer : IProjectDataHtmlRenderer, ITransientDependency
{
    public string Render(ProjectDataDocumentModelDto model)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"zh-CN\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\" />");
        builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        builder.Append("<title>").Append(Escape(model.ProjectName)).AppendLine(" · 接口文档</title>");
        builder.AppendLine("""
                           <style>
                             :root { color-scheme: light; }
                             body { font-family: "Segoe UI", "Microsoft YaHei", sans-serif; margin: 0; background: #f6f7f9; color: #1f2328; }
                             .page { max-width: 960px; margin: 0 auto; padding: 32px 24px 64px; }
                             h1 { font-size: 24px; margin: 0 0 4px; }
                             h2 { font-size: 18px; margin: 32px 0 8px; border-bottom: 1px solid #d8dee4; padding-bottom: 6px; }
                             h3 { font-size: 15px; margin: 20px 0 6px; }
                             .meta { color: #59636e; font-size: 13px; margin-bottom: 24px; }
                             .toc { background: #fff; border: 1px solid #d8dee4; border-radius: 8px; padding: 14px 18px; margin-bottom: 24px; }
                             .toc a { color: #0969da; text-decoration: none; font-size: 13px; line-height: 1.9; }
                             .card { background: #fff; border: 1px solid #d8dee4; border-radius: 8px; padding: 16px 20px; margin-bottom: 16px; }
                             .badge { display: inline-block; font-size: 12px; font-weight: 600; border-radius: 4px; padding: 2px 8px; margin-right: 8px; color: #fff; background: #59636e; }
                             .badge.get { background: #1a7f37; }
                             .badge.post { background: #0969da; }
                             .badge.put, .badge.patch { background: #9a6700; }
                             .badge.delete { background: #cf222e; }
                             .path { font-family: Consolas, monospace; font-size: 13px; }
                             table { border-collapse: collapse; width: 100%; margin: 8px 0 12px; font-size: 13px; }
                             th, td { border: 1px solid #d8dee4; text-align: left; padding: 6px 10px; vertical-align: top; }
                             th { background: #f6f7f9; }
                             pre { background: #f6f7f9; border: 1px solid #d8dee4; border-radius: 6px; padding: 12px; overflow: auto; font-size: 12px; line-height: 1.6; }
                             .empty { color: #59636e; }
                             .case { border-left: 3px solid #d8dee4; padding-left: 12px; margin: 12px 0; }
                             .case-title { font-weight: 600; font-size: 13px; margin-bottom: 4px; }
                             .note { color: #59636e; font-size: 12px; }
                           </style>
                           """);
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("<div class=\"page\">");
        builder.Append("<h1>").Append(Escape(model.ProjectName)).AppendLine("</h1>");
        builder.AppendLine("<div class=\"meta\">");
        builder.Append("导出时间：").Append(model.ExportedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        if (!string.IsNullOrWhiteSpace(model.ProjectDescription))
        {
            builder.Append(" · ").Append(Escape(model.ProjectDescription));
        }
        builder.AppendLine("</div>");

        if (model.Interfaces.Count == 0)
        {
            builder.AppendLine("<p class=\"empty\">当前项目暂无接口。</p>");
        }
        else
        {
            AppendTableOfContents(builder, model);
            var sectionIndex = 0;
            foreach (var group in GroupByFolder(model))
            {
                builder.Append("<h2>").Append(Escape(group.Key)).AppendLine("</h2>");
                foreach (var item in group)
                {
                    AppendInterface(builder, item, $"api-{sectionIndex++}");
                }
            }
        }

        builder.AppendLine("<p class=\"note\">本文档由 ApixPress 生成，仅供阅读与复制参考；请求用例的可恢复导入请使用请求用例交付包。</p>");
        builder.AppendLine("</div>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    private static void AppendTableOfContents(StringBuilder builder, ProjectDataDocumentModelDto model)
    {
        builder.AppendLine("<div class=\"toc\">");
        var sectionIndex = 0;
        foreach (var group in GroupByFolder(model))
        {
            builder.Append("<div><strong>").Append(Escape(group.Key)).AppendLine("</strong></div>");
            foreach (var item in group)
            {
                builder.Append("<div>　<a href=\"#api-").Append(sectionIndex++).Append("\">");
                builder.Append("<span class=\"path\">").Append(Escape(item.Snapshot.Method.ToUpperInvariant())).Append(' ').Append(Escape(item.Snapshot.Url));
                builder.Append("</span> · ").Append(Escape(item.Name));
                builder.AppendLine("</a></div>");
            }
        }

        builder.AppendLine("</div>");
    }

    private static void AppendInterface(StringBuilder builder, ProjectDataDocumentInterfaceDto item, string anchor)
    {
        builder.AppendLine("<div class=\"card\">");
        builder.Append("<h3 id=\"").Append(anchor).Append("\">");
        builder.Append(MethodBadge(item.Snapshot.Method));
        builder.Append("<span class=\"path\">").Append(Escape(item.Snapshot.Method.ToUpperInvariant())).Append(' ').Append(Escape(item.Snapshot.Url));
        builder.Append("</span> · ").Append(Escape(item.Name));
        builder.AppendLine("</h3>");
        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            builder.Append("<p>").Append(Escape(item.Description)).AppendLine("</p>");
        }

        AppendParametersTable(builder, "查询参数", item.Snapshot.QueryParameters);
        AppendParametersTable(builder, "路径参数", item.Snapshot.PathParameters);
        AppendParametersTable(builder, "请求头", item.Snapshot.Headers);
        AppendBody(builder, item.Snapshot);

        foreach (var testCase in item.Cases)
        {
            builder.AppendLine("<div class=\"case\">");
            builder.Append("<div class=\"case-title\">请求用例示例：").Append(Escape(testCase.Name)).AppendLine("</div>");
            if (!string.IsNullOrWhiteSpace(testCase.Description))
            {
                builder.Append("<p class=\"note\">").Append(Escape(testCase.Description)).AppendLine("</p>");
            }

            builder.Append("<div class=\"path\">").Append(Escape(testCase.Snapshot.Method.ToUpperInvariant())).Append(' ').Append(Escape(testCase.Snapshot.Url)).AppendLine("</div>");
            AppendParametersTable(builder, "查询参数", testCase.Snapshot.QueryParameters);
            AppendParametersTable(builder, "路径参数", testCase.Snapshot.PathParameters);
            AppendParametersTable(builder, "请求头", testCase.Snapshot.Headers);
            AppendBody(builder, testCase.Snapshot);
            builder.AppendLine("</div>");
        }

        builder.AppendLine("</div>");
    }

    private static void AppendParametersTable(StringBuilder builder, string title, List<RequestKeyValueDto> parameters)
    {
        var enabled = parameters.Where(item => item.IsEnabled).ToList();
        if (enabled.Count == 0)
        {
            return;
        }

        builder.Append("<table><thead><tr><th>").Append(title).AppendLine("</th><th>值</th></tr></thead><tbody>");
        foreach (var parameter in enabled)
        {
            builder.Append("<tr><td>").Append(Escape(parameter.Name));
            builder.Append("</td><td>").Append(Escape(string.IsNullOrWhiteSpace(parameter.Value) ? "-" : parameter.Value));
            builder.AppendLine("</td></tr>");
        }

        builder.AppendLine("</tbody></table>");
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
                    builder.Append("<pre>").Append(Escape(snapshot.BodyContent)).AppendLine("</pre>");
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
                builder.AppendLine("<p class=\"note\">二进制请求体不在文档中展示。</p>");
                break;
        }
    }

    private static string MethodBadge(string method)
    {
        var normalized = method.Trim().ToUpperInvariant();
        var cssClass = normalized switch
        {
            "GET" => "get",
            "POST" => "post",
            "PUT" or "PATCH" => "put",
            "DELETE" => "delete",
            _ => ""
        };
        return $"<span class=\"badge {cssClass}\">{WebUtility.HtmlEncode(normalized)}</span>";
    }

    private static IEnumerable<IGrouping<string, ProjectDataDocumentInterfaceDto>> GroupByFolder(ProjectDataDocumentModelDto model)
    {
        return model.Interfaces.GroupBy(item => string.IsNullOrWhiteSpace(item.FolderPath) ? "根目录" : item.FolderPath);
    }

    private static string Escape(string? text)
    {
        return WebUtility.HtmlEncode(text ?? string.Empty);
    }
}
