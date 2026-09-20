namespace ApixPress.TestApi;

/// <summary>
/// OpenAPI 元数据辅助：MapMethods 一次挂多个 HTTP 方法时，summary/tags/description
/// 由该路径的全部操作共享，ApixPress 等按 summary 命名导入端点的工具会得到成排同名接口。
/// 这里改为逐方法注册，summary 由调用方按方法生成，保证每个操作名称唯一可辨。
/// </summary>
internal static class PerMethodRegistration
{
    /// <summary>按方法逐个注册同一路径；handler 必须是内联块体 lambda（.NET 10 下
    /// 方法组或表达式体会退回运行时 RequestDelegateFactory，返回 200 空响应）。</summary>
    public static void MapPerMethod(
        this WebApplication app,
        string pattern,
        string[] methods,
        Delegate handler,
        string tag,
        Func<string, string> summaryFactory,
        string description)
    {
        foreach (var method in methods)
        {
            app.MapMethods(pattern, [method], handler)
                .WithTags(tag)
                .WithSummary(summaryFactory(method))
                .WithDescription(description);
        }
    }
}
