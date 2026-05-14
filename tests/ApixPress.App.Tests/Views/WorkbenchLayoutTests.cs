using System.Xml.Linq;

namespace ApixPress.App.Tests.Views;

public sealed class WorkbenchLayoutTests
{
    [Theory]
    [InlineData("HttpInterfaceWorkbenchView.axaml")]
    [InlineData("QuickRequestWorkbenchView.axaml")]
    public void WorkbenchSplitter_ShouldResizeOnlyConfigAndResponseRows(string fileName)
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", fileName));
        var splitter = document.Descendants()
            .Single(element => element.Name.LocalName == "GridSplitter"
                && HasClass(element, "HttpWorkbenchSplitter"));

        var splitterHost = splitter.Parent;

        Assert.NotNull(splitterHost);
        Assert.Equal("Grid", splitterHost!.Name.LocalName);
        var rowDefinitions = splitterHost.Elements()
            .Single(element => element.Name.LocalName == "Grid.RowDefinitions")
            .Elements()
            .Where(element => element.Name.LocalName == "RowDefinition")
            .ToArray();

        Assert.Equal(3, rowDefinitions.Length);
        Assert.Equal("Auto", rowDefinitions[0].Attribute("Height")?.Value);
        Assert.Equal("118", rowDefinitions[0].Attribute("MinHeight")?.Value);
        Assert.Equal("{Binding ConfigTab.ConfigPanelMaxHeight}", rowDefinitions[0].Attribute("MaxHeight")?.Value);
        Assert.Equal("6", rowDefinitions[1].Attribute("Height")?.Value);
        Assert.Equal("*", rowDefinitions[2].Attribute("Height")?.Value);
        Assert.DoesNotContain(
            splitterHost.Descendants(),
            element => HasClass(element, "HttpDesignMetaCard"));
    }

    [Fact]
    public void InterfaceRootWorkspace_ShouldUseFullWidthWorkbenchTable()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "ProjectInterfaceRootWorkspaceView.axaml"));

        var rootLayout = document.Root?.Elements().SingleOrDefault();
        Assert.NotNull(rootLayout);
        Assert.Equal("Grid", rootLayout!.Name.LocalName);
        Assert.Equal("Auto,*", rootLayout.Attribute("RowDefinitions")?.Value);
        Assert.Equal("{StaticResource Space.1}", rootLayout.Attribute("RowSpacing")?.Value);
        Assert.DoesNotContain(
            (rootLayout.Attribute("Classes")?.Value ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries),
            className => className is "PanelCard" or "ProjectWorkspaceCanvas");

        Assert.Contains(
            document.Descendants(),
            element => element.Name.LocalName == "Border"
                && HasClass(element, "InterfaceRootWorkbenchSurface"));
        Assert.DoesNotContain(
            document.Descendants(),
            element => element.Name.LocalName == "Border"
                && HasClass(element, "InterfaceRootAllPanel"));
    }

    private static bool HasClass(XElement element, string className)
    {
        return (element.Attribute("Classes")?.Value ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(className);
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
