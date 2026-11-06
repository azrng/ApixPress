using System.Xml.Linq;
using ApixPress.App.Services.Implementations;

namespace ApixPress.App.Tests.Services;

public sealed class AccentPaletteServiceTests
{
    [Fact]
    public void AccentTheme_ShouldRoundTripThroughSettingsFile()
    {
        // AccentTheme 字段走 CloneSettings/JSON 序列化两条链路，漏一处即丢字段（T123）
        var dto = new ApixPress.App.Models.DTOs.AppShellSettingsDto { AccentTheme = "purple" };
        var clone = typeof(AppShellSettingsService)
            .GetMethod("CloneSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [dto]) as ApixPress.App.Models.DTOs.AppShellSettingsDto;

        Assert.Equal("purple", clone!.AccentTheme);
    }

    [Fact]
    public void ColorsAxaml_TealPreset_ShouldStayInSyncWithDefaultBrushValues()
    {
        // teal 预设是 Colors.axaml 的默认值镜像；Colors 改默认色时必须同步预设，否则切换后回不到原色
        var colorsFile = FindSourceFile("src", "ApixPress.App", "Assets", "Styles", "Colors.axaml");
        var document = XDocument.Load(colorsFile);
        string? ResolveValue(string key)
        {
            var xKey = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");
            return document.Descendants()
                .SingleOrDefault(element => element.Attribute(xKey)?.Value == key)?
                .Attribute("Color")?.Value;
        }

        Assert.Equal("#44E7D4", ResolveValue("Brush.Brand.Primary"));
        Assert.Equal("#22C7B8", ResolveValue("Brush.Brand.Primary.Strong"));
        Assert.Equal("#6BF0E2", ResolveValue("Brush.Brand.Hover"));
        Assert.Equal("#08201E", ResolveValue("Brush.Text.OnBrand"));
        Assert.Equal("#44E7D4", ResolveValue("Brush.Border.Focus"));
        Assert.Equal("#44E7D4", ResolveValue("Brush.Text.Link"));
        Assert.Equal("#3D44E7D4", ResolveValue("Brush.Focus.Ring"));
    }

    private static string FindSourceFile(params string[] pathParts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine([current.FullName, .. pathParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Could not find source file: {Path.Combine(pathParts)}");
    }
}
