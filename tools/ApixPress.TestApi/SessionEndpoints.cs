using System.Text;

namespace ApixPress.TestApi;

/// <summary>Cookie 与认证端点：验证客户端的 Cookie 自动携带与 Authorization 头处理。</summary>
internal static class SessionEndpoints
{
    private static readonly string[] AnyMethods = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    public static void Map(WebApplication app)
    {
        app.MapGet("/cookies", (HttpContext context) => Results.Json(new
        {
            cookies = context.Request.Cookies.ToDictionary(kv => kv.Key, kv => kv.Value)
        })).WithTags("Cookie 与认证")
            .WithSummary("查看当前 Cookie")
            .WithDescription("以键值对返回请求携带的全部 Cookie");

        app.MapGet("/cookies/set", (HttpContext context) =>
        {
            // 逐个查询参数写入 Cookie，随后跳回 /cookies 便于客户端展示
            foreach (var (key, value) in context.Request.Query)
            {
                context.Response.Cookies.Append(key, value.ToString(), new CookieOptions
                {
                    Path = "/",
                    HttpOnly = false
                });
            }

            return Redirect(context, "/cookies");
        }).WithTags("Cookie 与认证")
            .WithSummary("写入 Cookie")
            .WithDescription("每个查询参数写入一个 Cookie 后 302 跳回 /cookies");

        app.MapGet("/cookies/delete", (HttpContext context) =>
        {
            foreach (var key in context.Request.Query.Keys)
            {
                context.Response.Cookies.Delete(key, new CookieOptions { Path = "/" });
            }

            return Redirect(context, "/cookies");
        }).WithTags("Cookie 与认证")
            .WithSummary("删除 Cookie")
            .WithDescription("按查询参数中的键删除 Cookie 后 302 跳回 /cookies");

        app.MapMethods("/basic-auth/{user}/{password}", AnyMethods, (string user, string password, HttpContext context)
            => TryBasicAuth(context, user, password, hidden: false)).WithTags("Cookie 与认证")
            .WithSummary("Basic 认证校验")
            .WithDescription("路径参数 user/password 与 Authorization: Basic 头解码结果一致时返回 200，否则 401");

        app.MapMethods("/hidden-basic-auth/{user}/{password}", AnyMethods, (string user, string password, HttpContext context)
            => TryBasicAuth(context, user, password, hidden: true)).WithTags("Cookie 与认证")
            .WithSummary("隐藏式 Basic 认证")
            .WithDescription("认证失败时返回 404 而非 401，避免暴露端点存在");

        app.MapMethods("/bearer", AnyMethods, (HttpContext context) =>
        {
            var authorization = context.Request.Headers.Authorization.ToString();
            if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) &&
                authorization["Bearer ".Length..].Trim().Length > 0)
            {
                return Results.Json(new
                {
                    authenticated = true,
                    token = authorization["Bearer ".Length..].Trim()
                });
            }

            context.Response.Headers.WWWAuthenticate = "Bearer";
            return Results.Json(new { authenticated = false, message = "缺少 Bearer Token" }, statusCode: 401);
        }).WithTags("Cookie 与认证")
            .WithSummary("Bearer Token 认证校验")
            .WithDescription("仅校验 Authorization: Bearer 头格式（不校验令牌内容），缺失返回 401");

        app.MapMethods("/apikey", AnyMethods, (HttpContext context) =>
        {
            // 自定义请求头认证场景，约定测试密钥 apixpress-dev-key
            var key = context.Request.Headers["X-Api-Key"].ToString();
            if (string.IsNullOrEmpty(key))
            {
                return Results.Json(new
                {
                    authenticated = false,
                    error = "missing_api_key",
                    error_description = "请在请求头中携带 X-Api-Key"
                }, statusCode: 401);
            }

            if (key != "apixpress-dev-key")
            {
                return Results.Json(new
                {
                    authenticated = false,
                    error = "invalid_api_key",
                    error_description = "X-Api-Key 不正确，测试密钥为 apixpress-dev-key"
                }, statusCode: 401);
            }

            return Results.Json(new { authenticated = true, key_name = "default" });
        }).WithTags("Cookie 与认证")
            .WithSummary("API Key 请求头认证")
            .WithDescription("要求请求头 X-Api-Key: apixpress-dev-key，缺失或不匹配返回 401");
    }

    private static IResult TryBasicAuth(HttpContext context, string user, string password, bool hidden)
    {
        var expected = $"{user}:{password}";
        var actual = ParseBasicCredentials(context.Request.Headers.Authorization.ToString());

        if (string.Equals(actual, expected, StringComparison.Ordinal))
        {
            return Results.Json(new
            {
                authenticated = true,
                user
            });
        }

        if (hidden)
        {
            // 隐藏式 Basic 认证失败时不返回 401，避免暴露端点存在
            return Results.Json(new { authenticated = false }, statusCode: 404);
        }

        context.Response.Headers.WWWAuthenticate = "Basic realm=\"ApixPress\"";
        return Results.Json(new { authenticated = false, message = "用户名或密码错误" }, statusCode: 401);
    }

    /// <summary>解析 Basic 认证头为 "user:password"，失败时返回空字符串。</summary>
    private static string ParseBasicCredentials(string authorization)
    {
        if (!authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorization["Basic ".Length..].Trim()));
            return decoded;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    private static IResult Redirect(HttpContext context, string url)
    {
        context.Response.StatusCode = 302;
        context.Response.Headers.Location = url;
        return Results.Empty;
    }
}
