using ApixPress.App.Helpers;

namespace ApixPress.App.Tests.Helpers;

public class PerfLogTests
{
    [Fact]
    public void Log_Should_WriteLineViaBackgroundDrainThread()
    {
        // 入队后由后台线程落盘：用唯一标记轮询等待，验证队列→磁盘链路真实生效
        var marker = $"perflog-冒烟-{Guid.NewGuid():N}";
        var logPath = Path.Combine(CrashLog.GetLogDirectory(), $"perf-{DateTime.Now:yyyyMMdd}.log");

        PerfLog.Log(marker, 123, "后台写盘测试");

        var found = false;
        var deadline = Environment.TickCount64 + 3000;
        while (Environment.TickCount64 < deadline)
        {
            if (File.Exists(logPath) && ReadShared(logPath).Contains(marker, StringComparison.Ordinal))
            {
                found = true;
                break;
            }

            Thread.Sleep(50);
        }

        Assert.True(found, "后台写盘线程应在 3 秒内把入队日志落盘");
    }

    [Fact]
    public void Log_AfterDrainCompletes_ShouldNotThrow()
    {
        // 异常路径兜底：任何情况下 Log 都不能把异常抛给调用方
        PerfLog.Log("perflog-异常兜底", -1);
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
