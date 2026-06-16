using System.Xml.Linq;

namespace ApixPress.App.Tests.Views;

public sealed class WorkbenchLayoutTests
{
    [Fact]
    public void MainWindow_ProjectWorkspaceHost_ShouldStretchGeneratedContainers()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "MainWindow.axaml"));
        var host = document.Descendants()
            .Single(element => element.Name.LocalName == "ItemsControl"
                && HasClass(element, "ProjectWorkspaceHost"));

        Assert.Equal("Stretch", host.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Stretch", host.Attribute("VerticalAlignment")?.Value);

        var presenterStyle = host.Descendants()
            .SingleOrDefault(element => element.Name.LocalName == "Style"
                && element.Attribute("Selector")?.Value == "ItemsControl.ProjectWorkspaceHost ContentPresenter");

        Assert.NotNull(presenterStyle);
        Assert.Contains(
            presenterStyle!.Elements(),
            element => element.Name.LocalName == "Setter"
                && element.Attribute("Property")?.Value == "HorizontalAlignment"
                && element.Attribute("Value")?.Value == "Stretch");
        Assert.Contains(
            presenterStyle.Elements(),
            element => element.Name.LocalName == "Setter"
                && element.Attribute("Property")?.Value == "VerticalAlignment"
                && element.Attribute("Value")?.Value == "Stretch");
    }

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
        Assert.Contains(
            document.Descendants(),
            element => element.Name.LocalName == "Border"
                && HasClass(element, "InterfaceRootAuthWorkbench"));
        Assert.DoesNotContain(
            document.Descendants(),
            element => element.Name.LocalName == "Border"
                && HasClass(element, "ProjectWorkspaceSettingsCard")
                && HasClass(element, "InterfaceRootAuthPanel"));
        Assert.DoesNotContain(
            document.Descendants(),
            element => element.Name.LocalName == "Border"
                && HasClass(element, "InterfaceRootAllPanel"));

        var styles = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Assets", "Styles", "WorkspaceEditorStyles.axaml"));
        var authPanelStyle = styles.Descendants()
            .SingleOrDefault(element => element.Name.LocalName == "Style"
                && element.Attribute("Selector")?.Value == "Border.InterfaceRootAuthPanel");

        Assert.Null(authPanelStyle);
        Assert.Contains(
            styles.Descendants(),
            element => element.Name.LocalName == "Style"
                && element.Attribute("Selector")?.Value == "Border.InterfaceRootAuthWorkbench");
    }

    [Fact]
    public void ProjectWorkspace_ShouldStretchLoadingHostAndMainGrid()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "ProjectWorkspaceView.axaml"));
        var loadingContainer = document.Descendants()
            .Single(element => element.Name.LocalName == "LoadingContainer");
        var mainGrid = loadingContainer.Elements()
            .Single(element => element.Name.LocalName == "Grid");

        Assert.Equal("Stretch", loadingContainer.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Stretch", loadingContainer.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("Stretch", loadingContainer.Attribute("HorizontalContentAlignment")?.Value);
        Assert.Equal("Stretch", loadingContainer.Attribute("VerticalContentAlignment")?.Value);
        Assert.Equal("Stretch", mainGrid.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Stretch", mainGrid.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("0", mainGrid.Attribute("MinWidth")?.Value);
        Assert.Equal("0", mainGrid.Attribute("MinHeight")?.Value);
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
