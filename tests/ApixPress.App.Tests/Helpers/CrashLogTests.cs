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
}
