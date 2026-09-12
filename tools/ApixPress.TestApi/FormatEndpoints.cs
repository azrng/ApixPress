using System.IO.Compression;
using System.Text;

namespace ApixPress.TestApi;

/// <summary>响应格式端点：覆盖 JSON、XML、HTML、纯文本、UTF-8、压缩编码与请求体解析。</summary>
internal static class FormatEndpoints
{
    private static readonly string[] WriteMethods = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    public static void Map(WebApplication app)
    {
        app.MapGet("/json", () => Results.Json(new
        {
            message = "ApixPress 本地测试 JSON",
            code = 0,
            data = new
            {
                tags = new[] { "json", "测试", "中文" },
                count = 3,
                nested = new { enabled = true, ratio = 0.75, items = new[] { 1, 2, 3 } }
            },
            updatedAt = DateTime.UtcNow
        }));

        app.MapGet("/xml", () => Results.Text(SampleXml, "application/xml"));

        app.MapGet("/html", () => Results.Text(SampleHtml, "text/html; charset=utf-8"));

        app.MapGet("/text", (HttpContext context) =>
        {
            var lines = Math.Clamp(context.Request.Query.TryGetValue("lines", out var raw)
                ? int.TryParse(raw, out var parsed) ? parsed : 10 : 10, 1, 1000);
            var builder = new StringBuilder();
            for (var i = 1; i <= lines; i++)
            {
                builder.AppendLine($"第 {i} 行：这是用于 ApixPress 响应预览的纯文本内容 plain text sample {i}.");
            }

            return Results.Text(builder.ToString(), "text/plain; charset=utf-8");
        });

        app.MapGet("/encoding/utf8", () => Results.Text(SampleUtf8, "text/plain; charset=utf-8"));

        app.MapGet("/gzip", async (HttpContext context) =>
        {
            var payload = Encoding.UTF8.GetBytes(SampleJsonForCompression);
            using var compressed = new MemoryStream();
            await using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            {
                await gzip.WriteAsync(payload);
            }

            context.Response.Headers.ContentEncoding = "gzip";
            return Results.Bytes(compressed.ToArray(), "application/json");
        });

        app.MapGet("/brotli", async (HttpContext context) =>
        {
            var payload = Encoding.UTF8.GetBytes(SampleJsonForCompression);
            using var compressed = new MemoryStream();
            await using (var brotli = new BrotliStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            {
                await brotli.WriteAsync(payload);
            }

            context.Response.Headers.ContentEncoding = "br";
            return Results.Bytes(compressed.ToArray(), "application/json");
        });

        app.MapGet("/problem/{code:int}", (int code) => Results.Problem(
            statusCode: Math.Clamp(code, 200, 599),
            title: "ApixPress 测试错误",
            detail: $"这是状态码 {code} 的 ProblemDetails 错误响应"));

        app.MapMethods("/forms/post", WriteMethods, async (HttpContext context) =>
        {
            if (!context.Request.HasFormContentType)
            {
                return Results.Problem(statusCode: 400, title: "请求体不是表单",
                    detail: "请使用 Content-Type: application/x-www-form-urlencoded");
            }

            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            return Results.Json(new
            {
                method = context.Request.Method,
                form = form.ToDictionary(kv => kv.Key, kv => kv.Value.ToString())
            });
        });

        app.MapMethods("/upload", WriteMethods, async (HttpContext context) =>
        {
            return Results.Json(await EchoEndpoints.BuildEchoAsync(context));
        });

        app.MapMethods("/binary-echo", WriteMethods, async (HttpContext context) =>
        {
            using var buffer = new MemoryStream();
            await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);
            return Results.Bytes(buffer.ToArray(), context.Request.ContentType ?? "application/octet-stream");
        });
    }

    private const string SampleJsonForCompression = """
        {"message":"这是 gzip/brotli 压缩响应测试数据","items":[1,2,3],"chinese":"压缩内容解码后应显示正常中文","tags":["gzip","brotli","compression"]}
        """;

    private const string SampleXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <response>
          <message>ApixPress 本地测试 XML</message>
          <items>
            <item id="1">中文内容一</item>
            <item id="2">中文内容二</item>
          </items>
          <nested>
            <enabled>true</enabled>
            <ratio>0.75</ratio>
          </nested>
        </response>
        """;

    private const string SampleHtml = """
        <!DOCTYPE html>
        <html lang="zh-CN">
        <head>
          <meta charset="utf-8">
          <title>ApixPress 测试页面</title>
        </head>
        <body>
          <h1>ApixPress 本地测试 HTML</h1>
          <p>这个页面用于验证 HTML 响应渲染，包含<a href="/">链接</a>与 <strong>加粗文本</strong>。</p>
          <ul>
            <li>列表项一</li>
            <li>列表项二</li>
          </ul>
        </body>
        </html>
        """;

    private const string SampleUtf8 = """
        UTF-8 编码测试文本 / Encoding sample:
        简体中文：API 调试工具需要正确显示多字节字符
        繁體中文：介面文字與訊息提示
        日本語：テスト用のサンプルテキスト
        한국어: 테스트용 샘플 텍스트
        Emoji：✅ ❌ 🚀 🔥 📦 😀 👍
        拉丁与重音：á é í ó ú ü ñ ç à è ì ò
        西里尔：Привет мир
        阿拉伯：مرحبا بالعالم
        特殊符号：© ® ™ ± × ÷ ° € ¥ £ ① ② ③
        """;
}
