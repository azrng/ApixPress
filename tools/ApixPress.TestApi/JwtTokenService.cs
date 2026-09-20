using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ApixPress.TestApi;

/// <summary>
/// HS256 JWT 签发与校验。仅用于本地联调：密钥固定、用户内置，参数都写进 README。
/// 不引入 JWT 依赖包，方便精确控制"过期/错签/错签发者"等测试场景。
/// </summary>
internal static class JwtTokenService
{
    public const string DefaultIssuer = "apixpress-testapi";
    public const string DefaultAudience = "apixpress-client";
    public const string DefaultSecret = "apixpress-test-secret-key-0123456789";
    public const int DefaultExpiresInSeconds = 3600;

    /// <summary>内置登录用户：(用户名, 密码, 角色, 显示名)。</summary>
    public static readonly (string Username, string Password, string Role, string Name)[] Users =
    [
        ("admin", "admin123", "admin", "管理员"),
        ("user", "123456", "user", "测试用户")
    ];

    public static string CreateToken(
        string subject,
        string? name,
        string? role,
        DateTimeOffset expiresAt,
        string? issuer = null,
        string? audience = null,
        string? secret = null,
        string tokenUse = "access")
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = new Dictionary<string, object?>
        {
            ["sub"] = subject,
            ["name"] = name,
            ["role"] = role,
            ["token_use"] = tokenUse,
            ["iss"] = issuer ?? DefaultIssuer,
            ["aud"] = audience ?? DefaultAudience,
            ["iat"] = now,
            ["exp"] = expiresAt.ToUnixTimeSeconds(),
            ["jti"] = Guid.NewGuid().ToString()
        };

        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var payloadPart = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signature = Base64UrlEncode(Sign($"{header}.{payloadPart}", secret ?? DefaultSecret));
        return $"{header}.{payloadPart}.{signature}";
    }

    public static JwtValidateResult Validate(string token, string? secret = null, string tokenUse = "access")
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return Fail("malformed_token", "令牌应包含 3 段以点分隔的 Base64Url");
        }

        JsonElement header;
        try
        {
            using var headerDoc = JsonDocument.Parse(DecodeUtf8(Base64UrlDecode(parts[0])));
            header = headerDoc.RootElement.Clone();
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return Fail("malformed_token", "令牌头不是合法的 Base64Url JSON");
        }

        if (header.TryGetProperty("alg", out var alg) && alg.GetString() != "HS256")
        {
            return Fail("unsupported_algorithm", "本测试服务仅支持 HS256");
        }

        // 先验签再信任负载
        byte[] expected;
        try
        {
            expected = Sign($"{parts[0]}.{parts[1]}", secret ?? DefaultSecret);
        }
        catch (CryptographicException)
        {
            return Fail("invalid_signature", "签名计算失败");
        }

        byte[] actual;
        try
        {
            actual = Base64UrlDecode(parts[2]);
        }
        catch (FormatException)
        {
            return Fail("malformed_token", "签名字段不是合法的 Base64Url");
        }

        if (!expected.AsSpan().SequenceEqual(actual))
        {
            return Fail("invalid_signature", "签名与密钥不匹配（令牌被篡改或使用了其他密钥）");
        }

        JsonElement payload;
        try
        {
            using var payloadDoc = JsonDocument.Parse(DecodeUtf8(Base64UrlDecode(parts[1])));
            payload = payloadDoc.RootElement.Clone();
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return Fail("malformed_token", "令牌负载不是合法的 Base64Url JSON");
        }

        if (payload.TryGetProperty("exp", out var exp) &&
            exp.ValueKind == JsonValueKind.Number &&
            exp.GetInt64() < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            return Fail("token_expired", $"令牌已于 {DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64()):O} 过期");
        }

        if (payload.TryGetProperty("iss", out var iss) && iss.GetString() != DefaultIssuer)
        {
            return Fail("invalid_issuer", $"签发者应为 {DefaultIssuer}，实际为 {iss.GetString()}");
        }

        if (payload.TryGetProperty("aud", out var aud) && aud.GetString() != DefaultAudience)
        {
            return Fail("invalid_audience", $"受众应为 {DefaultAudience}，实际为 {aud.GetString()}");
        }

        if (payload.TryGetProperty("token_use", out var use) && use.GetString() != tokenUse)
        {
            return Fail("wrong_token_type", $"该端点要求 {tokenUse} 类型令牌，实际为 {use.GetString()}");
        }

        return new JwtValidateResult(true, null, null, CloneClaims(payload));
    }

    /// <summary>把负载克隆为独立字典，避免依赖已释放的 JsonDocument。</summary>
    private static Dictionary<string, JsonElement> CloneClaims(JsonElement payload)
    {
        var claims = new Dictionary<string, JsonElement>();
        foreach (var property in payload.EnumerateObject())
        {
            claims[property.Name] = property.Value.Clone();
        }

        return claims;
    }

    private static byte[] Sign(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return hmac.ComputeHash(Encoding.ASCII.GetBytes(data));
    }

    private static JwtValidateResult Fail(string code, string description)
        => new(false, code, description, new Dictionary<string, JsonElement>());

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty
        };
        return Convert.FromBase64String(padded);
    }

    private static string DecodeUtf8(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}

/// <summary>JWT 校验结果：Ok 为 false 时 ErrorCode / ErrorDescription 说明失败原因。</summary>
internal sealed record JwtValidateResult(
    bool Ok,
    string? ErrorCode,
    string? ErrorDescription,
    Dictionary<string, JsonElement> Payload);
