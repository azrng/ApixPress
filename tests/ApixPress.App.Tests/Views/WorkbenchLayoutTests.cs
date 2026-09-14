using System.Xml.Linq;

namespace ApixPress.App.Tests.Views;

public sealed class WorkbenchLayoutTests
{
    [Fact]
    public void MainWindow_ProjectWorkspaceHost_ShouldCacheWorkspaceViewPerTab()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "MainWindow.axaml"));
        var host = document.Descendants()
            .Single(element => element.Name.LocalName == "ProjectTabHostView"
                && HasClass(element, "ProjectWorkspaceHost"));

        Assert.Equal("Stretch", host.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Stretch", host.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("{Binding HasActiveProjectTab}", host.Attribute("IsVisible")?.Value);

        // 视图由 ProjectTabHostView 代码按 ActiveProjectTab 缓存复用，XAML 不允许回到 ContentTemplate 整树重建
        Assert.DoesNotContain(
            document.Descendants(),
            element => element.Name.LocalName == "ContentControl.ContentTemplate");
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
        Assert.Equal("170", rowDefinitions[0].Attribute("MinHeight")?.Value);
        Assert.Equal("{Binding ConfigTab.ConfigPanelMaxHeight}", rowDefinitions[0].Attribute("MaxHeight")?.Value);
        Assert.Equal("6", rowDefinitions[1].Attribute("Height")?.Value);
        Assert.Equal("*", rowDefinitions[2].Attribute("Height")?.Value);
        Assert.DoesNotContain(
            splitterHost.Descendants(),
            element => HasClass(element, "HttpDesignMetaCard"));
    }

    [Theory]
    [InlineData("HttpInterfaceWorkbenchView.axaml")]
    [InlineData("QuickRequestWorkbenchView.axaml")]
    public void WorkbenchConfigCard_ShouldMergeUrlRowWithConfigTabs(string fileName)
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", fileName));
        var configCard = document.Descendants()
            .Single(element => element.Name.LocalName == "Border" && HasClass(element, "HttpWorkbenchConfigCard"));

        Assert.Contains(
            configCard.Descendants(),
            element => element.Name.LocalName == "HttpRequestUrlRowView");
        Assert.Contains(
            configCard.Descendants(),
            element => element.Name.LocalName == "TabControl" && HasClass(element, "HttpRequestTabs"));
    }

    [Fact]
    public void HttpCaseSave_ShouldUseNamingDialogInsteadOfInlineNameInput()
    {
        var requestRow = XDocument.Load(FindSourceFile(
            "src", "ApixPress.App", "Views", "Controls", "HttpRequestUrlRowView.axaml"));
        Assert.DoesNotContain(
            requestRow.Descendants(),
            element => element.Name.LocalName == "TextBox" && HasClass(element, "HttpCaseNameInput"));

        var dialog = XDocument.Load(FindSourceFile(
            "src", "ApixPress.App", "Views", "Controls", "HttpCaseSaveDialogView.axaml"));
        // 弹窗内容视图的 DataContext 即用例保存 VM，弹窗 chrome（遮罩/标题/关闭）由 Ursa StandardDialogControl 提供
        Assert.Contains(
            dialog.Descendants(),
            element => element.Name.LocalName == "TextBox"
                && (element.Attribute("Text")?.Value ?? string.Empty)
                    .Contains("{Binding DraftName}", StringComparison.Ordinal));
        Assert.Contains(
            dialog.Descendants(),
            element => element.Name.LocalName == "Button"
                && (element.Attribute("Command")?.Value ?? string.Empty)
                    .Contains("{Binding ConfirmSaveCommand}", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("HttpInterfaceWorkbenchView.axaml")]
    [InlineData("QuickRequestWorkbenchView.axaml")]
    public void ResponseBody_ShouldRenderInsideCodeCardWithVirtualizedEditor(string fileName)
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", fileName));
        var bodyEditor = document.Descendants()
            .Single(element => element.Name.LocalName == "ResponseBodyEditor"
                && (element.Attribute("Text")?.Value ?? string.Empty).Contains("{Binding ResponseSection.BodyText}"));

        var bodyCard = bodyEditor.Ancestors().First(element => HasClass(element, "HttpResponseBodyCard"));
        Assert.NotNull(bodyCard);

        // 回归守卫：Body 查看器必须保持 AvaloniaEdit 可视区排版，不得回到 SelectableTextBlock 整页构建
        Assert.DoesNotContain(
            bodyCard.Descendants(),
            element => HasClass(element, "HttpResponseBodyViewer"));
    }

    [Fact]
    public void MainWindowTitleBar_ShouldHostWorkspaceToolbarInTitleBar()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "MainWindowTitleBarView.axaml"));
        var toolbarHost = document.Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && element.Elements().Any(child => child.Name.LocalName == "ProjectWorkspaceToolbarView"));

        Assert.Equal("{Binding HasActiveProjectTab}", toolbarHost.Attribute("IsVisible")?.Value);
        Assert.Equal(
            "{Binding ActiveProjectTab}",
            toolbarHost.Elements().Single(element => element.Name.LocalName == "ProjectWorkspaceToolbarView")
                .Attribute("DataContext")?.Value);
    }

    [Fact]
    public void Workspaces_ShouldNotEmbedStandaloneWorkspaceToolbar()
    {
        foreach (var fileName in new[]
                 {
                     "RequestEditorWorkspaceView.axaml",
                     "ProjectInterfaceRootWorkspaceView.axaml",
                     "WorkspaceLandingView.axaml"
                 })
        {
            var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", fileName));
            Assert.DoesNotContain(
                document.Descendants(),
                element => element.Name.LocalName == "ProjectWorkspaceToolbarView");
        }
    }

    [Fact]
    public void RequestTabs_ShouldKeepPrimaryForegroundWhenSelected()
    {
        var styles = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Assets", "Styles", "WorkspaceEditorStyles.axaml"));
        var selectedTabStyle = styles.Descendants()
            .Single(element => element.Name.LocalName == "Style"
                && element.Attribute("Selector")?.Value == "TabControl.HttpRequestTabs TabItem:selected");

        var foreground = selectedTabStyle.Elements()
            .Single(element => element.Name.LocalName == "Setter"
                && element.Attribute("Property")?.Value == "Foreground")
            .Attribute("Value")?.Value;

        Assert.Equal("{DynamicResource Brush.Text.Primary}", foreground);
    }

    [Fact]
    public void InterfaceRootWorkspace_ShouldUseFullWidthWorkbenchTable()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "ProjectInterfaceRootWorkspaceView.axaml"));

        var rootLayout = document.Root?.Elements().SingleOrDefault();
        Assert.NotNull(rootLayout);
        Assert.Equal("Grid", rootLayout!.Name.LocalName);
        // 工具栏并入标题栏后，根目录工作区不再需要工具栏行
        Assert.Equal("*", rootLayout.Attribute("RowDefinitions")?.Value);
        Assert.Null(rootLayout.Attribute("RowSpacing"));
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
    public void EnvironmentManager_ShouldUseDrawerWithEnvCardsAndInlineDeleteStrip()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "EnvironmentManagerDialogView.axaml"));

        // 对齐原型 07：右侧抽屉 + 环境卡片列表 + 内联删除确认条
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Border"
            && HasClass(element, "EnvDrawerPanel"));
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Border"
            && HasClass(element, "EnvDrawerHead"));
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Border"
            && HasClass(element, "EnvCard"));
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Border"
            && HasClass(element, "EnvDeleteStrip"));

        var styles = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Assets", "Styles", "DialogStyles.axaml"));
        Assert.Contains(styles.Descendants(), element => element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == "Border.EnvDrawerPanel");
        Assert.Contains(styles.Descendants(), element => element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == "Border.EnvDeleteStrip");
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
