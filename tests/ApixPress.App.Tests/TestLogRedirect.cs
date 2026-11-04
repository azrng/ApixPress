using System.Runtime.CompilerServices;

namespace ApixPress.App.Tests;

/// <summary>
/// 装配初始化：把 crash/perf 日志重定向到 TEMP 独立目录。
/// CrashLog/PerfLog 默认写真实用户 %LOCALAPPDATA%\ApixPress\logs，
/// 单测（崩溃冒烟、PerfLog 落盘、首页列表加载埋点等）会污染真实日志，故装配期改向。
/// ModuleInitializer 先于程序集内一切静态构造执行，PerfLog 后台线程逐条现取目录，重定向全程生效。
/// </summary>
internal static class TestLogRedirect
{
    internal const string LogDirectoryEnvironmentVariable = "APIXPRESS_LOG_DIR";

    [ModuleInitializer]
    internal static void RedirectLogDirectory()
    {
        var logDirectory = Path.Combine(Path.GetTempPath(), "ApixPress.Tests", "logs");
        Directory.CreateDirectory(logDirectory);
        Environment.SetEnvironmentVariable(LogDirectoryEnvironmentVariable, logDirectory);
    }
}
