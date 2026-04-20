using System.Collections.Generic;
using FakeRequestCaseService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeRequestCaseService;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.Results;

namespace ApixPress.App.Tests.ViewModels;

public sealed partial class ProjectTabViewModelTests
{
    [Fact]
    public async Task InitializeAsync_ShouldLoadImportedSwaggerDocuments()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 3);

        var viewModel = CreateViewModel(apiWorkspaceService);

        await viewModel.InitializeAsync();

        var imported = Assert.Single(viewModel.Import.ImportedApiDocuments);
        Assert.Equal("支付服务", imported.Name);
        Assert.Equal("3", imported.EndpointCountText);
        Assert.True(viewModel.Import.HasImportedApiDocuments);
        Assert.Equal("1", viewModel.Import.ImportedApiDocumentCountText);
    }

    [Fact]
    public async Task InitializeAsync_ShouldShowImportedEndpointsInInterfaceCatalog()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 2);

        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();

        var titles = FlattenExplorerTitles(viewModel.Catalog.InterfaceCatalogItems).ToList();

        Assert.Contains("默认分组 (2)", titles);
        Assert.Contains("接口 1", titles);
        Assert.Contains("接口 2", titles);
        Assert.Equal(2, requestCaseService.Cases.Count(item => item.EntryType == "http-interface"));
    }

    [Fact]
    public async Task InitializeAsync_ShouldLoadSavedRequestsOnlyOnceWhenRefreshingImportedDocuments()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 2);

        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();

        Assert.Equal(1, requestCaseService.GetCasesCallCount);
    }

    [Fact]
    public async Task InitializeAsync_ShouldExposeFallbackDisplayTitleForUnnamedInterface()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local",
        [
            ("默认分组", string.Empty, "GET", "/unnamed-endpoint")
        ]);

        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();

        var folderItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "默认分组 (1)");
        var unnamedInterface = Assert.Single(folderItem!.Children);

        Assert.Equal(string.Empty, unnamedInterface.Title);
        Assert.Equal("未命名接口", unnamedInterface.DisplayTitle);
    }

    [Fact]
    public async Task ImportSwaggerUrlCommand_ShouldRefreshImportedDocumentList()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var viewModel = CreateViewModel(apiWorkspaceService);
        await viewModel.InitializeAsync();

        viewModel.Settings.ShowImportDataCommand.Execute(null);
        viewModel.Import.ImportUrl = "https://demo.local/swagger.json";

        await viewModel.Import.ImportSwaggerUrlCommand.ExecuteAsync(null);

        var imported = Assert.Single(viewModel.Import.ImportedApiDocuments);
        Assert.Equal("Swagger URL 导入成功：远程订单服务", viewModel.StatusMessage);
        Assert.Equal("远程订单服务", imported.Name);
        Assert.Equal("URL 导入", imported.SourceTypeText);
        Assert.True(viewModel.Import.ShowImportStatusSuccess);
        Assert.Equal("https://demo.local/swagger.json", apiWorkspaceService.LastImportedUrl);
    }

    [Fact]
    public async Task ImportSwaggerUrlCommand_ShouldShowBusyOverlayStateWhileLoading()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService
        {
            UrlPreviewGate = new TaskCompletionSource<bool>()
        };
        var viewModel = CreateViewModel(apiWorkspaceService);
        await viewModel.InitializeAsync();

        viewModel.Settings.ShowImportDataCommand.Execute(null);
        viewModel.Import.ImportUrl = "https://demo.local/swagger.json";

        var importTask = viewModel.Import.ImportSwaggerUrlCommand.ExecuteAsync(null);

        Assert.True(SpinWait.SpinUntil(() => viewModel.Import.IsImportDataBusy, TimeSpan.FromSeconds(1)));
        Assert.False(viewModel.Import.CanEditImportData);
        Assert.Equal("正在获取并校验 Swagger URL...", viewModel.Import.ImportDataBusyText);

        apiWorkspaceService.UrlPreviewGate.SetResult(true);
        await importTask;

        Assert.False(viewModel.Import.IsImportDataBusy);
        Assert.True(viewModel.Import.CanEditImportData);
    }

    [Fact]
    public async Task ImportSwaggerUrlCommand_ShouldAppendImportedDocumentWhenNoPathConflict()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "旧文档", "FILE", @"C:\temp\legacy-swagger.json", "https://legacy.demo.local", 1);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);
        await viewModel.InitializeAsync();

        viewModel.Settings.ShowImportDataCommand.Execute(null);
        viewModel.Import.ImportUrl = "https://demo.local/swagger.json";

        await viewModel.Import.ImportSwaggerUrlCommand.ExecuteAsync(null);

        Assert.Equal("2", viewModel.Import.ImportedApiDocumentCountText);
        Assert.Equal(3, requestCaseService.Cases.Count(item => item.EntryType == "http-interface"));
        Assert.Contains(requestCaseService.Cases, item => item.Name == "接口 1" && item.RequestSnapshot.Url == "/endpoint-1");
        Assert.Contains(requestCaseService.Cases, item => item.Name == "查询订单列表" && item.RequestSnapshot.Url == "/orders");
    }

    [Fact]
    public async Task ImportSwaggerUrlCommand_ShouldRequireConfirmationBeforeOverwritingConflictingEndpoints()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "旧文档", "FILE", @"C:\temp\legacy-swagger.json", "https://legacy.demo.local",
        [
            ("订单", "旧的查询订单列表", "GET", "/orders")
        ]);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);
        await viewModel.InitializeAsync();

        viewModel.Settings.ShowImportDataCommand.Execute(null);
        viewModel.Import.ImportUrl = "https://demo.local/swagger.json";

        await viewModel.Import.ImportSwaggerUrlCommand.ExecuteAsync(null);

        Assert.True(viewModel.Import.IsOverwriteConfirmDialogOpen);
        Assert.Contains("更新", viewModel.Import.PendingImportOverwriteSummary);
        Assert.Contains("GET /orders", viewModel.Import.PendingImportOverwriteDetailText);

        await viewModel.Import.ConfirmImportOverwriteCommand.ExecuteAsync(null);

        Assert.False(viewModel.Import.IsOverwriteConfirmDialogOpen);
        Assert.Equal("1", viewModel.Import.ImportedApiDocumentCountText);
        Assert.Contains(requestCaseService.Cases, item => item.Name == "查询订单列表" && item.RequestSnapshot.Url == "/orders");
        Assert.DoesNotContain(requestCaseService.Cases, item => item.Name == "旧的查询订单列表");
    }

    [Fact]
    public async Task SaveCurrentEditorAsync_ShouldKeepImportedEndpointIdentity()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 1);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();

        var interfaceItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "接口 1");
        Assert.NotNull(interfaceItem);

        viewModel.Catalog.LoadWorkspaceItem(interfaceItem);
        viewModel.Editor.CurrentHttpInterfaceName = "接口 1 已编辑";

        await viewModel.Workflow.SaveCurrentEditorAsync();

        var savedInterface = Assert.Single(requestCaseService.Cases, item => item.EntryType == "http-interface");
        Assert.Equal("接口 1 已编辑", savedInterface.Name);
        Assert.Equal("swagger-import:GET /endpoint-1", savedInterface.RequestSnapshot.EndpointId);

        await requestCaseService.SyncImportedHttpInterfacesAsync("project-1",
        [
            new ApiEndpointDto
            {
                GroupName = "默认分组",
                Name = "接口 1",
                Method = "GET",
                Path = "/endpoint-1"
            }
        ], CancellationToken.None);

        Assert.Equal(1, requestCaseService.Cases.Count(item => item.EntryType == "http-interface"));
    }

    [Fact]
    public async Task LoadWorkspaceItem_ShouldKeepVisibleWorkspaceTabsStableWhenSwitchingHttpInterfaces()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 2);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();

        var visibleTabs = viewModel.VisibleWorkspaceTabs;
        var firstInterfaceItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "接口 1");
        var secondInterfaceItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "接口 2");
        Assert.NotNull(firstInterfaceItem);
        Assert.NotNull(secondInterfaceItem);

        viewModel.Catalog.LoadWorkspaceItem(firstInterfaceItem);
        viewModel.Catalog.LoadWorkspaceItem(secondInterfaceItem);

        Assert.Same(visibleTabs, viewModel.VisibleWorkspaceTabs);
        Assert.Equal(2, viewModel.VisibleWorkspaceTabs.Count);
        Assert.Equal("接口 1", viewModel.VisibleWorkspaceTabs[0].HeaderText);
        Assert.Equal("接口 2", viewModel.VisibleWorkspaceTabs[1].HeaderText);
        Assert.Equal("接口 2", viewModel.ActiveWorkspaceTab?.HeaderText);
    }

    [Fact]
    public async Task ConfirmQuickRequestSaveAsync_ShouldKeepExistingInterfaceTreeNodesStable()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 1);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();

        var originalFolder = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "默认分组 (1)");
        Assert.NotNull(originalFolder);

        viewModel.Workspace.OpenQuickRequestWorkspaceCommand.Execute(null);
        Assert.NotNull(viewModel.ActiveWorkspaceTab);
        viewModel.ActiveWorkspaceTab!.RequestUrl = "https://demo.local/ping";
        viewModel.ActiveWorkspaceTab.ConfigTab.RequestName = "健康检查";

        await viewModel.Workflow.SaveCurrentEditorAsync();
        await viewModel.QuickRequestSave.ConfirmSaveCommand.ExecuteAsync(null);

        var currentFolder = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "默认分组 (1)");
        Assert.Same(originalFolder, currentFolder);
        Assert.Single(viewModel.Catalog.QuickRequestTreeItems);
        Assert.Equal("健康检查", viewModel.Catalog.QuickRequestTreeItems[0].Title);
    }

    [Fact]
    public async Task SaveCurrentEditorAsync_ShouldKeepExistingInterfaceTreeNodesStable()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 1);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();

        var originalFolder = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "默认分组 (1)");
        var originalInterface = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "接口 1");
        Assert.NotNull(originalFolder);
        Assert.NotNull(originalInterface);

        viewModel.Catalog.LoadWorkspaceItem(originalInterface);
        viewModel.Editor.CurrentHttpInterfaceName = "接口 1 已编辑";

        await viewModel.Workflow.SaveCurrentEditorAsync();

        var currentFolder = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "默认分组 (1)");
        var currentInterface = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "接口 1 已编辑");
        Assert.Same(originalFolder, currentFolder);
        Assert.Same(originalInterface, currentInterface);
        Assert.Equal("接口 1 已编辑", currentInterface!.Title);
    }

    [Fact]
    public void OpenHttpInterfaceWorkspace_ShouldUseDefaultModuleFolder()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());

        viewModel.Workspace.OpenHttpInterfaceWorkspaceCommand.Execute(null);

        Assert.Equal("默认模块", viewModel.Editor.CurrentInterfaceFolderPath);
    }

    [Fact]
    public async Task DeleteWorkspaceItemAsync_ShouldDeleteFolderAndImportedEndpoints()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 2);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();

        var folderItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "默认分组 (2)");
        Assert.NotNull(folderItem);

        await viewModel.Catalog.DeleteWorkspaceItemAsync(folderItem);

        Assert.Empty(viewModel.Catalog.InterfaceCatalogItems);
        Assert.Empty(requestCaseService.Cases);
        var remainingDocument = Assert.Single(viewModel.Import.ImportedApiDocuments);
        Assert.Equal("0", remainingDocument.EndpointCountText);
    }

    [Fact]
    public async Task RequestDeleteWorkspaceTreeItemCommand_ShouldOpenDeleteConfirmDialog()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 1);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();

        var interfaceItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "接口 1");
        Assert.NotNull(interfaceItem);

        viewModel.Catalog.RequestDeleteWorkspaceTreeItemCommand.Execute(interfaceItem);

        Assert.True(viewModel.Catalog.IsDeleteConfirmDialogOpen);
        Assert.Equal("接口 1", viewModel.Catalog.PendingDeleteTitle);
        Assert.Contains("删除后无法恢复", viewModel.Catalog.PendingDeleteDescription);
    }

    [Fact]
    public async Task ConfirmWorkspaceItemDeleteCommand_ShouldDeleteAfterConfirmation()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 1);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();

        var interfaceItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "接口 1");
        Assert.NotNull(interfaceItem);

        viewModel.Catalog.RequestDeleteWorkspaceTreeItemCommand.Execute(interfaceItem);
        await viewModel.Catalog.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.False(viewModel.Catalog.IsDeleteConfirmDialogOpen);
        Assert.Null(viewModel.Catalog.PendingDeleteWorkspaceItem);
        Assert.Empty(viewModel.Catalog.InterfaceCatalogItems);
        Assert.Empty(requestCaseService.Cases);
    }

    [Fact]
    public void ProjectSettingsCommands_ShouldSwitchBetweenOverviewAndImportDataSections()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());

        viewModel.Settings.OpenWorkspaceCommand.Execute(null);
        viewModel.Settings.ShowOverviewCommand.Execute(null);

        Assert.True(viewModel.IsProjectSettingsSection);
        Assert.True(viewModel.Settings.IsOverviewSelected);
        Assert.False(viewModel.Settings.IsImportDataSelected);
        Assert.Equal("基本设置", viewModel.Settings.CurrentTitle);

        viewModel.Settings.ShowImportDataCommand.Execute(null);

        Assert.True(viewModel.IsProjectSettingsSection);
        Assert.False(viewModel.Settings.IsOverviewSelected);
        Assert.True(viewModel.Settings.IsImportDataSelected);
        Assert.Equal("导入数据", viewModel.Settings.CurrentTitle);

        viewModel.Settings.OpenWorkspaceCommand.Execute(null);

        Assert.True(viewModel.IsProjectSettingsSection);
        Assert.True(viewModel.Settings.IsOverviewSelected);
        Assert.False(viewModel.Settings.IsImportDataSelected);
        Assert.Equal("基本设置", viewModel.Settings.CurrentTitle);
    }

    [Fact]
    public void SelectingWorkspaceNavigationItem_ShouldSwitchWorkspaceSection()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());
        var projectSettingsItem = Assert.Single(viewModel.WorkspaceNavigationItems, item => item.SectionKey == "project-settings");

        viewModel.SelectedWorkspaceNavigationItem = projectSettingsItem;

        Assert.True(viewModel.IsProjectSettingsSection);
        Assert.Equal(projectSettingsItem, viewModel.SelectedWorkspaceNavigationItem);
    }

    [Fact]
    public void ExecutingProjectSettingsNavCommand_ShouldSwitchWorkspaceSection()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());
        var projectSettingsItem = Assert.Single(viewModel.WorkspaceNavigationItems, item => item.SectionKey == "project-settings");

        projectSettingsItem.Command.Execute(null);

        Assert.True(viewModel.IsProjectSettingsSection);
        Assert.Equal(projectSettingsItem, viewModel.SelectedWorkspaceNavigationItem);
        Assert.True(projectSettingsItem.IsSelected);
    }
}
