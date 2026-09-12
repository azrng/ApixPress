using System.Text.Json;

namespace ApixPress.TestApi;

/// <summary>
/// JWT 认证端点：登录发放令牌、受保护资源校验、刷新、按需铸造测试令牌与无校验解码，
/// 覆盖缺失/畸形/错签/过期/错误签发者/错误受众/权限不足等常见联调场景。
/// </summary>
internal static class JwtEndpoints
{
    private static readonly string[] AnyMethods = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    public static void Map(WebApplication app)
    {
        // 注意：.NET 10 实测发现，方法组或表达式体形式的 async 处理器会退回运行时
        // RequestDelegateFactory，返回 200 空响应体；只有内联块体 lambda 行为正常。
        // 因此所有端点统一用块体 lambda 包装私有方法。
        app.MapMethods("/jwt/login", AnyMethods, async (HttpContext context) =>
        {
            return await LoginAsync(context);
        });
        app.MapMethods("/jwt/protected", AnyMethods, async (HttpContext context) =>
        {
            return await ProtectedAsync(context);
        });
        app.MapMethods("/jwt/admin-only", AnyMethods, async (HttpContext context) =>
        {
            return await AdminOnlyAsync(context);
        });
        app.MapMethods("/jwt/refresh", AnyMethods, async (HttpContext context) =>
        {
            return await RefreshAsync(context);
        });
        app.MapGet("/jwt/decode", async (HttpContext context) =>
        {
            return await DecodeAsync(context);
        });
        app.MapGet("/jwt/mint", (HttpContext context) =>
        {
            return MintAsync(context);
        });
    }

    private static async Task<IResult> LoginAsync(HttpContext context)
    {
        var (username, password) = await ResolveUsernamePasswordAsync(context);
        var user = JwtTokenService.Users.FirstOrDefault(u =>
            u.Username == username && u.Password == password);
        if (user != default)
        {
            var now = DateTimeOffset.UtcNow;
            var access = JwtTokenService.CreateToken(user.Username, user.Name, user.Role,
                now.AddSeconds(JwtTokenService.DefaultExpiresInSeconds));
            var refresh = JwtTokenService.CreateToken(user.Username, user.Name, user.Role,
                now.AddDays(7), tokenUse: "refresh");
            return Results.Json(new
            {
                access_token = access,
                token_type = "Bearer",
                expires_in = JwtTokenService.DefaultExpiresInSeconds,
                refresh_token = refresh
            });
        }

        return Results.Json(new
        {
            error = "invalid_credentials",
            error_description = "用户名或密码错误，可用账号见 README：admin/admin123、user/123456"
        }, statusCode: 401);
    }

    private static async Task<IResult> ProtectedAsync(HttpContext context)
    {
        var validation = ValidateBearer(context);
        if (validation.Result != null)
        {
            return validation.Result;
        }

        return Results.Json(new
        {
            authenticated = true,
            message = "这是 JWT 受保护资源，令牌签名与有效期校验通过",
            claims = validation.Claims
        });
    }

    private static async Task<IResult> AdminOnlyAsync(HttpContext context)
    {
        var validation = ValidateBearer(context);
        if (validation.Result != null)
        {
            return validation.Result;
        }

        // 角色可能是字符串（登录发放）或数组（mint 指定），统一处理后判断
        var roles = FlattenRoles(validation.Claims);
        if (roles.Contains("admin"))
        {
            return Results.Json(new
            {
                authenticated = true,
                authorized = true,
                message = "管理员专属资源访问成功",
                roles
            });
        }

        return Results.Json(new
        {
            authenticated = true,
            authorized = false,
            error = "insufficient_role",
            error_description = "该资源要求 role=admin",
            roles
        }, statusCode: 403);
    }

    private static async Task<IResult> RefreshAsync(HttpContext context)
    {
        var refreshToken = await ResolveTokenParameterAsync(context, "refresh_token");
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Results.Json(new
            {
                error = "missing_refresh_token",
                error_description = "请通过 JSON/表单字段或查询参数 refresh_token 提供刷新令牌"
            }, statusCode: 400);
        }

        var validation = JwtTokenService.Validate(refreshToken, tokenUse: "refresh");
        if (!validation.Ok)
        {
            return UnauthorizedWithChallenge(context, validation.ErrorCode!, validation.ErrorDescription!);
        }

