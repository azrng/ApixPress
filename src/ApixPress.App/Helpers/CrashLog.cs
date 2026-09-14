using System.Globalization;
using System.Text;

namespace ApixPress.App.Helpers;

/// <summary>
/// 最简崩溃日志：三类全局异常挂点共用，按日滚动写入
/// %LOCALAPPDATA%\ApixPress\logs\crash-yyyyMMdd.log。
/// 只依赖 BCL，线程安全；写日志自身失败时静默，绝不二次抛异常。
/// </summary>
internal static class CrashLog
{
    private static readonly object Gate = new();

    public static void Write(Exception? exception, string source)
    {
        if (exception is null)
        {
            return;
        }

        try
        {
            var line = string.Create(CultureInfo.InvariantCulture,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{source}] {exception.GetType().FullName}: {exception.Message}\n{exception.StackTrace}\n\n");
            lock (Gate)
            {
                File.AppendAllText(
                    Path.Combine(GetLogDirectory(), $"crash-{DateTime.Now:yyyyMMdd}.log"),
                    line,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch
        {
            // 崩溃日志失败不能再抛：吞掉，避免干扰原有异常传播
        }
    }

    public static string GetLogDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ApixPress",
        "logs");
}
