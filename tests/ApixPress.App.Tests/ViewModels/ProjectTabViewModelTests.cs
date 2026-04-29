using System.Collections.Generic;
using Avalonia.Controls.Notifications;
using FakeRequestCaseService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeRequestCaseService;
using FakeAppNotificationService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeAppNotificationService;
using FakeFilePickerService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeFilePickerService;
using FakeProjectDataExportService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeProjectDataExportService;
using FakeProjectWorkspaceService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeProjectWorkspaceService;
using FakeRequestExecutionService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeRequestExecutionService;
using FakeRequestHistoryService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeRequestHistoryService;
using FakeSystemDataService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeSystemDataService;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.Results;

namespace ApixPress.App.Tests.ViewModels;

public sealed partial class ProjectTabViewModelTests
{
    [Fact]
    public async Task ShowImportDataCommand_ShouldLoadImportedSwaggerDocumentsOnDemand()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 3);

        var viewModel = CreateViewModel(apiWorkspaceService);

        await viewModel.InitializeAsync();
        Assert.Empty(viewModel.Import.ImportedApiDocuments);
        Assert.Equal(0, apiWorkspaceService.GetDocumentsCallCount);
        Assert.Equal(0, apiWorkspaceService.GetProjectEndpointsCallCount);

        await viewModel.Settings.ShowImportDataCommand.ExecuteAsync(null);

        var imported = Assert.Single(viewModel.Import.ImportedApiDocuments);
        Assert.Equal("支付服务", imported.Name);
        Assert.Equal("3", imported.EndpointCountText);
        Assert.True(viewModel.Import.HasImportedApiDocuments);
        Assert.Equal("1", viewModel.Import.ImportedApiDocumentCountText);
    }

    [Fact]
    public async Task ShowImportDataCommand_ShouldShowImportedEndpointsInInterfaceCatalog()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 2);

        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();
        await viewModel.Settings.ShowImportDataCommand.ExecuteAsync(null);

        var titles = FlattenExplorerTitles(viewModel.Catalog.InterfaceCatalogItems).ToList();

        Assert.Contains("默认分组 (2)", titles);
        Assert.Contains("接口 1", titles);
        Assert.Contains("接口 2", titles);
        Assert.Equal(2, requestCaseService.Cases.Count(item => item.EntryType == "http-interface"));
    }

    [Fact]
    public async Task InterfaceCatalogSearch_ShouldMatchInterfaceNameAndPath()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "订单服务", "FILE", @"C:\temp\orders-swagger.json", "https://order.demo.local",
        [
            ("订单/查询", "查询订单列表", "GET", "/orders"),
            ("用户", "获取用户详情", "GET", "/users/{id}")
        ]);

        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();
        await viewModel.Import.LoadImportedDocumentsAsync(manageBusyState: false);

        viewModel.Catalog.InterfaceSearchText = "用户详情";
        var nameMatchTitles = FlattenExplorerTitles(viewModel.Catalog.InterfaceCatalogItems).ToList();

        Assert.Contains("用户 (1)", nameMatchTitles);
        Assert.Contains("获取用户详情", nameMatchTitles);
        Assert.DoesNotContain("查询订单列表", nameMatchTitles);

        viewModel.Catalog.InterfaceSearchText = "/orders";
        var pathMatchTitles = FlattenExplorerTitles(viewModel.Catalog.InterfaceCatalogItems).ToList();

        Assert.Contains("订单 (1)", pathMatchTitles);
        Assert.Contains("查询 (1)", pathMatchTitles);
        Assert.Contains("查询订单列表", pathMatchTitles);
        Assert.DoesNotContain("获取用户详情", pathMatchTitles);
    }

    [Fact]
    public async Task InitializeAsync_ShouldLoadSavedRequestSummariesWithoutImportedDocuments()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 2);
        requestCaseService.Cases.Add(new RequestCaseDto
        {
            Id = "interface-1",
            ProjectId = "project-1",
            EntryType = "http-interface",
            Name = "查询用户",
            GroupName = "接口",
            FolderPath = "用户",
            RequestSnapshot = new RequestSnapshotDto
            {
                EndpointId = "swagger-import:GET /users",
                Method = "GET",
                Url = "/users"
            },
            UpdatedAt = DateTime.UtcNow
        });

        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();

        Assert.Equal(1, requestCaseService.GetCasesCallCount);
        Assert.Equal(0, apiWorkspaceService.GetDocumentsCallCount);
        Assert.Equal(0, apiWorkspaceService.GetProjectEndpointsCallCount);

        var titles = FlattenExplorerTitles(viewModel.Catalog.InterfaceCatalogItems).ToList();
        Assert.Contains("用户 (1)", titles);
        Assert.Contains("查询用户", titles);
    }

    [Fact]
    public async Task InitializeAsync_ShouldNotLoadRequestHistoryUntilHistorySectionOpened()
    {
        var requestHistoryService = new FakeRequestHistoryService();
        requestHistoryService.Items.Add(new RequestHistoryItemDto
        {
            Id = "history-1",
            Timestamp = new DateTime(2026, 4, 24, 10, 0, 0, DateTimeKind.Utc),
            HasResponse = true,
            StatusCode = 200,
            DurationMs = 12,
            SizeBytes = 128,
            RequestSnapshot = new RequestSnapshotDto
            {
                Method = "GET",
                Url = "https://demo.local/orders"
            },
            ResponseSnapshot = new ResponseSnapshotDto
            {
                StatusCode = 200,
                DurationMs = 12,
                SizeBytes = 128,
                Content = "{\"ok\":true}"
            }
        });

        var viewModel = CreateViewModel(new FakeApiWorkspaceService(), requestHistoryService: requestHistoryService);

        await viewModel.InitializeAsync();

        Assert.Equal(0, requestHistoryService.GetHistoryCallCount);
        Assert.Equal(0, requestHistoryService.GetDetailCallCount);
        Assert.Empty(viewModel.RequestHistory);
    }

    [Fact]
    public async Task InterfaceCatalog_ShouldBeCollapsedByDefault()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());

        await viewModel.InitializeAsync();

        Assert.False(viewModel.Catalog.IsInterfaceCatalogExpanded);
    }

    [Fact]
    public async Task InterfaceCatalog_ShouldLazyLoadNestedNodesWhenExpanded()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local",
        [
            ("订单/支付", "查询支付状态", "GET", "/payments/status")
        ]);

        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();
        await viewModel.Import.LoadImportedDocumentsAsync(manageBusyState: false);

        var folderItem = Assert.Single(viewModel.Catalog.InterfaceCatalogItems);
        Assert.True(folderItem.HasChildren);
        Assert.Empty(folderItem.Children);

        folderItem.IsExpanded = true;

        var nestedFolder = Assert.Single(folderItem.Children);
        Assert.Equal("支付 (1)", nestedFolder.Title);
        Assert.Empty(nestedFolder.Children);

        nestedFolder.IsExpanded = true;

        var interfaceItem = Assert.Single(nestedFolder.Children);
        Assert.Equal("查询支付状态", interfaceItem.Title);
    }

    [Fact]
    public async Task ImportSwaggerUrlCommand_ShouldNotReloadAllSavedRequestsAfterSyncImportedInterfaces()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);
        await viewModel.InitializeAsync();

        await viewModel.Settings.ShowImportDataCommand.ExecuteAsync(null);
        viewModel.Import.ImportUrl = "https://demo.local/swagger.json";

        await viewModel.Import.ImportSwaggerUrlCommand.ExecuteAsync(null);

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
        await viewModel.Import.LoadImportedDocumentsAsync(manageBusyState: false);

        var folderItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "默认分组 (1)");
        var unnamedInterface = Assert.Single(folderItem!.Children);

        Assert.Equal(string.Empty, unnamedInterface.Title);
        Assert.Equal("未命名接口", unnamedInterface.DisplayTitle);
    }

    [Fact]
    public async Task ImportSwaggerUrlCommand_ShouldRefreshImportedDocumentList()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var notificationService = new FakeAppNotificationService();
        var viewModel = CreateViewModel(apiWorkspaceService, appNotificationService: notificationService);
        await viewModel.InitializeAsync();

        await viewModel.Settings.ShowImportDataCommand.ExecuteAsync(null);
        viewModel.Import.ImportUrl = "https://demo.local/swagger.json";

        await viewModel.Import.ImportSwaggerUrlCommand.ExecuteAsync(null);

        var imported = Assert.Single(viewModel.Import.ImportedApiDocuments);
        Assert.Equal("Swagger URL 导入成功：远程订单服务", viewModel.StatusMessage);
        Assert.Equal("远程订单服务", imported.Name);
        Assert.Equal("URL 导入", imported.SourceTypeText);
        Assert.True(viewModel.Import.ShowImportStatusSuccess);
        Assert.Equal("https://demo.local/swagger.json", apiWorkspaceService.LastImportedUrl);
        var notification = Assert.Single(notificationService.Notifications);
        Assert.Equal("Swagger 导入成功", notification.Title);
        Assert.Equal("Swagger URL 导入成功：远程订单服务", notification.Content);
        Assert.Equal(NotificationType.Success, notification.Type);
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

        await viewModel.Settings.ShowImportDataCommand.ExecuteAsync(null);
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

        await viewModel.Settings.ShowImportDataCommand.ExecuteAsync(null);
        viewModel.Import.ImportUrl = "https://demo.local/swagger.json";

        await viewModel.Import.ImportSwaggerUrlCommand.ExecuteAsync(null);

        Assert.Equal("2", viewModel.Import.ImportedApiDocumentCountText);
        Assert.Equal(3, requestCaseService.Cases.Count(item => item.EntryType == "http-interface"));
        Assert.Contains(requestCaseService.Cases, item => item.Name == "接口 1" && item.RequestSnapshot.Url == "/endpoint-1");
        Assert.Contains(requestCaseService.Cases, item => item.Name == "查询订单列表" && item.RequestSnapshot.Url == "/orders");
    }

    [Fact]
    public async Task ExportProjectDataCommand_ShouldExportInterfacesAndTestCases()
    {
        var filePickerService = new FakeFilePickerService
        {
            SaveProjectDataExportFileResult = @"C:\temp\project-export.apixpkg.json"
        };
        var exportService = new FakeProjectDataExportService
        {
            Result = ResultModel<ProjectDataExportResultDto>.Success(new ProjectDataExportResultDto
            {
                FilePath = @"C:\temp\project-export.apixpkg.json",
                InterfaceCount = 2,
                TestCaseCount = 3
            })
        };
        var notificationService = new FakeAppNotificationService();
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            appNotificationService: notificationService,
            filePickerService: filePickerService,
            projectDataExportService: exportService);

        await viewModel.InitializeAsync();
        await viewModel.Import.ExportProjectDataCommand.ExecuteAsync(null);

        Assert.NotNull(exportService.LastRequest);
        Assert.Equal("project-1", exportService.LastRequest!.ProjectId);
        Assert.Equal("测试项目", exportService.LastRequest.ProjectName);
        Assert.Equal("用于验证项目设置数据管理", exportService.LastRequest.ProjectDescription);
        Assert.Equal(@"C:\temp\project-export.apixpkg.json", exportService.LastRequest.OutputFilePath);
        Assert.Equal("项目数据导出成功：2 个接口、3 个测试用例，已保存到 project-export.apixpkg.json", viewModel.StatusMessage);
        Assert.True(viewModel.Import.ShowImportStatusSuccess);
        var notification = Assert.Single(notificationService.Notifications);
        Assert.Equal("项目数据导出成功", notification.Title);
        Assert.Equal(NotificationType.Success, notification.Type);
    }

    [Fact]
    public async Task ExportProjectDataCommand_ShouldKeepInfoStatusWhenUserCancelsSaveDialog()
    {
        var filePickerService = new FakeFilePickerService();
        var exportService = new FakeProjectDataExportService();
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            filePickerService: filePickerService,
            projectDataExportService: exportService);

        await viewModel.InitializeAsync();
        await viewModel.Import.ExportProjectDataCommand.ExecuteAsync(null);

        Assert.Null(exportService.LastRequest);
        Assert.Equal("已取消导出项目数据。", viewModel.StatusMessage);
        Assert.True(viewModel.Import.ShowImportStatusInfo);
    }

    [Fact]
    public async Task ImportProjectDataPackageFileCommand_ShouldImportPackageFromFile()
    {
        var filePickerService = new FakeFilePickerService
        {
            PickProjectDataPackageFileResult = @"C:\temp\orders.apixpkg.json"
        };
        var exportService = new FakeProjectDataExportService
        {
            PreviewImportResult = ResultModel<ApiImportPreviewDto>.Success(new ApiImportPreviewDto
            {
                DocumentName = "订单项目",
                SourceType = "APIXPKG",
                SourceValue = @"C:\temp\orders.apixpkg.json",
                TotalEndpointCount = 2,
                NewEndpointCount = 2,
                ConflictCount = 0
            }),
            ImportResult = ResultModel<ApiDocumentDto>.Success(new ApiDocumentDto
            {
                Id = "doc-pkg-1",
                ProjectId = "project-1",
                Name = "订单项目",
                SourceType = "APIXPKG",
                SourceValue = @"C:\temp\orders.apixpkg.json",
                ImportedAt = DateTime.UtcNow
            })
        };
        var notificationService = new FakeAppNotificationService();
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            appNotificationService: notificationService,
            filePickerService: filePickerService,
            projectDataExportService: exportService);

        await viewModel.InitializeAsync();
        await viewModel.Import.ImportProjectDataPackageFileCommand.ExecuteAsync(null);

        Assert.Equal("project-1", exportService.LastImportProjectId);
        Assert.Equal(@"C:\temp\orders.apixpkg.json", exportService.LastImportFilePath);
        Assert.Equal("项目数据包导入成功：订单项目", viewModel.StatusMessage);
        Assert.True(viewModel.Import.ShowImportStatusSuccess);
        var notification = Assert.Single(notificationService.Notifications);
        Assert.Equal("项目数据包导入成功", notification.Title);
        Assert.Equal(NotificationType.Success, notification.Type);
    }

    [Fact]
    public async Task ImportProjectDataPackageFileCommand_ShouldKeepInfoStatusWhenUserCancelsPicker()
    {
        var filePickerService = new FakeFilePickerService();
        var exportService = new FakeProjectDataExportService();
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            filePickerService: filePickerService,
            projectDataExportService: exportService);

        await viewModel.InitializeAsync();
        await viewModel.Import.ImportProjectDataPackageFileCommand.ExecuteAsync(null);

        Assert.Null(exportService.LastImportFilePath);
        Assert.Equal("未选择项目数据包文件。", viewModel.StatusMessage);
        Assert.True(viewModel.Import.ShowImportStatusInfo);
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

        await viewModel.Settings.ShowImportDataCommand.ExecuteAsync(null);
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
        await viewModel.Import.LoadImportedDocumentsAsync(manageBusyState: false);

        var interfaceItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "接口 1");
        Assert.NotNull(interfaceItem);

        await viewModel.Catalog.LoadWorkspaceItem(interfaceItem);
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
    public async Task LoadWorkspaceItem_ShouldReuseCleanActiveTabWhenSwitchingHttpInterfaces()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 2);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();
        await viewModel.Import.LoadImportedDocumentsAsync(manageBusyState: false);

        var visibleTabs = viewModel.VisibleWorkspaceTabs;
        var firstInterfaceItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "接口 1");
        var secondInterfaceItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "接口 2");
        Assert.NotNull(firstInterfaceItem);
        Assert.NotNull(secondInterfaceItem);

        await viewModel.Catalog.LoadWorkspaceItem(firstInterfaceItem);
        await viewModel.Catalog.LoadWorkspaceItem(secondInterfaceItem);

        Assert.Same(visibleTabs, viewModel.VisibleWorkspaceTabs);
        var tab = Assert.Single(viewModel.VisibleWorkspaceTabs);
        Assert.Equal("接口 2", tab.HeaderText);
        Assert.Equal("接口 2", viewModel.ActiveWorkspaceTab?.HeaderText);
    }

    [Fact]
    public async Task LoadWorkspaceItem_ShouldOpenNewTabWhenActiveTabHasUnsavedChanges()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 2);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();
        await viewModel.Import.LoadImportedDocumentsAsync(manageBusyState: false);

        var firstInterfaceItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "接口 1");
        var secondInterfaceItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "接口 2");
        Assert.NotNull(firstInterfaceItem);
        Assert.NotNull(secondInterfaceItem);

        await viewModel.Catalog.LoadWorkspaceItem(firstInterfaceItem);
        viewModel.Editor.CurrentHttpInterfaceName = "接口 1 未保存修改";
        Assert.True(viewModel.ActiveWorkspaceTab?.HasUnsavedChanges);

        await viewModel.Catalog.LoadWorkspaceItem(secondInterfaceItem);

        Assert.Equal(2, viewModel.VisibleWorkspaceTabs.Count);
        Assert.Equal("接口 1 未保存修改", viewModel.VisibleWorkspaceTabs[0].HeaderText);
        Assert.Equal("接口 2", viewModel.VisibleWorkspaceTabs[1].HeaderText);
        Assert.Equal("接口 2", viewModel.ActiveWorkspaceTab?.HeaderText);
    }

    [Fact]
    public async Task LoadWorkspaceItem_ShouldCoalesceShellRefreshWhenReusingLandingTab()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local",
        [
            ("默认分组", "创建订单", "POST", "/orders")
        ]);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();
        await viewModel.Import.LoadImportedDocumentsAsync(manageBusyState: false);

        var interfaceItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "创建订单");
        Assert.NotNull(interfaceItem);

        var shellStateChangedCount = 0;
        viewModel.ShellStateChanged += _ => shellStateChangedCount++;

        await viewModel.Catalog.LoadWorkspaceItem(interfaceItem);

        Assert.InRange(shellStateChangedCount, 1, 4);
        Assert.Equal("创建订单", viewModel.Editor.CurrentHttpInterfaceDisplayName);
    }

    [Fact]
    public async Task LoadWorkspaceItem_ShouldShowRequestEditorWorkspaceWhenReusingLandingTab()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 1);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();
        await viewModel.Import.LoadImportedDocumentsAsync(manageBusyState: false);

        Assert.True(viewModel.Shell.ShowInterfaceManagementLanding);
        Assert.False(viewModel.Shell.ShowRequestEditorWorkspace);
        Assert.Equal(ProjectWorkspaceContentMode.Landing, viewModel.Shell.CurrentContentMode);

        var interfaceItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "接口 1");
        Assert.NotNull(interfaceItem);

        await viewModel.Catalog.LoadWorkspaceItem(interfaceItem);

        Assert.NotNull(viewModel.ActiveWorkspaceTab);
        Assert.False(viewModel.ActiveWorkspaceTab!.IsLandingTab);
        Assert.True(viewModel.Shell.ShowRequestEditorWorkspace);
        Assert.False(viewModel.Shell.ShowInterfaceManagementLanding);
        Assert.Equal(ProjectWorkspaceContentMode.RequestEditor, viewModel.Shell.CurrentContentMode);
        Assert.Equal("接口 1", viewModel.Editor.CurrentHttpInterfaceDisplayName);
    }

    [Fact]
    public async Task LoadWorkspaceItem_ShouldOpenEditorBeforeRequestDetailCompletes()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService
        {
            DetailLoadGate = new TaskCompletionSource<bool>()
        };
        requestCaseService.Cases.Add(new RequestCaseDto
        {
            Id = "interface-1",
            ProjectId = "project-1",
            EntryType = ProjectTabRequestEntryTypes.HttpInterface,
            Name = "查询用户",
            GroupName = "接口",
            FolderPath = "用户",
            RequestSnapshot = new RequestSnapshotDto
            {
                EndpointId = "swagger-import:GET /users",
                Name = "查询用户",
                Method = "GET",
                Url = "/users",
                QueryParameters =
                [
                    new RequestKeyValueDto
                    {
                        Name = "page",
                        Value = "1"
                    }
                ]
            },
            HasLoadedDetail = true,
            UpdatedAt = DateTime.UtcNow
        });
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();
        var interfaceItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "查询用户");
        Assert.NotNull(interfaceItem);

        var loadTask = viewModel.Catalog.LoadWorkspaceItem(interfaceItem);

        await loadTask;
        Assert.True(loadTask.IsCompletedSuccessfully);
        Assert.True(viewModel.Shell.ShowRequestEditorWorkspace);
        Assert.Equal(ProjectWorkspaceContentMode.RequestEditor, viewModel.Shell.CurrentContentMode);
        Assert.Equal("/users", viewModel.Editor.RequestUrl);
        Assert.Empty(viewModel.Editor.ConfigTab.QueryParameters);

        requestCaseService.DetailLoadGate.SetResult(true);
        await WaitUntilAsync(() => viewModel.Editor.ConfigTab.QueryParameters.Count == 1);

        var parameter = Assert.Single(viewModel.Editor.ConfigTab.QueryParameters);
        Assert.Equal("page", parameter.Name);
    }

    [Fact]
    public async Task WorkspaceShell_ShouldExposeCurrentContentModeForHistoryAndSettings()
    {
        var requestHistoryService = new FakeRequestHistoryService();
        requestHistoryService.Items.Add(new RequestHistoryItemDto
        {
            Id = "history-1",
            Timestamp = new DateTime(2026, 4, 24, 10, 0, 0, DateTimeKind.Utc),
            HasResponse = true,
            StatusCode = 200,
            DurationMs = 18,
            SizeBytes = 256,
            RequestSnapshot = new RequestSnapshotDto
            {
                Method = "GET",
                Url = "https://demo.local/orders"
            },
            ResponseSnapshot = new ResponseSnapshotDto
            {
                StatusCode = 200,
                DurationMs = 18,
                SizeBytes = 256,
                Content = "{\"items\":[]}"
            }
        });
        var viewModel = CreateViewModel(new FakeApiWorkspaceService(), requestHistoryService: requestHistoryService);
        await viewModel.InitializeAsync();

        Assert.Equal(ProjectWorkspaceContentMode.Landing, viewModel.Shell.CurrentContentMode);

        await viewModel.Shell.ShowRequestHistoryCommand.ExecuteAsync(null);
        Assert.Equal(ProjectWorkspaceContentMode.RequestHistory, viewModel.Shell.CurrentContentMode);
        Assert.Equal(1, requestHistoryService.GetHistoryCallCount);
        Assert.Equal(0, requestHistoryService.GetDetailCallCount);
        Assert.Single(viewModel.RequestHistory);
        Assert.Null(viewModel.RequestHistory[0].ResponseSnapshot);

        viewModel.Shell.ShowInterfaceManagementCommand.Execute(null);
        await viewModel.Shell.ShowRequestHistoryCommand.ExecuteAsync(null);

        Assert.Equal(1, requestHistoryService.GetHistoryCallCount);

        viewModel.Settings.OpenWorkspaceCommand.Execute(null);
        Assert.Equal(ProjectWorkspaceContentMode.ProjectSettings, viewModel.Shell.CurrentContentMode);
    }

    [Fact]
    public async Task LoadHistoryRequestAsync_ShouldLoadDetailOnDemand()
    {
        var requestHistoryService = new FakeRequestHistoryService();
        requestHistoryService.Items.Add(new RequestHistoryItemDto
        {
            Id = "history-1",
            Timestamp = new DateTime(2026, 4, 24, 10, 0, 0, DateTimeKind.Utc),
            HasResponse = true,
            StatusCode = 200,
            DurationMs = 18,
            SizeBytes = 256,
            RequestSnapshot = new RequestSnapshotDto
            {
                Method = "GET",
                Url = "https://demo.local/orders"
            },
            ResponseSnapshot = new ResponseSnapshotDto
            {
                StatusCode = 200,
                DurationMs = 18,
                SizeBytes = 256,
                Content = "{\"items\":[]}"
            }
        });
        var viewModel = CreateViewModel(new FakeApiWorkspaceService(), requestHistoryService: requestHistoryService);
        await viewModel.InitializeAsync();
        await viewModel.Shell.ShowRequestHistoryCommand.ExecuteAsync(null);

        var historyItem = Assert.Single(viewModel.RequestHistory);
        Assert.Null(historyItem.ResponseSnapshot);

        await viewModel.LoadHistoryRequestAsync(historyItem);

        Assert.Equal(1, requestHistoryService.GetDetailCallCount);
        Assert.NotNull(historyItem.ResponseSnapshot);
        Assert.True(viewModel.ResponseSection.HasResponse);
        Assert.Contains("\"items\":[]", viewModel.ResponseSection.BodyText);
    }

    [Fact]
    public void RequestEditor_ShouldExposeCurrentContentModeWhenSwitchingEditorModes()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());

        Assert.Equal(RequestEditorContentMode.None, viewModel.Editor.CurrentContentMode);

        viewModel.Workspace.OpenQuickRequestWorkspaceCommand.Execute(null);
        Assert.Equal(RequestEditorContentMode.QuickRequest, viewModel.Editor.CurrentContentMode);

        viewModel.Workspace.OpenHttpInterfaceWorkspaceCommand.Execute(null);
        Assert.Equal(RequestEditorContentMode.HttpWorkbench, viewModel.Editor.CurrentContentMode);

        viewModel.Editor.ShowHttpDocumentPreviewModeCommand.Execute(null);
        Assert.Equal(RequestEditorContentMode.HttpDocumentPreview, viewModel.Editor.CurrentContentMode);

        viewModel.Editor.ShowHttpDebugEditorModeCommand.Execute(null);
        Assert.Equal(RequestEditorContentMode.HttpWorkbench, viewModel.Editor.CurrentContentMode);
    }

    [Fact]
    public async Task SendRequestCommand_ShouldSendQuickRequestWithAbsoluteUrl()
    {
        var requestExecutionService = new FakeRequestExecutionService();
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            requestExecutionService: requestExecutionService);
        const string requestUrl = "http://172.16.127.100:37797/be/notification/notification/negotiate";

        viewModel.Workspace.OpenQuickRequestWorkspaceCommand.Execute(null);
        viewModel.Editor.RequestUrl = requestUrl;

        Assert.True(viewModel.SendRequestCommand.CanExecute(null));

        await viewModel.SendRequestCommand.ExecuteAsync(null);

        Assert.Equal(1, requestExecutionService.SendCallCount);
        Assert.NotNull(requestExecutionService.LastRequest);
        Assert.Equal(requestUrl, requestExecutionService.LastRequest!.Url);
        Assert.Equal("快捷请求发送完成。", viewModel.StatusMessage);
    }

    [Fact]
    public async Task SendRequestCommand_ShouldShowResponseLoadingWhileRequestIsPending()
    {
        var requestExecutionService = new FakeRequestExecutionService
        {
            PendingSendResult = new TaskCompletionSource<IResultModel<ResponseSnapshotDto>>(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            requestExecutionService: requestExecutionService);

        viewModel.Workspace.OpenQuickRequestWorkspaceCommand.Execute(null);
        viewModel.Editor.RequestUrl = "https://demo.local/orders";

        var sendTask = viewModel.SendRequestCommand.ExecuteAsync(null);

        Assert.True(SpinWait.SpinUntil(
            () => requestExecutionService.SendCallCount == 1 && viewModel.ResponseSection.IsLoading,
            TimeSpan.FromSeconds(1)));
        Assert.False(viewModel.ResponseSection.ShowPlaceholder);
        Assert.Contains("快捷请求", viewModel.ResponseSection.LoadingText);

        requestExecutionService.PendingSendResult.SetResult(ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto
        {
            StatusCode = 204,
            DurationMs = 31,
            SizeBytes = 0
        }));
        await sendTask;

        Assert.False(viewModel.ResponseSection.IsLoading);
        Assert.True(viewModel.ResponseSection.HasResponse);
        Assert.Equal("HTTP 204", viewModel.ResponseSection.StatusText);
    }

    [Fact]
    public async Task ConfirmQuickRequestSaveAsync_ShouldKeepExistingInterfaceTreeNodesStable()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 1);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();
        await viewModel.Import.LoadImportedDocumentsAsync(manageBusyState: false);

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
        await viewModel.Import.LoadImportedDocumentsAsync(manageBusyState: false);

        var originalFolder = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "默认分组 (1)");
        var originalInterface = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "接口 1");
        Assert.NotNull(originalFolder);
        Assert.NotNull(originalInterface);

        await viewModel.Catalog.LoadWorkspaceItem(originalInterface);
        viewModel.Editor.CurrentHttpInterfaceName = "接口 1 已编辑";

        await viewModel.Workflow.SaveCurrentEditorAsync();

        var currentFolder = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "默认分组 (1)");
        var currentInterface = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "接口 1 已编辑");
        Assert.Same(originalFolder, currentFolder);
        Assert.Same(originalInterface, currentInterface);
        Assert.Equal("接口 1 已编辑", currentInterface!.Title);
    }

    [Fact]
    public async Task SaveHttpCaseAsync_ShouldCreateCaseUnderInterfaceNode()
    {
        var requestCaseService = new FakeRequestCaseService();
        var viewModel = CreateViewModel(new FakeApiWorkspaceService(), requestCaseService);

        await viewModel.InitializeAsync();

        viewModel.Workspace.OpenHttpInterfaceWorkspaceCommand.Execute(null);
        viewModel.Editor.CurrentHttpInterfaceName = "创建订单";
        viewModel.Editor.SelectedMethod = "POST";
        viewModel.Editor.RequestUrl = "/orders";
        viewModel.Editor.CurrentInterfaceFolderPath = "订单";
        viewModel.Editor.CurrentHttpCaseName = "成功响应";

        await viewModel.Workflow.SaveHttpCaseAsync();

        var savedInterface = Assert.Single(requestCaseService.Cases, item => item.EntryType == ProjectTabRequestEntryTypes.HttpInterface);
        var savedCase = Assert.Single(requestCaseService.Cases, item => item.EntryType == ProjectTabRequestEntryTypes.HttpCase);

        Assert.Equal(savedInterface.Id, savedCase.ParentId);
        Assert.Equal("订单", savedCase.FolderPath);

        var folderItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "订单 (1)");
        var interfaceItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "创建订单 (1)");
        var caseItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "成功响应");

        Assert.NotNull(folderItem);
        Assert.NotNull(interfaceItem);
        Assert.NotNull(caseItem);
        Assert.Single(interfaceItem!.Children);
        Assert.Same(caseItem, interfaceItem.Children[0]);
    }

    [Fact]
    public async Task LoadWorkspaceItem_ShouldLoadRequestCaseDetailOnDemand()
    {
        var requestCaseService = new FakeRequestCaseService();
        requestCaseService.Cases.Add(new RequestCaseDto
        {
            Id = "case-1",
            ProjectId = "project-1",
            EntryType = ProjectTabRequestEntryTypes.HttpInterface,
            Name = "创建订单",
            GroupName = "接口",
            FolderPath = "订单",
            Description = "创建订单接口",
            RequestSnapshot = new RequestSnapshotDto
            {
                EndpointId = "swagger-import:POST /orders",
                Method = "POST",
                Url = "/orders",
                BodyMode = BodyModes.RawJson,
                BodyContent = "{\"customerId\":1,\"amount\":128}"
            },
            UpdatedAt = DateTime.UtcNow
        });
        var viewModel = CreateViewModel(new FakeApiWorkspaceService(), requestCaseService);

        await viewModel.InitializeAsync();
        await viewModel.Catalog.ReloadSavedRequestsAsync();

        var folderItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "订单 (1)");
        Assert.NotNull(folderItem);
        folderItem!.IsExpanded = true;

        var interfaceItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "创建订单");
        Assert.NotNull(interfaceItem);
        Assert.Equal(0, requestCaseService.GetDetailCallCount);

        await viewModel.Catalog.LoadWorkspaceItem(interfaceItem);
        await WaitUntilAsync(() => requestCaseService.GetDetailCallCount == 1);

        Assert.Equal(BodyModes.RawJson, viewModel.ActiveWorkspaceTab!.ConfigTab.SelectedBodyMode);
        Assert.Equal("{\"customerId\":1,\"amount\":128}", viewModel.ActiveWorkspaceTab.ConfigTab.RequestBody);
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
        await viewModel.Import.LoadImportedDocumentsAsync(manageBusyState: false);

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
        await viewModel.Import.LoadImportedDocumentsAsync(manageBusyState: false);

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
        await viewModel.Import.LoadImportedDocumentsAsync(manageBusyState: false);

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
    public async Task ProjectSettingsCommands_ShouldSwitchBetweenOverviewImportAndExportSections()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());

        viewModel.Settings.OpenWorkspaceCommand.Execute(null);
        viewModel.Settings.ShowOverviewCommand.Execute(null);

        Assert.True(viewModel.Shell.IsProjectSettingsSection);
        Assert.True(viewModel.Settings.IsOverviewSelected);
        Assert.False(viewModel.Settings.IsImportDataSelected);
        Assert.Equal("基本设置", viewModel.Settings.CurrentTitle);

        await viewModel.Settings.ShowImportDataCommand.ExecuteAsync(null);

        Assert.True(viewModel.Shell.IsProjectSettingsSection);
        Assert.False(viewModel.Settings.IsOverviewSelected);
        Assert.True(viewModel.Settings.IsImportDataSelected);
        Assert.False(viewModel.Settings.IsExportDataSelected);
        Assert.Equal("导入数据", viewModel.Settings.CurrentTitle);

        viewModel.Settings.ShowExportDataCommand.Execute(null);

        Assert.True(viewModel.Shell.IsProjectSettingsSection);
        Assert.False(viewModel.Settings.IsOverviewSelected);
        Assert.False(viewModel.Settings.IsImportDataSelected);
        Assert.True(viewModel.Settings.IsExportDataSelected);
        Assert.Equal("导出数据", viewModel.Settings.CurrentTitle);

        viewModel.Settings.OpenWorkspaceCommand.Execute(null);

        Assert.True(viewModel.Shell.IsProjectSettingsSection);
        Assert.True(viewModel.Settings.IsOverviewSelected);
        Assert.False(viewModel.Settings.IsImportDataSelected);
        Assert.False(viewModel.Settings.IsExportDataSelected);
        Assert.Equal("基本设置", viewModel.Settings.CurrentTitle);
    }

    [Fact]
    public async Task ConfirmClearProjectDataCommand_ShouldClearProjectAndReloadWorkspaceState()
    {
        var systemDataService = new FakeSystemDataService();
        var requestCaseService = new FakeRequestCaseService();
        requestCaseService.Cases.Add(new RequestCaseDto
        {
            Id = "case-1",
            ProjectId = "project-1",
            EntryType = ProjectTabRequestEntryTypes.QuickRequest,
            Name = "待清空请求",
            GroupName = "默认分组",
            RequestSnapshot = new RequestSnapshotDto
            {
                Method = "GET",
                Url = "/before-clear"
            },
            UpdatedAt = DateTime.UtcNow
        });
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            requestCaseService,
            systemDataService: systemDataService);

        await viewModel.InitializeAsync();
        Assert.NotEmpty(viewModel.SavedRequests);

        viewModel.Settings.RequestClearProjectDataCommand.Execute(null);
        Assert.True(viewModel.Settings.IsClearProjectDataConfirmDialogOpen);

        requestCaseService.Cases.Clear();
        await viewModel.Settings.ConfirmClearProjectDataCommand.ExecuteAsync(null);

        Assert.Equal(1, systemDataService.ClearProjectCallCount);
        Assert.Equal("project-1", systemDataService.LastClearedProjectId);
        Assert.False(viewModel.Settings.IsClearProjectDataConfirmDialogOpen);
        Assert.Empty(viewModel.SavedRequests);
        Assert.True(viewModel.Settings.IsOverviewSelected);
        Assert.Contains("数据已清空", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ConfirmDeleteProjectCommand_ShouldDeleteProjectAndNotifyHost()
    {
        var projectWorkspaceService = new FakeProjectWorkspaceService();
        var deletedProjectId = string.Empty;
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            projectWorkspaceService: projectWorkspaceService,
            handleProjectDeletedAsync: projectId =>
            {
                deletedProjectId = projectId;
                return Task.CompletedTask;
            });

        viewModel.Settings.RequestDeleteProjectCommand.Execute(null);
        Assert.True(viewModel.Settings.IsDeleteProjectConfirmDialogOpen);

        await viewModel.Settings.ConfirmDeleteProjectCommand.ExecuteAsync(null);

        Assert.Equal(1, projectWorkspaceService.DeleteCallCount);
        Assert.Equal("project-1", projectWorkspaceService.LastDeletedProjectId);
        Assert.Equal("project-1", deletedProjectId);
        Assert.False(viewModel.Settings.IsDeleteProjectConfirmDialogOpen);
        Assert.False(viewModel.Settings.IsProjectDangerOperationBusy);
    }

    [Fact]
    public void SelectingWorkspaceNavigationItem_ShouldSwitchWorkspaceSection()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());
        var projectSettingsItem = Assert.Single(viewModel.Shell.NavigationItems, item => item.SectionKey == "project-settings");

        viewModel.Shell.SelectedNavigationItem = projectSettingsItem;

        Assert.True(viewModel.Shell.IsProjectSettingsSection);
        Assert.Equal(projectSettingsItem, viewModel.Shell.SelectedNavigationItem);
    }

    [Fact]
    public void ExecutingProjectSettingsNavCommand_ShouldSwitchWorkspaceSection()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());
        var projectSettingsItem = Assert.Single(viewModel.Shell.NavigationItems, item => item.SectionKey == "project-settings");

        projectSettingsItem.Command.Execute(null);

        Assert.True(viewModel.Shell.IsProjectSettingsSection);
        Assert.Equal(projectSettingsItem, viewModel.Shell.SelectedNavigationItem);
        Assert.True(projectSettingsItem.IsSelected);
    }

    [Fact]
    public void Dispose_ShouldStopProjectChangeFromRaisingShellState()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());
        var shellStateChangedCount = 0;
        viewModel.ShellStateChanged += _ => shellStateChangedCount++;

        viewModel.Project.Name = "测试项目 V2";
        Assert.True(shellStateChangedCount > 0);
        var countBeforeDispose = shellStateChangedCount;

        viewModel.Dispose();
        viewModel.Project.Name = "测试项目 V3";

        Assert.Equal(countBeforeDispose, shellStateChangedCount);
        Assert.Empty(viewModel.WorkspaceTabs);
        Assert.Null(viewModel.ActiveWorkspaceTab);
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());

        viewModel.Dispose();
        var exception = Record.Exception(viewModel.Dispose);

        Assert.Null(exception);
        Assert.Empty(viewModel.WorkspaceTabs);
        Assert.Null(viewModel.ActiveWorkspaceTab);
    }
}