        validation.Payload.TryGetValue("sub", out var sub);
        validation.Payload.TryGetValue("name", out var name);
        validation.Payload.TryGetValue("role", out var role);
        var now = DateTimeOffset.UtcNow;
        var access = JwtTokenService.CreateToken(
            sub.ValueKind == JsonValueKind.String ? sub.GetString()! : "unknown",
            name.ValueKind == JsonValueKind.String ? name.GetString() : null,
            role.ValueKind == JsonValueKind.String ? role.GetString() : null,
            now.AddSeconds(JwtTokenService.DefaultExpiresInSeconds));
        var newRefresh = JwtTokenService.CreateToken(
            sub.ValueKind == JsonValueKind.String ? sub.GetString()! : "unknown",
            name.ValueKind == JsonValueKind.String ? name.GetString() : null,
            role.ValueKind == JsonValueKind.String ? role.GetString() : null,
            now.AddDays(7), tokenUse: "refresh");
        return Results.Json(new
        {
            access_token = access,
            token_type = "Bearer",
            expires_in = JwtTokenService.DefaultExpiresInSeconds,
            refresh_token = newRefresh
        });
    }

    private static async Task<IResult> DecodeAsync(HttpContext context)
    {
        var token = context.Request.Query["token"].ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.Json(new
            {
                error = "missing_token",
                error_description = "示例：/jwt/decode?token=eyJhbGciOi..."
            }, statusCode: 400);
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return Results.Json(new
            {
                error = "malformed_token",
                error_description = $"令牌包含 {parts.Length} 段，应为 3 段"
            }, statusCode: 400);
        }

        try
        {
            using var headerDoc = JsonDocument.Parse(DecodeSegment(parts[0]));
            using var payloadDoc = JsonDocument.Parse(DecodeSegment(parts[1]));
            var validation = JwtTokenService.Validate(token);
            var signatureValid = validation.Ok || validation.ErrorCode == "invalid_signature";
            var expired = payloadDoc.RootElement.TryGetProperty("exp", out var exp) &&
                          exp.ValueKind == JsonValueKind.Number &&
                          exp.GetInt64() < DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return Results.Json(new
            {
                header = headerDoc.RootElement.Clone(),
                payload = payloadDoc.RootElement.Clone(),
                signature_valid = signatureValid,
                expired
            });
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return Results.Json(new
            {
                error = "malformed_token",
                error_description = "头或负载不是合法的 Base64Url JSON"
            }, statusCode: 400);
        }
    }

    private static IResult MintAsync(HttpContext context)
    {
        var query = context.Request.Query;
        var expiresIn = long.TryParse(query["expires_in"], out var parsedExpires) ? parsedExpires : JwtTokenService.DefaultExpiresInSeconds;
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        var token = JwtTokenService.CreateToken(
            subject: query["sub"].FirstOrDefault() ?? "debug-user",
            name: query["name"].FirstOrDefault(),
            role: query["role"].FirstOrDefault() ?? "user",
            expiresAt: expiresAt,
            issuer: query["issuer"].FirstOrDefault(),
            audience: query["audience"].FirstOrDefault(),
            secret: query["secret"].FirstOrDefault(),
            tokenUse: query["token_use"].FirstOrDefault() ?? "access");
        return Results.Json(new
        {
            token,
            token_type = "Bearer",
            expires_in = expiresIn,
            expires_at = expiresAt,
            hint = "负数 expires_in 可生成已过期令牌；自定义 issuer/audience/secret 可生成校验失败的令牌"
        });
    }

    /// <summary>校验 Authorization: Bearer 头；成功返回声明，失败返回已写好 401 的结果。</summary>
    private static (IResult? Result, Dictionary<string, JsonElement> Claims) ValidateBearer(
        HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ||
            authorization["Bearer ".Length..].Trim().Length == 0)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
            return (Results.Json(new
            {
                authenticated = false,
                error = "missing_token",
                error_description = "请求头需包含 Authorization: Bearer <token>，可先调用 /jwt/login 获取"
            }, statusCode: 401), []);
        }

        var validation = JwtTokenService.Validate(authorization["Bearer ".Length..].Trim());
        if (!validation.Ok)
        {
            return (UnauthorizedWithChallenge(context, validation.ErrorCode!, validation.ErrorDescription!), []);
        }

        return (null, validation.Payload);
    }

    /// <summary>
    /// 401 响应带 WWW-Authenticate 挑战头与机器可读的错误码，便于客户端测试 401 处理。
    /// 头值只允许 ASCII，中文说明放在响应体里。
    /// </summary>
    private static IResult UnauthorizedWithChallenge(HttpContext context, string errorCode, string description)
    {
        context.Response.Headers.WWWAuthenticate = $"Bearer error=\"invalid_token\", error_description=\"{errorCode}\"";
        return Results.Json(new
        {
            authenticated = false,
            error = errorCode,
            error_description = description
        }, statusCode: 401, contentType: "application/json; charset=utf-8");
    }

    /// <summary>依次从查询参数、表单、JSON 请求体解析用户名与密码，便于各种客户端联调。</summary>
    private static async Task<(string Username, string Password)> ResolveUsernamePasswordAsync(
        HttpContext context)
    {
        var query = context.Request.Query;
        if (query.TryGetValue("username", out var queryUser) || query.TryGetValue("password", out _))
        {
            return (query["username"].ToString(), query["password"].ToString());
        }

        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            return (form["username"].ToString(), form["password"].ToString());
        }

        using var reader = new StreamReader(context.Request.Body);
        var raw = await reader.ReadToEndAsync(context.RequestAborted);
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var document = JsonDocument.Parse(raw);
                var username = document.RootElement.TryGetProperty("username", out var u) ? u.GetString() : null;
                var password = document.RootElement.TryGetProperty("password", out var p) ? p.GetString() : null;
                return (username ?? string.Empty, password ?? string.Empty);
            }
            catch (JsonException)
            {
                // 非 JSON 请求体按空凭据处理
            }
        }

        return (string.Empty, string.Empty);
    }

    private static async Task<string> ResolveTokenParameterAsync(HttpContext context, string parameterName)
    {
        var value = context.Request.Query[parameterName].ToString();
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            return form[parameterName].ToString();
        }

        using var reader = new StreamReader(context.Request.Body);
        var raw = await reader.ReadToEndAsync(context.RequestAborted);
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var document = JsonDocument.Parse(raw);
                return document.RootElement.TryGetProperty(parameterName, out var found)
                    ? found.GetString() ?? string.Empty
                    : string.Empty;
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }

        return string.Empty;
    }

    private static string[] FlattenRoles(Dictionary<string, JsonElement> claims)
    {
        if (!claims.TryGetValue("role", out var role))
        {
            return [];
        }

        return role.ValueKind switch
        {
            JsonValueKind.Array => role.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .ToArray(),
            JsonValueKind.String => [role.GetString()!],
            _ => []
        };
    }

    private static string DecodeSegment(string segment)
    {
        var padded = segment.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty
        };
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}
