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
        }));

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
        });

        app.MapGet("/cookies/delete", (HttpContext context) =>
        {
            foreach (var key in context.Request.Query.Keys)
            {
                context.Response.Cookies.Delete(key, new CookieOptions { Path = "/" });
            }

            return Redirect(context, "/cookies");
        });

        app.MapMethods("/basic-auth/{user}/{password}", AnyMethods, (string user, string password, HttpContext context)
            => TryBasicAuth(context, user, password, hidden: false));

        app.MapMethods("/hidden-basic-auth/{user}/{password}", AnyMethods, (string user, string password, HttpContext context)
            => TryBasicAuth(context, user, password, hidden: true));

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
        });
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
