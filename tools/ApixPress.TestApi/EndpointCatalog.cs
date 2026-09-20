namespace ApixPress.TestApi;

/// <summary>端点目录条目，同时作为根路径 JSON 目录的数据来源。</summary>
internal sealed record EndpointItem(string Method, string Path, string Description);

/// <summary>全部端点目录，与各 Endpoints 文件中注册的路由保持一致。</summary>
internal static class EndpointCatalog
{
    public static readonly IReadOnlyList<EndpointItem> Items =
    [
        // 回显类
        new("ANY", "/get /post /put /patch /delete", "任意 HTTP 方法回显请求详情"),
        new("ANY", "/anything", "任意路径与方法回显，支持 /anything/{**path}"),
        new("ANY", "/headers", "返回请求头集合"),
        new("GET", "/ip", "返回客户端 IP"),
        new("GET", "/user-agent", "返回 User-Agent"),
        new("GET", "/uuid", "返回新生成的 GUID"),

        // 请求体格式
        new("ANY", "/forms/post", "解析 x-www-form-urlencoded 并回显字段"),
        new("ANY", "/upload", "解析 multipart/form-data，回显字段与文件摘要（含 SHA-256）"),
        new("ANY", "/binary-echo", "按原始字节回显请求体（二进制上传）"),

        // 响应格式
        new("GET", "/json", "返回示例 JSON"),
        new("GET", "/xml", "返回示例 XML"),
        new("GET", "/html", "返回示例 HTML"),
        new("GET", "/text?lines=10", "返回纯文本，可指定行数"),
        new("GET", "/encoding/utf8", "返回多语言与 Emoji 的 UTF-8 文本"),
        new("GET", "/gzip", "返回 gzip 压缩的 JSON"),
        new("GET", "/brotli", "返回 brotli 压缩的 JSON"),
        new("GET", "/problem/{code}", "返回 RFC 9457 ProblemDetails 错误"),

        // 状态与控制
        new("ANY", "/status/{code}", "返回指定状态码（418 有彩蛋响应体）"),
        new("ANY", "/delay/{seconds}", "延迟指定秒数（0~60）后回显"),
        new("GET", "/redirect/{n}?status_code=302", "连续重定向 n 次后落到 /get"),
        new("GET", "/relative-redirect/{n}", "相对地址重定向"),
        new("GET", "/absolute-redirect/{n}", "绝对地址重定向"),
        new("GET", "/redirect-to?url=", "重定向到指定地址"),
        new("GET", "/unstable?rate=0.5", "按比例随机返回 500，用于重试测试"),
        new("GET", "/cache", "带 ETag 的响应，命中 If-None-Match 时返回 304"),
        new("GET", "/stream/{n}", "分块流式返回 n 行 JSON"),
        new("GET", "/bytes/{n}[/{seed}]", "返回 n 字节随机二进制（可带随机种子）"),
        new("GET", "/image/png | /image/gif | /image/svg", "返回示例图片"),
        new("GET", "/links/{n}", "返回包含 n 个链接的 HTML 页面"),
        new("GET", "/sse?count=5&interval=200", "Server-Sent Events 推送"),

        // Cookie 与认证
        new("GET", "/cookies", "查看当前 Cookie"),
        new("GET", "/cookies/set?k=v", "写入 Cookie 后跳回 /cookies"),
        new("GET", "/cookies/delete?k=", "删除 Cookie 后跳回 /cookies"),
        new("ANY", "/basic-auth/{user}/{password}", "HTTP Basic 认证校验"),
        new("ANY", "/hidden-basic-auth/{user}/{password}", "Basic 认证失败时返回 404"),
        new("ANY", "/bearer", "Bearer Token 认证校验（仅校验头格式）"),
        new("ANY", "/apikey", "X-Api-Key 请求头认证（测试密钥 apixpress-dev-key）"),

        // JWT 认证
        new("ANY", "/jwt/login", "登录发放 JWT（admin/admin123、user/123456，支持 JSON/表单/查询参数）"),
        new("ANY", "/jwt/protected", "JWT 受保护端点：校验签名、有效期、签发者与受众"),
        new("ANY", "/jwt/admin-only", "要求 role=admin，权限不足返回 403"),
        new("ANY", "/jwt/refresh", "用 refresh_token 换取新的令牌对"),
        new("GET", "/jwt/decode?token=", "无校验解码 JWT，便于联调时检查客户端发出的令牌"),
        new("GET", "/jwt/mint?sub=&role=&expires_in=", "按需铸造测试令牌（可指定过期时间/角色/签发者/受众/密钥）"),

        // 文档
        new("GET", "/openapi/v1.json", "内置 OpenAPI 文档，供 Swagger URL 导入测试"),
        new("GET", "/", "本端点目录")
    ];
}
