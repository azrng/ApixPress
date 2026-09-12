using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace ApixPress.TestApi;

/// <summary>行为控制端点：状态码、延迟、重定向、流式与二进制下载等真实网络场景。</summary>
internal static class ControlEndpoints
{
    public static void Map(WebApplication app)
    {
        // 返回指定状态码；418 返回彩蛋响应体，便于区分"空状态码"与"带响应体"两种场景
        app.MapMethods("/status/{code:int}", ["GET", "POST", "PUT", "PATCH", "DELETE"], (int code) =>
        {
            if (code is < 200 or > 599)
            {
                return Results.Problem(statusCode: 400, title: "状态码超出范围", detail: "仅支持 200~599");
            }

            return code == 418
                ? Results.Content("-=[ teapot ]=-\n\n我是茶壶，短而胖。", "text/plain; charset=utf-8", statusCode: 418)
                : Results.StatusCode(code);
        });

        app.MapMethods("/delay/{seconds:double}", ["GET", "POST", "PUT", "PATCH", "DELETE"],
            async (double seconds, HttpContext context) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(seconds, 0, 60)), context.RequestAborted);
                return Results.Json(await EchoEndpoints.BuildEchoAsync(context));
            });

        MapRedirect(app, "/redirect/{n:int}", relative: true);
        MapRedirect(app, "/relative-redirect/{n:int}", relative: true);
        MapRedirect(app, "/absolute-redirect/{n:int}", relative: false);

        app.MapGet("/redirect-to", (HttpContext context) =>
        {
            var url = context.Request.Query["url"].ToString();
            if (string.IsNullOrWhiteSpace(url))
            {
                return Results.Problem(statusCode: 400, title: "缺少 url 参数",
                    detail: "示例：/redirect-to?url=/get 或完整地址");
            }

            return Redirect(context, url, ParseRedirectStatus(context));
        });

        app.MapGet("/unstable", async (HttpContext context) =>
        {
            var rate = context.Request.Query.TryGetValue("rate", out var raw)
                ? double.TryParse(raw, out var parsed) ? parsed : 0.5 : 0.5;
            if (Random.Shared.NextDouble() < Math.Clamp(rate, 0, 1))
            {
                await Task.Delay(50, context.RequestAborted);
                return Results.Problem(statusCode: 500, title: "不稳定端点随机失败",
                    detail: $"本次按 rate={Math.Clamp(rate, 0, 1)} 随机失败，可调低后重试");
            }

            return Results.Json(new { success = true, rate, at = DateTime.UtcNow });
        });

        app.MapGet("/cache", (HttpContext context) =>
        {
            // ETag 值需带双引号，与客户端回传的 If-None-Match 原始字符串比较
            const string etag = "\"apixpress-cache-v1\"";
            if (context.Request.Headers.IfNoneMatch.ToString().Contains("apixpress-cache-v1"))
            {
                return Results.StatusCode(304);
            }

            context.Response.Headers.ETag = etag;
            context.Response.Headers.LastModified = DateTimeOffset.UtcNow.ToString("R");
            return Results.Json(new
            {
                message = "首次请求返回 200 与 ETag；携带 If-None-Match 再次请求将返回 304"
            });
        });

        app.MapGet("/stream/{n:int}", async (int n, HttpContext context) =>
        {
            var response = context.Response;
            response.StatusCode = 200;
            response.ContentType = "application/x-ndjson; charset=utf-8";
            var count = Math.Clamp(n, 1, 100);
            for (var i = 1; i <= count; i++)
            {
                var line = JsonSerializer.Serialize(new
                {
                    id = i,
                    total = count,
                    method = context.Request.Method,
                    url = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}",
                    at = DateTime.UtcNow
                });
                await response.WriteAsync(line + "\n", context.RequestAborted);
                await response.Body.FlushAsync(context.RequestAborted);
                await Task.Delay(25, context.RequestAborted);
            }
        });

        app.MapGet("/bytes/{n:int}", (int n) => Results.Bytes(RandomBytes(n, seed: null), "application/octet-stream"));
        app.MapGet("/bytes/{n:int}/{seed:int}", (int n, int seed)
            => Results.Bytes(RandomBytes(n, seed), "application/octet-stream"));

        app.MapGet("/image/png", () => Results.File(PngImage.CreateRgb(96, 96), "image/png"));
        app.MapGet("/image/gif", () => Results.Bytes(
            Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"), "image/gif"));
        app.MapGet("/image/svg", () => Results.Text(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120">
              <rect width="120" height="120" rx="16" fill="#2563eb"/>
              <text x="60" y="70" font-size="28" text-anchor="middle" fill="#fff" font-family="sans-serif">API</text>
            </svg>
            """, "image/svg+xml"));

        app.MapGet("/links/{n:int}", (int n) =>
        {
            var count = Math.Clamp(n, 1, 20);
            var builder = new StringBuilder();
            builder.AppendLine("<html><head><meta charset=\"utf-8\"><title>Links</title></head><body>");
            for (var i = 1; i <= count; i++)
            {
                builder.AppendLine($"<p><a href=\"/links/{count}\">链接 {i} / 首页</a></p>");
            }

            builder.AppendLine("</body></html>");
            return Results.Text(builder.ToString(), "text/html; charset=utf-8");
        });

        app.MapGet("/sse", async (HttpContext context) =>
        {
            var count = context.Request.Query.TryGetValue("count", out var rawCount)
                ? int.TryParse(rawCount, out var parsedCount) ? parsedCount : 5 : 5;
            var interval = context.Request.Query.TryGetValue("interval", out var rawInterval)
                ? int.TryParse(rawInterval, out var parsedInterval) ? parsedInterval : 200 : 200;
            count = Math.Clamp(count, 1, 20);
            interval = Math.Clamp(interval, 10, 5000);

            var response = context.Response;
            response.StatusCode = 200;
            response.ContentType = "text/event-stream";
            for (var i = 1; i <= count; i++)
            {
                await response.WriteAsync($"id: {i}\nevent: message\ndata: {{\"seq\":{i},\"at\":\"{DateTimeOffset.UtcNow:o}\"}}\n\n",
                    context.RequestAborted);
                await response.Body.FlushAsync(context.RequestAborted);
                await Task.Delay(interval, context.RequestAborted);
            }

            await response.WriteAsync("event: done\ndata: [done]\n\n", context.RequestAborted);
            await response.Body.FlushAsync(context.RequestAborted);
        });
    }

    private static void MapRedirect(WebApplication app, string pattern, bool relative)
    {
        app.MapGet(pattern, (int n, HttpContext context) =>
        {
            var status = ParseRedirectStatus(context);
            if (n <= 0)
            {
                return Redirect(context, "/get", status);
            }

            var target = relative
                ? $"{pattern.Replace("{n:int}", (n - 1).ToString())}"
                : $"{context.Request.Scheme}://{context.Request.Host}{pattern.Replace("{n:int}", (n - 1).ToString())}";
            return Redirect(context, target, status);
        });
    }

    private static IResult Redirect(HttpContext context, string url, int status)
    {
        context.Response.StatusCode = status;
        context.Response.Headers.Location = url;
        return Results.Empty;
    }

    // 仅允许 301/302/303/307/308，其余回退到 302
    private static int ParseRedirectStatus(HttpContext context)
    {
        var raw = context.Request.Query["status_code"].ToString();
        return int.TryParse(raw, out var status) && status is 301 or 302 or 303 or 307 or 308 ? status : 302;
    }

    private static byte[] RandomBytes(int count, int? seed)
    {
        var total = Math.Clamp(count, 1, 10 * 1024 * 1024);
        var buffer = new byte[total];
        (seed.HasValue ? new Random(seed.Value) : Random.Shared).NextBytes(buffer);
        return buffer;
    }
}
