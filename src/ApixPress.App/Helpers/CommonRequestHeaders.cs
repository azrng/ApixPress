namespace ApixPress.App.Helpers;

/// <summary>
/// Header 名内置候选（T124，T128 精简）：常用 HTTP 请求头静态清单，供 Headers 页签 Header 名输入下拉选择。
/// 只保留调试请求时真正常用的一项（如鉴权、内容类型、Cookie），Host、Connection、If-* 等低频项不内置，可自由输入。
/// 只做名称提示，不限制自由输入；清单与原型 04 的下拉候选保持一致。
/// </summary>
public static class CommonRequestHeaders
{
    public static IReadOnlyList<string> Names { get; } =
    [
        "Accept",
        "Accept-Encoding",
        "Accept-Language",
        "Authorization",
        "Cache-Control",
        "Content-Length",
        "Content-Type",
        "Cookie",
        "Origin",
        "Referer",
        "User-Agent",
        "X-Api-Key",
        "X-Requested-With"
    ];
}
