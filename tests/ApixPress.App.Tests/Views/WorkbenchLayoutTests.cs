using System.Xml.Linq;

namespace ApixPress.App.Tests.Views;

public sealed class WorkbenchLayoutTests
{
    [Fact]
    public void MainWindow_ProjectWorkspaceHost_ShouldRenderOnlyActiveProjectTab()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "MainWindow.axaml"));
        var host = document.Descendants()
            .Single(element => element.Name.LocalName == "ContentControl"
                && HasClass(element, "ProjectWorkspaceHost"));

        Assert.Equal("Stretch", host.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Stretch", host.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("{Binding ActiveProjectTab}", host.Attribute("Content")?.Value);
        Assert.Equal("{Binding HasActiveProjectTab}", host.Attribute("IsVisible")?.Value);

        var contentTemplate = host.Descendants()
            .Single(element => element.Name.LocalName == "ContentControl.ContentTemplate");
        var workspaceView = contentTemplate.Descendants()
            .Single(element => element.Name.LocalName == "ProjectWorkspaceView");

        Assert.Null(workspaceView.Attribute("IsVisible"));
        Assert.DoesNotContain(
            document.Descendants(),
            element => element.Name.LocalName == "ItemsControl"
                && HasClass(element, "ProjectWorkspaceHost"));
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

    [Fact]
    public void QuickRequestWorkbench_ShouldReuseSharedRequestConfigurationTabs()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "QuickRequestWorkbenchView.axaml"));

        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "HttpInterfaceParamsTabView");
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "HttpInterfaceBodyTabView");
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "HttpInterfaceHeadersTabView");
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "Border" && HasClass(element, "HttpRequestTableHeader"));
    }

    [Fact]
    public void HttpInterfaceBodyTab_ShouldReserveSpaceForTheActiveEditor()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "HttpInterfaceBodyTabView.axaml"));
        var contentHost = document.Descendants()
            .Single(element => element.Name.LocalName == "Grid" && HasClass(element, "HttpBodyContentHost"));

        Assert.Equal("2", contentHost.Attribute("Grid.Row")?.Value);

        var styles = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Assets", "Styles", "WorkspaceEditorStyles.axaml"));
        Assert.Contains(styles.Descendants(), element => element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == "Grid.HttpBodyContentHost");
    }

    [Fact]
    public void RequestHistoryDetail_ShouldStretchLoadingHostAndContentGrid()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "RequestHistoryDetailView.axaml"));
        var loadingContainer = document.Descendants()
            .Single(element => element.Name.LocalName == "LoadingContainer");
        var contentGrid = loadingContainer.Elements()
            .Single(element => element.Name.LocalName == "Grid");

        Assert.Equal("Stretch", loadingContainer.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Stretch", loadingContainer.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("Stretch", loadingContainer.Attribute("HorizontalContentAlignment")?.Value);
        Assert.Equal("Stretch", loadingContainer.Attribute("VerticalContentAlignment")?.Value);
        Assert.Equal("Stretch", contentGrid.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Stretch", contentGrid.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("0", contentGrid.Attribute("MinWidth")?.Value);
        Assert.Equal("0", contentGrid.Attribute("MinHeight")?.Value);
    }

    [Fact]
    public void RequestEditor_ShouldSeparateInterfaceContextFromRequestExecution()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "RequestEditorWorkspaceView.axaml"));
        var contextBar = document.Descendants()
            .Single(element => element.Name.LocalName == "Border" && HasClass(element, "HttpEditorContextBar"));
        var contextGrid = contextBar.Elements()
            .Single(element => element.Name.LocalName == "Grid");

        Assert.Equal("Auto,*,Auto", contextGrid.Attribute("ColumnDefinitions")?.Value);
        Assert.Contains(
            contextGrid.Descendants(),
            element => element.Name.LocalName == "TextBox"
                && element.Attribute("Grid.Column")?.Value == "1"
                && HasClass(element, "HttpEditorTitleInput"));
        Assert.Contains(
            contextGrid.Descendants(),
            element => element.Name.LocalName == "Border" && HasClass(element, "HttpEditorModeBar"));
        Assert.Contains(
            contextGrid.Descendants(),
            element => element.Name.LocalName == "Button"
                && HasClass(element, "HttpCodeIconButton"));
    }

    [Fact]
    public void RequestHistoryViews_ShouldUseSelectionDrivenPreviewAndContinueAction()
    {
        var sidebar = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "ProjectWorkspaceSidebarView.axaml"));
        var historyList = sidebar.Descendants()
            .Single(element => element.Name.LocalName == "ListBox" && HasClass(element, "HistoryListBox"));

        Assert.Equal("{Binding HistoryPanel.SelectedHistoryItem, Mode=TwoWay}", historyList.Attribute("SelectedItem")?.Value);
        Assert.DoesNotContain(
            sidebar.Descendants(),
            element => element.Name.LocalName == "Button" && element.Attribute("Content")?.Value == "转存");

        var detail = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "RequestHistoryDetailView.axaml"));
        Assert.Contains(
            detail.Descendants(),
            element => element.Name.LocalName == "Border" && HasClass(element, "HistoryRequestDetailCard"));
        Assert.Contains(
            detail.Descendants(),
            element => element.Name.LocalName == "Button"
                && element.Attribute("Content")?.Value == "继续请求"
                && element.Attribute("CommandParameter")?.Value == "{Binding HistoryPanel.SelectedHistoryItem}");
    }

    [Fact]
    public void ProjectWorkspaceSidebar_ShouldUseACompactTwoLevelCatalogHierarchy()
    {
        var sidebar = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "ProjectWorkspaceSidebarView.axaml"));

        Assert.Contains(sidebar.Descendants(), element => element.Name.LocalName == "Border"
            && HasClass(element, "ProjectSidebarQuickSectionCard"));

        // 数据模型 / 组件库为无内容的占位目录，已按产品决策移除，不允许回归
        Assert.DoesNotContain(sidebar.Descendants(), element => element.Name.LocalName == "ToggleButton"
            && HasClass(element, "ProjectSidebarSecondaryCatalogButton"));
        Assert.DoesNotContain(sidebar.DescendantNodes().OfType<System.Xml.Linq.XText>(),
            element => element.Value.Contains("数据模型") || element.Value.Contains("组件库"));

        var styles = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Assets", "Styles", "WorkspaceSidebarStyles.axaml"));
        Assert.Contains(styles.Descendants(), element => element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == "Border.ProjectSidebarQuickSectionCard");
    }

    [Fact]
    public void EnvironmentManager_ShouldUseACompactFormAndVariablesEmptyState()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "EnvironmentManagerDialogView.axaml"));

        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Border"
            && HasClass(element, "EnvironmentIdentityBar"));
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Border"
            && HasClass(element, "EnvironmentVariablesEmptyState"));
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Border"
            && HasClass(element, "EnvironmentDialogFooter"));

        var styles = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Assets", "Styles", "DialogStyles.axaml"));
        Assert.Contains(styles.Descendants(), element => element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == "Border.EnvironmentVariablesEmptyState");
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
