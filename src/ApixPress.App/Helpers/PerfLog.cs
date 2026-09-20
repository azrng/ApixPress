using System.Collections.Concurrent;
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

    /// <summary>进程退出时给后台写盘线程的最多等待时间：丢几条诊断日志可接受，不能拖慢退出。</summary>
    private static readonly TimeSpan ExitFlushTimeout = TimeSpan.FromMilliseconds(500);

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>待写盘队列：UI 线程只做入队（微秒级），磁盘 IO 全部由后台线程承担。</summary>
    private static readonly BlockingCollection<string> PendingLines = new(new ConcurrentQueue<string>());

    private static readonly Thread DrainThread;

    static PerfLog()
    {
        DrainThread = new Thread(WritePendingLines)
        {
            IsBackground = true,
            Name = "perflog-drain",
        };
        DrainThread.Start();
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    public static void Log(string operation, long elapsedMilliseconds, string? detail = null)
    {
        try
        {
            var slowMark = elapsedMilliseconds >= SlowThresholdMs ? "[SLOW] " : string.Empty;
            var line = string.Create(CultureInfo.InvariantCulture,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {slowMark}{operation} {elapsedMilliseconds}ms{(string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail}")}\n");
            PendingLines.Add(line);
        }
        catch
        {
            // 耗时日志失败不能影响业务：吞掉（含退出阶段 CompleteAdding 后的入队）
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

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        try
        {
            PendingLines.CompleteAdding();
            DrainThread.Join(ExitFlushTimeout);
        }
        catch
        {
            // 退出冲刷失败静默：诊断日志不阻塞进程退出
        }
    }

    /// <summary>后台单写线程：串行消费队列写盘，天然保序且免锁；单条写失败只丢该条，不能让线程退出。</summary>
    private static void WritePendingLines()
    {
        foreach (var line in PendingLines.GetConsumingEnumerable())
        {
            try
            {
                File.AppendAllText(
                    Path.Combine(CrashLog.GetLogDirectory(), $"perf-{DateTime.Now:yyyyMMdd}.log"),
                    line,
                    Utf8NoBom);
            }
            catch
            {
                // 消费线程绝不能把异常抛出去终止进程，也不能因单条失败停止消费：吞掉继续
            }
        }
    }
}
