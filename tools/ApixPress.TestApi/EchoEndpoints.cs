using System.Security.Cryptography;
using System.Text.Json;

namespace ApixPress.TestApi;

/// <summary>请求回显端点：把客户端实际发出的方法、参数、头、Cookie 与请求体原样回显。</summary>
internal static class EchoEndpoints
{
    // 各回显端点同时接受全部常用方法，方便验证客户端真实发送的方法与请求体
    private static readonly string[] AllMethods = ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"];

    public static void Map(WebApplication app)
    {
        // 注意：这里的处理器统一使用块体 lambda。.NET 10 下 MapMethods 搭配
        // "async (...) => Results.Json(...)" 表达式体时响应体会丢失（实测 200 空响应），
        // 块体写法则一切正常。
        // summary 按方法区分（如 "PUT /get 回显请求详情"）：此前 MapMethods 全方法共享
        // 一份 summary，ApixPress 导入后同一路径的 7 个接口全部同名。
        foreach (var verb in new[] { "get", "post", "put", "patch", "delete" })
        {
            var pattern = $"/{verb}";
            app.MapPerMethod(pattern, AllMethods, async (HttpContext context) =>
            {
                return Results.Json(await BuildEchoAsync(context));
            }, "回显类", method => $"{method} {pattern} 回显请求详情",
                "原样回显真实请求方法、query、headers、cookies、请求体与 JSON/表单解析结果");
        }

        app.MapPerMethod("/anything", AllMethods, async (HttpContext context) =>
        {
            return Results.Json(await BuildEchoAsync(context));
        }, "回显类", method => $"{method} /anything 回显请求详情",
            "回显内容与 /get 一致");
        app.MapPerMethod("/anything/{**path}", AllMethods, async (HttpContext context) =>
        {
            return Results.Json(await BuildEchoAsync(context));
        }, "回显类", method => $"{method} /anything/{{path}} 回显请求详情",
            "通配 /anything/ 后的任意子路径，回显内容与 /get 一致");

        app.MapPerMethod("/headers", AllMethods, (HttpContext context) => Results.Json(new
        {
            headers = context.Request.Headers.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray())
        }), "回显类", method => $"{method} /headers 返回请求头集合",
            "以键值对形式返回客户端发送的全部请求头");

        app.MapGet("/ip", (HttpContext context) => Results.Json(new
        {
            origin = context.Connection.RemoteIpAddress?.ToString()
        })).WithTags("回显类")
            .WithSummary("返回客户端 IP")
            .WithDescription("origin 字段为连接的远程 IP 地址");

        app.MapGet("/user-agent", (HttpContext context) => Results.Json(new
        {
            userAgent = context.Request.Headers.UserAgent.ToString()
        })).WithTags("回显类")
            .WithSummary("返回 User-Agent")
            .WithDescription("返回请求头 User-Agent 的原始值");

        app.MapGet("/uuid", () => Results.Json(new
        {
            uuid = Guid.NewGuid()
        })).WithTags("回显类")
            .WithSummary("返回随机 GUID")
            .WithDescription("每次请求生成新的 UUID v4");
    }

    /// <summary>构建统一回显载荷：表单与文件、原始请求体、JSON 解析结果互斥处理。</summary>
    internal static async Task<object> BuildEchoAsync(HttpContext context)
    {
        var request = context.Request;
        var rawBody = string.Empty;
        JsonElement? json = null;
        Dictionary<string, string>? form = null;
        List<object>? files = null;

        if (request.HasFormContentType)
        {
            var formCollection = await request.ReadFormAsync(context.RequestAborted);
            form = formCollection.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
            files = [];
            foreach (var file in formCollection.Files)
            {
                using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer, context.RequestAborted);
                files.Add(new
                {
                    field = file.Name,
                    fileName = file.FileName,
                    contentType = file.ContentType,
                    size = file.Length,
                    sha256 = Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant()
                });
            }
        }
        else
        {
            using var reader = new StreamReader(request.Body);
            rawBody = await reader.ReadToEndAsync(context.RequestAborted);
            if (!string.IsNullOrWhiteSpace(rawBody))
            {
                try
                {
                    json = JsonSerializer.Deserialize<JsonElement>(rawBody);
                }
                catch (JsonException)
                {
                    // 非 JSON 请求体不做解析，保留原始内容回显
                }
            }
        }

        return new
        {
            method = request.Method,
            scheme = request.Scheme,
            host = request.Host.Value,
            path = request.Path.Value,
            url = $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}",
            query = request.Query.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()),
            headers = request.Headers.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()),
            cookies = request.Cookies.ToDictionary(kv => kv.Key, kv => kv.Value),
            origin = context.Connection.RemoteIpAddress?.ToString(),
            body = rawBody,
            json,
            form,
            files
        };
    }
}
