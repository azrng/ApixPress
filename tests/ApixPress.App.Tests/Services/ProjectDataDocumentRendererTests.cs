using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Implementations;

namespace ApixPress.App.Tests.Services;

public sealed class ProjectDataDocumentRendererTests
{
    private static ProjectDataDocumentModelDto CreateModel(params ProjectDataDocumentInterfaceDto[] interfaces)
    {
        return new ProjectDataDocumentModelDto
        {
            ProjectName = "订单项目",
            ProjectDescription = "演示项目",
            ExportedAt = new DateTime(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc),
            Interfaces = interfaces.ToList()
        };
    }

    private static ProjectDataDocumentInterfaceDto CreateInterface(
        string folderPath,
        string name,
        string method,
        string url,
        params ProjectDataDocumentCaseDto[] cases)
    {
        return new ProjectDataDocumentInterfaceDto
        {
            FolderPath = folderPath,
            Name = name,
            Description = $"{name}说明",
            Snapshot = new RequestSnapshotDto
            {
                Method = method,
                Url = url,
                QueryParameters =
                [
                    new RequestKeyValueDto { Name = "page", Value = "1", IsEnabled = true },
                    new RequestKeyValueDto { Name = "disabled", Value = "x", IsEnabled = false }
                ],
                BodyMode = BodyModes.RawJson,
                BodyContent = "{ \"id\": 1 }"
            },
            Cases = cases.ToList()
        };
    }

    [Fact]
    public void HtmlRenderer_ShouldRenderTitleTocAndEscapedContent()
    {
        var model = CreateModel(CreateInterface("订单", "查询<script>订单", "GET", "/orders?x=1"));

        var html = new ProjectDataHtmlRenderer().Render(model);

        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("订单项目", html);
        Assert.Contains("查询&lt;script&gt;订单", html);
        Assert.Contains("/orders?x=1", html);
        Assert.Contains("查询参数", html);
        Assert.Contains("page", html);
        // 禁用参数不进入文档
        Assert.DoesNotContain("disabled", html);
        // 请求体经 HTML 转义，引号变为 &quot;
        Assert.Contains("{ &quot;id&quot;: 1 }", html);
    }

    [Fact]
    public void HtmlRenderer_ShouldShowEmptyStateWithoutInterfaces()
    {
        var html = new ProjectDataHtmlRenderer().Render(CreateModel());

        Assert.Contains("当前项目暂无接口。", html);
        Assert.DoesNotContain("api-0", html);
    }

    [Fact]
    public void MarkdownRenderer_ShouldWriteIndexAndPerFolderFiles()
    {
        var model = CreateModel(
            CreateInterface("订单", "查询订单", "GET", "/orders"),
            CreateInterface("订单", "创建订单", "POST", "/orders", new ProjectDataDocumentCaseDto
            {
                Name = "成功",
                Snapshot = new RequestSnapshotDto { Method = "POST", Url = "/orders" }
            }),
            CreateInterface("", "根接口", "GET", "/root"));

        var files = new ProjectDataMarkdownRenderer().Render(model);

        Assert.Equal(3, files.Count);
        Assert.Equal("README.md", files[0].RelativePath);
        Assert.Equal("订单.md", files[1].RelativePath);
        Assert.Equal("根目录.md", files[2].RelativePath);

        var index = files[0].Content;
        Assert.Contains("接口目录", index);
        Assert.Contains("(订单.md)", index);
        Assert.Contains("(根目录.md)", index);

        var orderFile = files[1].Content;
        Assert.Contains("## GET /orders · 查询订单", orderFile);
        Assert.Contains("## POST /orders · 创建订单", orderFile);
        Assert.Contains("#### 成功", orderFile);
        Assert.Contains("```json", orderFile);
        // 排序稳定：查询订单（GET）排在创建订单（POST）前
        Assert.True(orderFile.IndexOf("## GET /orders", StringComparison.Ordinal)
            < orderFile.IndexOf("## POST /orders", StringComparison.Ordinal));
    }

    [Fact]
    public void MarkdownRenderer_ShouldSanitizeFolderNamesAndDeduplicate()
    {
        var model = CreateModel(
            CreateInterface("一级/二级", "接口A", "GET", "/a"),
            CreateInterface("一级-二级", "接口B", "GET", "/b"));

        var files = new ProjectDataMarkdownRenderer().Render(model);

        // 目录路径拼接与字符清理撞名时追加数字后缀，保持文件名唯一
        Assert.Contains(files, file => file.RelativePath == "一级-二级.md");
        Assert.Contains(files, file => file.RelativePath == "一级-二级-2.md");
    }

    [Fact]
    public void MarkdownRenderer_ShouldWriteEmptyIndexWhenNoInterfaces()
    {
        var files = new ProjectDataMarkdownRenderer().Render(CreateModel());

        var file = Assert.Single(files);
        Assert.Equal("README.md", file.RelativePath);
        Assert.Contains("当前项目暂无接口。", file.Content);
    }
}
