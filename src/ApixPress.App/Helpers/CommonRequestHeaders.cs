namespace ApixPress.App.Helpers;

/// <summary>
/// Header 名内置候选（T124）：常用 HTTP 请求头静态清单，供 Headers 页签 Header 名输入下拉选择。
/// 只做名称提示，不限制自由输入；清单与原型 04 的 builtinHeaderKeys 保持一致。
/// </summary>
public static class CommonRequestHeaders
{
    public static IReadOnlyList<string> Names { get; } =
    [
        "Accept",
        "Accept-Charset",
        "Accept-Encoding",
        "Accept-Language",
        "Authorization",
        "Cache-Control",
        "Connection",
        "Content-Disposition",
        "Content-Encoding",
        "Content-Language",
        "Content-Length",
        "Content-Type",
        "Cookie",
        "Date",
        "Expect",
        "Host",
        "If-Match",
        "If-Modified-Since",
        "If-None-Match",
        "If-Unmodified-Since",
        "Origin",
        "Pragma",
        "Proxy-Authorization",
        "Range",
        "Referer",
        "User-Agent",
        "X-Api-Key",
        "X-Correlation-Id",
        "X-Forwarded-For",
        "X-Request-Id",
        "X-Requested-With"
    ];
}
