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
        foreach (var verb in new[] { "get", "post", "put", "patch", "delete" })
        {
            app.MapMethods($"/{verb}", AllMethods, async (HttpContext context) =>
            {
                return Results.Json(await BuildEchoAsync(context));
            });
        }

        app.MapMethods("/anything", AllMethods, async (HttpContext context) =>
        {
            return Results.Json(await BuildEchoAsync(context));
        });
        app.MapMethods("/anything/{**path}", AllMethods, async (HttpContext context) =>
        {
            return Results.Json(await BuildEchoAsync(context));
        });

        app.MapMethods("/headers", AllMethods, (HttpContext context) => Results.Json(new
        {
            headers = context.Request.Headers.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray())
        }));

        app.MapGet("/ip", (HttpContext context) => Results.Json(new
        {
            origin = context.Connection.RemoteIpAddress?.ToString()
        }));

        app.MapGet("/user-agent", (HttpContext context) => Results.Json(new
        {
            userAgent = context.Request.Headers.UserAgent.ToString()
        }));

        app.MapGet("/uuid", () => Results.Json(new
        {
            uuid = Guid.NewGuid()
        }));
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
