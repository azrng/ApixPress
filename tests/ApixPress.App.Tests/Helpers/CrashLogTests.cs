using ApixPress.App.Helpers;

namespace ApixPress.App.Tests.Helpers;

public class CrashLogTests
{
    [Fact]
    public void Write_Should_CreateDatedLogFileWithSourceAndException()
    {
        var logDir = CrashLog.GetLogDirectory();
        Directory.CreateDirectory(logDir);
        var stamp = DateTime.Now.ToString("yyyyMMdd");
        var logPath = Path.Combine(logDir, $"crash-{stamp}.log");
        var sizeBefore = File.Exists(logPath) ? new FileInfo(logPath).Length : 0;

        CrashLog.Write(new InvalidOperationException("崩溃日志冒烟"), "UnitTest");

        Assert.True(File.Exists(logPath), "崩溃日志文件应被创建");
        using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(sizeBefore, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);
        var appended = reader.ReadToEnd();

        Assert.Contains("[UnitTest]", appended);
        Assert.Contains("System.InvalidOperationException: 崩溃日志冒烟", appended);
    }

    [Fact]
    public void Write_WithNullException_ShouldNotThrow()
    {
        CrashLog.Write(null, "UnitTest");
    }

    [Fact]
    public void GetLogDirectory_WithEnvironmentOverride_ShouldReturnOverrideDirectory()
    {
        // 重定向 seam 是测试隔离的基础：环境变量存在时必须整体生效
        var original = Environment.GetEnvironmentVariable(TestLogRedirect.LogDirectoryEnvironmentVariable);
        var expected = Path.Combine(Path.GetTempPath(), $"apixpress-logdir-{Guid.NewGuid():N}");
        try
        {
            Environment.SetEnvironmentVariable(TestLogRedirect.LogDirectoryEnvironmentVariable, expected);
            Assert.Equal(expected, CrashLog.GetLogDirectory());
        }
        finally
        {
            // 还原装配期设好的重定向值，不能影响后续测试的日志隔离
            Environment.SetEnvironmentVariable(TestLogRedirect.LogDirectoryEnvironmentVariable, original);
        }
    }
}
