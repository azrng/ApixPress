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

        // 模式选择与提示合并为一行后，内容宿主位于第 1 行并占满剩余空间
        Assert.Equal("1", contentHost.Attribute("Grid.Row")?.Value);

        // 宿主最小高必须留在紧凑面板预算内（288 - URL行/页签行/边距/选择器 ≈ 剩 128），
        // 超预算会触发 Grid 亏空排列，空态向上叠住选择器行（T057 回归守卫）
        var styles = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Assets", "Styles", "WorkspaceEditorStyles.axaml"));
        var hostStyle = styles.Descendants().Single(element => element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == "Grid.HttpBodyContentHost");
        var minHeight = double.Parse(hostStyle.Elements()
            .Single(element => element.Name.LocalName == "Setter" && element.Attribute("Property")?.Value == "MinHeight")
            .Attribute("Value")?.Value ?? "0");
        Assert.True(minHeight <= 128, $"HttpBodyContentHost MinHeight={minHeight} 超出紧凑面板可用高度");
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
        // 环境管理已迁移为右侧抽屉（多环境卡片平铺，对齐原型 07）：EnvironmentDrawerView
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "EnvironmentDrawerView.axaml"));

        // 卡片式环境列表：每张卡片含激活徽章、逐卡新增变量、单卡保存与全局保存
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Border"
            && HasClass(element, "PanelCard"));
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Border"
            && HasClass(element, "EnvMenuActiveChip"));
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Button"
            && element.Attribute("Command")?.Value?.Contains("AddVariableCommand") == true);
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Button"
            && element.Attribute("Command")?.Value?.Contains("SaveAllEnvironmentsCommand") == true);
    }

    [Fact]
    public void ProjectWorkspaceSidebar_QuickRequestArea_ShouldUseVirtualizedListBox()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "ProjectWorkspaceSidebarView.axaml"));

        // 快捷请求是扁平列表：TreeView 默认面板不虚拟化（有 MaxHeight 也全量实例化），必须走 ListBox+VSP
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "TreeView");

        var quickList = document.Descendants()
            .Single(element => element.Name.LocalName == "ListBox" && HasClass(element, "QuickRequestSidebarTree"));
        Assert.Equal("{Binding Catalog.QuickRequestTreeItems}", quickList.Attribute("ItemsSource")?.Value);
        Assert.Contains(
            quickList.Descendants(),
            element => element.Name.LocalName == "VirtualizingStackPanel");
    }

    [Fact]
    public void UseCasesDrawerView_CaseList_ShouldVirtualizeInsideOwnViewport()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "UseCasesDrawerView.axaml"));

        // 用例列表自身提供 ScrollViewer+VSP 并占据有界行（Grid * 行），不再外层整页滚动导致全量实例化
        var caseList = document.Descendants()
            .Single(element => element.Name.LocalName == "ItemsControl"
                && (element.Attribute("ItemsSource")?.Value ?? string.Empty).Contains("CurrentInterfaceCaseItems"));
        Assert.Contains(
            caseList.Descendants(),
            element => element.Name.LocalName == "VirtualizingStackPanel");
        Assert.Contains(
            caseList.Descendants(),
            element => element.Name.LocalName == "ScrollViewer");
        Assert.Equal("3", caseList.Attribute("Grid.Row")?.Value);
        Assert.DoesNotContain(
            document.Descendants(),
            element => element.Name.LocalName == "ScrollViewer" && element.Parent?.Name.LocalName == "UserControl");
    }

    private static bool HasClass(XElement element, string className)
    {
        return (element.Attribute("Classes")?.Value ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(className);
    }

    [Theory]
    [InlineData("HttpInterfaceWorkbenchView.axaml", "{Binding ResponseSection}")]
    [InlineData("QuickRequestWorkbenchView.axaml", "{Binding ResponseSection}")]
    [InlineData("RequestHistoryDetailView.axaml", "{Binding HistoryPanel.SelectedResponseSection}")]
    public void ResponseTabs_ShouldExposeActualRequestTab(string fileName, string expectedDataContext)
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", fileName));
        var responseTabs = document.Descendants()
            .Single(element => element.Name.LocalName == "TabControl"
                && HasClass(element, "ResponseTabs"));

        var actualRequestTab = responseTabs.Descendants()
            .Single(element => element.Name.LocalName == "TabItem"
                && (element.Attribute("Header")?.Value == "实际请求"));

        var panel = actualRequestTab.Descendants()
            .Single(element => element.Name.LocalName == "ActualRequestPanelView");
        Assert.Equal(expectedDataContext, panel.Attribute("DataContext")?.Value);
    }

    [Fact]
    public void ActualRequestPanel_ShouldKeepUrlHeaderBodySections()
    {
        var document = XDocument.Load(FindSourceFile(
            "src", "ApixPress.App", "Views", "Controls", "ActualRequestPanelView.axaml"));

        Assert.Equal("vm:ResponseSectionViewModel", document.Root?.Attribute("{http://schemas.microsoft.com/winfx/2006/xaml}DataType")?.Value);
        var source = File.ReadAllText(FindSourceFile(
            "src", "ApixPress.App", "Views", "Controls", "ActualRequestPanelView.axaml"));

        // 面板必须覆盖 URL / Header / Body 三段与无数据兜底，防止后续重写丢段
        Assert.Contains("请求 URL", source);
        Assert.Contains("ActualRequestHeaderRows", source);
        Assert.Contains("ActualRequestBodyText", source);
        Assert.Contains("暂无实际请求数据", source);
    }

    [Theory]
    [InlineData("QuickRequestSaveDialogView.axaml")]
    [InlineData("HttpCaseSaveDialogView.axaml")]
    [InlineData("WorkspaceCreateFolderDialogView.axaml")]
    public void SmallFormDialogs_ShouldLayFormDirectlyWithSeparatedFooter(string fileName)
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", fileName));

        // 对齐原型 modal-body：表单直接铺放在正文上，禁止回到内层卡片嵌套（CreateProjectFormShell 已删除）
        Assert.DoesNotContain(
            document.Descendants(),
            element => HasClass(element, "CreateProjectFormShell"));
        Assert.DoesNotContain(
            document.Descendants(),
            element => element.Name.LocalName == "Form");
        // 底栏带分隔线（对齐原型 modal-foot border-top）
        Assert.Contains(
            document.Descendants(),
            element => HasClass(element, "DialogFooter"));
        Assert.Contains(
            document.Descendants(),
            element => element.Name.LocalName == "TextBlock" && HasClass(element, "DialogFormLabel"));
    }

    [Fact]
    public void RequestCodeDialog_ShouldProvideCopyAndCloseFooter()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "RequestCodeDialogView.axaml"));

        // 对齐原型 10 modal-foot：此前弹窗 showCloseButton:false 且无 footer，用户没有任何复制/关闭途径
        Assert.Contains(
            document.Descendants(),
            element => HasClass(element, "DialogFooter"));
        Assert.Contains(
            document.Descendants(),
            element => element.Name.LocalName == "Button"
                && (element.Attribute("Content")?.Value ?? string.Empty) == "复制代码"
                && (element.Attribute("Command")?.Value ?? string.Empty).Contains("CopyRequestCodeCommand"));
        Assert.Contains(
            document.Descendants(),
            element => element.Name.LocalName == "Button"
                && (element.Attribute("Content")?.Value ?? string.Empty) == "关闭"
                && (element.Attribute("Command")?.Value ?? string.Empty).Contains("CloseRequestCodeDialogCommand"));
        Assert.Contains(
            document.Descendants(),
            element => (element.Attribute("Text")?.Value ?? string.Empty).Contains("RequestCodeCopiedHint"));
    }

    [Fact]
    public void HttpRequestUrlRow_ShouldExposeSaveAndSaveCaseButtonsWithoutFlyout()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "HttpRequestUrlRowView.axaml"));
        var styles = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Assets", "Styles", "WorkspaceEditorStyles.axaml"));

        // 用户决策：caret 下拉交互繁琐，保存与保存用例平铺两按钮；用例管理入口收敛到侧栏计数芯片与用例抽屉
        Assert.Contains(
            document.Descendants(),
            element => element.Name.LocalName == "Button"
                && (element.Attribute("Content")?.Value ?? string.Empty) == "保存"
                && (element.Attribute("Command")?.Value ?? string.Empty).Contains("SaveCaseCommand"));
        Assert.Contains(
            document.Descendants(),
            element => element.Name.LocalName == "Button"
                && (element.Attribute("Content")?.Value ?? string.Empty) == "保存用例"
                && (element.Attribute("Command")?.Value ?? string.Empty).Contains("SaveHttpCaseCommand"));
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "Flyout");
        Assert.DoesNotContain(
            styles.Descendants(), element => element.Name.LocalName == "Style"
                && (element.Attribute("Selector")?.Value ?? string.Empty).Contains("HttpSplit"));
        Assert.DoesNotContain(
            document.Descendants(),
            element => HasClass(element, "HttpUseCasesButton"));
        Assert.DoesNotContain(styles.Descendants(), element => element.Name.LocalName == "Style"
            && (element.Attribute("Selector")?.Value ?? string.Empty).Contains("HttpUseCasesButton"));
    }

    [Theory]
    [InlineData("HttpInterfaceWorkbenchView.axaml")]
    [InlineData("QuickRequestWorkbenchView.axaml")]
    public void RequestConfigurationTabs_ShouldExposeParameterCountBadges(string fileName)
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", fileName));

        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Badge"
            && (element.Attribute("Header")?.Value ?? string.Empty).Contains("QueryParameterCountText"));
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "Badge"
            && (element.Attribute("Header")?.Value ?? string.Empty).Contains("HeaderCountText"));
    }

    [Fact]
    public void T064ConfirmationAndHistoryViews_ShouldExposeNewDecisionStates()
    {
        var importDialog = XDocument.Load(FindSourceFile(
            "src", "ApixPress.App", "Views", "Controls", "ImportOverwriteConfirmDialogView.axaml"));
        Assert.Contains(importDialog.Descendants(), element =>
            (element.Attribute("Content")?.Value ?? string.Empty).Contains("跳过同名接口", StringComparison.Ordinal)
            || (element.Attribute("Text")?.Value ?? string.Empty).Contains("跳过同名接口", StringComparison.Ordinal));

        var deleteDialog = XDocument.Load(FindSourceFile(
            "src", "ApixPress.App", "Views", "Controls", "WorkspaceDeleteConfirmDialogView.axaml"));
        Assert.Contains(deleteDialog.Descendants(), element => HasClass(element, "DialogDangerNotice"));

        var history = XDocument.Load(FindSourceFile(
            "src", "ApixPress.App", "Views", "Controls", "RequestHistoryDetailView.axaml"));
        var snapshotCard = history.Descendants().Single(element => HasClass(element, "HistoryRequestDetailCard"));
        Assert.Contains(snapshotCard.Descendants(), element =>
            (element.Attribute("Text")?.Value ?? string.Empty)
                .Contains("选择左侧历史记录后查看请求快照", StringComparison.Ordinal));
    }

    [Fact]
    public void RequestCodeDialog_ShouldLayLanguagesAcrossTheHeader()
    {
        var document = XDocument.Load(FindSourceFile(
            "src", "ApixPress.App", "Views", "Controls", "RequestCodeDialogView.axaml"));
        var languageBar = document.Descendants()
            .Single(element => element.Name.LocalName == "StackPanel"
                && HasClass(element, "RequestCodeDialogLanguageList"));

        Assert.Equal("Horizontal", languageBar.Attribute("Orientation")?.Value);
    }

    [Fact]
    public void SidebarTree_ShouldExposeCaseCountChipInsteadOfCaseNodes()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "ProjectWorkspaceSidebarView.axaml"));
        var styles = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Assets", "Styles", "WorkspaceEditorStyles.axaml"));

        // 对齐原型 08：树上无用例节点，接口节点用计数芯片承载用例数（点击开抽屉）
        Assert.Contains(
            document.Descendants(),
            element => HasClass(element, "WorkspaceTreeCaseChip"));
        Assert.DoesNotContain(
            document.Descendants(),
            element => (element.Attribute("IsVisible")?.Value ?? string.Empty).Contains("IsHttpCaseNode"));
        Assert.DoesNotContain(styles.Descendants(), element => element.Name.LocalName == "Style"
            && (element.Attribute("Selector")?.Value ?? string.Empty).Contains("WorkspaceTreeStatusDot"));
    }

    [Fact]
    public void HttpInterfaceWorkbench_ShouldDropDesignModeCard()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "HttpInterfaceWorkbenchView.axaml"));
        var styles = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Assets", "Styles", "WorkspaceEditorStyles.axaml"));

        // 「设计」页签已移除，接口说明编辑唯一入口在文档预览页
        Assert.DoesNotContain(
            document.Descendants(),
            element => HasClass(element, "HttpDesignMetaCard"));
        Assert.DoesNotContain(styles.Descendants(), element => element.Name.LocalName == "Style"
            && (element.Attribute("Selector")?.Value ?? string.Empty).Contains("HttpDesign"));
    }

    [Fact]
    public void HttpDocumentPreview_ShouldHostDescriptionEditorAndInfoRows()
    {
        var document = XDocument.Load(FindSourceFile("src", "ApixPress.App", "Views", "Controls", "HttpDocumentWorkspaceView.axaml"));

        Assert.Contains(
            document.Descendants(),
            element => element.Name.LocalName == "TextBox" && HasClass(element, "HttpDocumentDescriptionInput"));
        // 概览改 info-row 形态并带标签类
        Assert.Contains(
            document.Descendants(),
            element => HasClass(element, "HttpDocumentInfoRow"));
        Assert.Contains(
            document.Descendants(),
            element => HasClass(element, "HttpDocumentInfoRowLast"));
        // 未调试时响应示例收缩为空态，不渲染空编辑器
        Assert.Contains(
            document.Descendants(),
            element => (element.Attribute("IsVisible")?.Value ?? string.Empty) == "{Binding !Editor.HasHttpDocumentBodyPreview}");
    }

    [Fact]
    public void BodyTab_ShouldMergeModeSelectorAndHintIntoSingleRow()
    {
        var document = XDocument.Load(FindSourceFile(
            "src", "ApixPress.App", "Views", "Controls", "HttpInterfaceBodyTabView.axaml"));

        // 对齐原型 body-modes：提示横条已删除，提示文字与模式选择器同行（同一父容器）
        Assert.DoesNotContain(
            document.Descendants(),
            element => HasClass(element, "HttpBodyModeHint"));
        var hintText = document.Descendants()
            .Single(element => element.Name.LocalName == "TextBlock"
                && HasClass(element, "HttpBodyModeHintText"));
        var hintRow = hintText.Parent;
        Assert.NotNull(hintRow);
        Assert.Contains(
            hintRow.Descendants(),
            element => element.Name.LocalName == "SelectionList"
                && HasClass(element, "HttpBodyModeSelector"));
    }

    [Fact]
    public void HttpInterfaceBodyTab_ShouldExposeFieldTypeChipsFileChipsAndBinaryMode()
    {
        var document = XDocument.Load(FindSourceFile(
            "src", "ApixPress.App", "Views", "Controls", "HttpInterfaceBodyTabView.axaml"));

        // 对齐原型 03：类型 chip + 文件 chip + 添加文件入口 + Binary 空态选择入口
        Assert.Contains(document.Descendants(), element => HasClass(element, "HttpFieldTypeChip"));
        Assert.Contains(document.Descendants(), element => HasClass(element, "HttpFileChip"));
        Assert.Contains(document.Descendants(), element => HasClass(element, "HttpFileAddButton"));
        Assert.Contains(
            document.Descendants(),
            element => element.Name.LocalName == "TextBlock"
                && (element.Attribute("Text")?.Value ?? string.Empty).Contains("未选择文件"));
        // 旧「说明（可选）」列已让位给类型列（对齐原型 03 表头）
        Assert.DoesNotContain(
            document.Descendants(),
            element => element.Name.LocalName == "TextBlock" && (element.Attribute("Text")?.Value ?? string.Empty) == "说明（可选）");
    }

    [Fact]
    public void ProjectSettingsBasicInfo_ShouldOpenEditDialogInsteadOfProjectDrawer()
    {
        var settings = XDocument.Load(FindSourceFile(
            "src", "ApixPress.App", "Views", "Controls", "ProjectSettingsWorkspaceView.axaml"));
        var dialog = XDocument.Load(FindSourceFile(
            "src", "ApixPress.App", "Views", "Controls", "ProjectInfoEditDialogView.axaml"));

        // 对齐原型 06：基本信息「编辑」打开居中编辑弹窗（editProjectModal），不再跳转右侧项目管理抽屉
        Assert.DoesNotContain(
            settings.Descendants(),
            element => (element.Attribute("Click")?.Value ?? string.Empty).Contains("OnOpenProjectDrawer"));
        Assert.Contains(
            settings.Descendants(),
            element => (element.Attribute("Command")?.Value ?? string.Empty).Contains("OpenRenameProjectDialogCommand"));
        Assert.Contains(
            settings.Descendants(),
            element => (element.Attribute("Command")?.Value ?? string.Empty).Contains("OpenEditProjectDescriptionDialogCommand"));
        Assert.Contains(dialog.Descendants(), element => HasClass(element, "ProjectInfoEditDialogCard"));
        Assert.Contains(dialog.Descendants(), element => HasClass(element, "DialogFooter"));
        Assert.Contains(dialog.Descendants(), element => HasClass(element, "DialogFormLabel"));
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
