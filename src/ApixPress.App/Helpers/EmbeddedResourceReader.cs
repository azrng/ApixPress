using System.Reflection;
using System.Text;

namespace ApixPress.App.Helpers;

public static class EmbeddedResourceReader
{
    public static string ReadRequiredText(Assembly assembly, string resourceNameSuffix)
    {
        using var stream = OpenRequiredStream(assembly, resourceNameSuffix);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public static Stream OpenRequiredStream(Assembly assembly, string resourceNameSuffix)
    {
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(resourceNameSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new FileNotFoundException($"未找到内嵌资源：{resourceNameSuffix}");
        }

        return assembly.GetManifestResourceStream(resourceName)
               ?? throw new FileNotFoundException($"无法打开内嵌资源流：{resourceNameSuffix}");
    }
}
