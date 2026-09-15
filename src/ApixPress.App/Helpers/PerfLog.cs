using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace ApixPress.App.Helpers;

/// <summary>
/// 轻量操作耗时日志：定位界面卡顿用，按日滚动写入
/// %LOCALAPPDATA%\ApixPress\logs\perf-yyyyMMdd.log。
/// 与 CrashLog 同款约束：只依赖 BCL、线程安全、写日志自身失败时静默。
/// 超过 <see cref="SlowThresholdMs"/> 的记录带 [SLOW] 标记，便于直接 grep。
/// </summary>
internal static class PerfLog
{
    /// <summary>超过该毫秒数视为慢操作，追加 [SLOW] 标记。</summary>
    private const int SlowThresholdMs = 200;

    private static readonly object Gate = new();

    public static void Log(string operation, long elapsedMilliseconds, string? detail = null)
    {
        try
        {
            var slowMark = elapsedMilliseconds >= SlowThresholdMs ? "[SLOW] " : string.Empty;
            var line = string.Create(CultureInfo.InvariantCulture,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {slowMark}{operation} {elapsedMilliseconds}ms{(string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail}")}\n");
            lock (Gate)
            {
                File.AppendAllText(
                    Path.Combine(CrashLog.GetLogDirectory(), $"perf-{DateTime.Now:yyyyMMdd}.log"),
                    line,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch
        {
            // 耗时日志失败不能影响业务：吞掉
        }
    }

    public static void Measure(string operation, Action action)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            action();
        }
        finally
        {
            Log(operation, stopwatch.ElapsedMilliseconds);
        }
    }

    public static T Measure<T>(string operation, Func<T> func)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return func();
        }
        finally
        {
            Log(operation, stopwatch.ElapsedMilliseconds);
        }
    }

    public static async Task MeasureAsync(string operation, Func<Task> action)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            Log(operation, stopwatch.ElapsedMilliseconds);
        }
    }

    public static async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            Log(operation, stopwatch.ElapsedMilliseconds);
        }
    }
}
