using System.Text;

namespace ApixPress.App.Helpers;

public static class UiFormatHelper
{
    public static string FormatBytes(long sizeBytes)
    {
        var units = new[] { "B", "KB", "MB", "GB" };
        double current = sizeBytes;
        var index = 0;

        while (current >= 1024 && index < units.Length - 1)
        {
            current /= 1024;
            index++;
        }

        return $"{current:0.##} {units[index]}";
    }

    public static string HeadersToText(IEnumerable<KeyValuePair<string, string>> headers)
    {
        var builder = new StringBuilder();
        foreach (var header in headers)
        {
            builder.AppendLine($"{header.Key}: {header.Value}");
        }

        return builder.ToString().Trim();
    }
}
