using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls.Notifications;
using ApixPress.App;
using FakeRequestCaseService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeRequestCaseService;
using FakeAppNotificationService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeAppNotificationService;
using FakeEnvironmentVariableService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeEnvironmentVariableService;
using FakeFilePickerService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeFilePickerService;
using FakeProjectDataExportService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeProjectDataExportService;
using FakeProjectHttpSettingsService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeProjectHttpSettingsService;
using FakeProjectWorkspaceService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeProjectWorkspaceService;
using FakeRequestExecutionService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeRequestExecutionService;
using FakeRequestHistoryService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeRequestHistoryService;
using FakeSystemDataService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeSystemDataService;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels;
using ApixPress.App.Views.Controls;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.Results;

namespace ApixPress.App.Tests.ViewModels;

public sealed partial class ProjectTabViewModelTests
{
    private static bool _isAvaloniaInitialized;

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
    public async Task RequestCaseDelivery_ShouldAutoSelectParentInterfaceWhenCaseIsSelected()
    {
        var exportService = new FakeProjectDataExportService
        {
            DeliveryOptions = new RequestCaseDeliveryOptionsDto
            {
                Interfaces =
                [
                    new RequestCaseDeliveryCandidateDto
                    {
                        Id = "interface-1", EntryType = "http-interface", Name = "订单接口", Method = "GET", Url = "/orders"
                    }
                ],
                RequestCases =
                [
                    new RequestCaseDeliveryCandidateDto
                    {
                        Id = "case-1", EntryType = "http-case", ParentId = "interface-1", Name = "成功", Method = "GET", Url = "/orders"
                    }
                ],
                EnvironmentCount = 1,
                VariableCount = 2
            }
        };
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            projectDataExportService: exportService,
            filePickerService: new FakeFilePickerService
            {
                SaveRequestCaseDeliveryFileResult = Path.Combine(Path.GetTempPath(), "request-delivery-test.apixdelivery.json")
            });

        await viewModel.InitializeAsync();
        await viewModel.Import.Delivery.OpenExportCommand.ExecuteAsync(null);

        var requestCase = Assert.Single(viewModel.Import.Delivery.RequestCases);
        var parentInterface = Assert.Single(viewModel.Import.Delivery.Interfaces);
        requestCase.IsSelected = true;

        Assert.True(parentInterface.IsSelected);
        Assert.True(parentInterface.IsAutoIncluded);
        Assert.Equal(1, viewModel.Import.Delivery.SelectedInterfaceCount);
        Assert.Equal(1, viewModel.Import.Delivery.SelectedRequestCaseCount);
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
        await Task.Delay(300);
        var nameMatchTitles = FlattenExplorerTitles(viewModel.Catalog.InterfaceCatalogItems).ToList();

        Assert.Contains("用户 (1)", nameMatchTitles);
        Assert.Contains("获取用户详情", nameMatchTitles);
        Assert.DoesNotContain("查询订单列表", nameMatchTitles);

        viewModel.Catalog.InterfaceSearchText = "/orders";
        await Task.Delay(300);
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
    public async Task InterfaceCatalog_ShouldBeExpandedByDefault()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());

        await viewModel.InitializeAsync();

        Assert.True(viewModel.Catalog.IsInterfaceCatalogExpanded);
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
                OutputPath = @"C:\temp\project-export.apixpkg.json",
                Format = ProjectDataExportFormat.Backup,
                InterfaceCount = 2,
                TestCaseCount = 3,
                FileCount = 1
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
        Assert.Equal("项目数据导出成功：2 个接口、3 个请求用例、0 个环境、0 条请求历史，已保存到 project-export.apixpkg.json", viewModel.StatusMessage);
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
    public void SelectExportFormatCommand_ShouldSwitchPanelsAndKeepScopeFlags()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());

        Assert.True(viewModel.Import.IsBackupExportFormat);
        Assert.Equal("导出项目数据", viewModel.Import.ExportActionButtonText);
        viewModel.Import.ExportIncludeRequestHistory = true;

        viewModel.Import.SelectExportFormatCommand.Execute("markdown");

        Assert.True(viewModel.Import.IsMarkdownExportFormat);
        Assert.False(viewModel.Import.IsBackupExportFormat);
        Assert.False(viewModel.Import.IsHtmlExportFormat);
        Assert.Equal("导出 Markdown", viewModel.Import.ExportActionButtonText);
        // 切换格式保留四项范围勾选，仅备份面板展示它们
        Assert.True(viewModel.Import.ExportIncludeRequestHistory);

        viewModel.Import.SelectExportFormatCommand.Execute("html");
        Assert.True(viewModel.Import.IsHtmlExportFormat);
        Assert.Equal("导出 HTML", viewModel.Import.ExportActionButtonText);
    }

    [Fact]
    public async Task ExportProjectDataCommand_Markdown_ShouldUseDirectoryPickerAndDirectoryRequest()
    {
        var parentDirectory = Path.Combine(Path.GetTempPath(), $"md-export-{Guid.NewGuid():N}");
        var filePickerService = new FakeFilePickerService
        {
            PickStorageDirectoryResult = parentDirectory
        };
        var exportService = new FakeProjectDataExportService
        {
            Result = ResultModel<ProjectDataExportResultDto>.Success(new ProjectDataExportResultDto
            {
                OutputPath = Path.Combine(parentDirectory, "测试项目-docs"),
                Format = ProjectDataExportFormat.Markdown,
                InterfaceCount = 1,
                FileCount = 2
            })
        };
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            filePickerService: filePickerService,
            projectDataExportService: exportService);

        await viewModel.InitializeAsync();
        viewModel.Import.SelectExportFormatCommand.Execute("markdown");
        await viewModel.Import.ExportProjectDataCommand.ExecuteAsync(null);

        Assert.NotNull(exportService.LastRequest);
        Assert.Equal(ProjectDataExportFormat.Markdown, exportService.LastRequest!.Format);
        Assert.Equal(parentDirectory, exportService.LastRequest.OutputDirectoryPath);
        Assert.Equal(string.Empty, exportService.LastRequest.OutputFilePath);
        Assert.Contains("Markdown 文档导出成功", viewModel.StatusMessage);
    }

    [Fact]
    public async Task InitializeAsync_ShouldRestorePersistedExportPreferences()
    {
        var exportService = new FakeProjectDataExportService
        {
            LoadSettingsResult = new ProjectDataExportSettingsDto
            {
                Format = ProjectDataExportFormats.Html,
                IncludeInterfaces = true,
                IncludeRequestCases = false,
                IncludeEnvironments = true,
                IncludeRequestHistory = true,
                LastOutputDirectory = @"C:\temp"
            }
        };
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            projectDataExportService: exportService);

        await viewModel.InitializeAsync();

        Assert.True(viewModel.Import.IsHtmlExportFormat);
        Assert.False(viewModel.Import.ExportIncludeRequestCases);
        Assert.True(viewModel.Import.ExportIncludeRequestHistory);
    }

    [Fact]
    public async Task ExportProjectDataCommand_ShouldSavePreferencesEvenWhenUserCancels()
    {
        var filePickerService = new FakeFilePickerService();
        var exportService = new FakeProjectDataExportService();
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            filePickerService: filePickerService,
            projectDataExportService: exportService);

        await viewModel.InitializeAsync();
        viewModel.Import.SelectExportFormatCommand.Execute("html");
        viewModel.Import.ExportIncludeRequestCases = false;
        await viewModel.Import.ExportProjectDataCommand.ExecuteAsync(null);

        // 取消选择器不算失败，但格式与四项范围已经落库
        Assert.Null(exportService.LastRequest);
        Assert.Contains(exportService.SavedSettings, saved =>
            saved.Settings.Format == ProjectDataExportFormats.Html && !saved.Settings.IncludeRequestCases);
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
        var dialogService = new ViewModelSharedTestDoubles.FakeDialogService();
        string? summaryAtConfirm = null;
        string? conflictDetailAtConfirm = null;
        dialogService.OnShow = request =>
        {
            var confirmContext = Assert.IsType<ImportOverwriteConfirmDialogViewModel>(request.DataContext);
            summaryAtConfirm = confirmContext.Import.PendingImportOverwriteSummary;
            conflictDetailAtConfirm = confirmContext.Import.PendingImportOverwriteDetailText;
        };
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService, dialogService: dialogService);
        await viewModel.InitializeAsync();

        await viewModel.Settings.ShowImportDataCommand.ExecuteAsync(null);
        viewModel.Import.ImportUrl = "https://demo.local/swagger.json";

        await viewModel.Import.ImportSwaggerUrlCommand.ExecuteAsync(null);

        // 冲突时弹出覆盖确认弹窗（独立上下文），替身按脚本返回“确认更新”
        var request = Assert.Single(dialogService.Requests);
        Assert.Equal(typeof(ImportOverwriteConfirmDialogView), request.ViewType);
        Assert.Contains("更新", summaryAtConfirm);
        Assert.Contains("GET /orders", conflictDetailAtConfirm);

        Assert.Equal("1", viewModel.Import.ImportedApiDocumentCountText);
        Assert.Contains(requestCaseService.Cases, item => item.Name == "查询订单列表" && item.RequestSnapshot.Url == "/orders");
        Assert.DoesNotContain(requestCaseService.Cases, item => item.Name == "旧的查询订单列表");
    }

    [Fact]
    public async Task ImportSwaggerUrlCommand_ShouldKeepConflictingInterfaceWhenUserChoosesSkip()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "旧文档", "FILE", @"C:\temp\legacy-swagger.json", "https://legacy.demo.local",
        [
            ("订单", "旧的查询订单列表", "GET", "/orders")
        ]);
        requestCaseService.Cases.Add(new RequestCaseDto
        {
            Id = "existing-orders",
            ProjectId = "project-1",
            EntryType = "http-interface",
            Name = "旧的查询订单列表",
            GroupName = "接口",
            FolderPath = "订单",
            RequestSnapshot = new RequestSnapshotDto
            {
                EndpointId = "swagger-import:GET /orders",
                Method = "GET",
                Url = "/orders"
            },
            UpdatedAt = DateTime.UtcNow
        });
        var dialogService = new ViewModelSharedTestDoubles.FakeDialogService
        {
            OnShow = request =>
            {
                var confirmContext = Assert.IsType<ImportOverwriteConfirmDialogViewModel>(request.DataContext);
                confirmContext.Import.ChooseSkipSameNameInterfacesCommand.Execute(null);
            }
        };
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService, dialogService: dialogService);
        await viewModel.InitializeAsync();

        await viewModel.Settings.ShowImportDataCommand.ExecuteAsync(null);
        viewModel.Import.ImportUrl = "https://demo.local/swagger.json";

        await viewModel.Import.ImportSwaggerUrlCommand.ExecuteAsync(null);

        Assert.Contains(requestCaseService.Cases, item =>
            item.Id == "existing-orders"
            && item.Name == "旧的查询订单列表"
            && item.RequestSnapshot.EndpointId == "swagger-import:GET /orders");
        Assert.Contains(requestCaseService.Cases, item =>
            item.EntryType == "http-interface"
            && item.RequestSnapshot.Method == "POST"
            && item.RequestSnapshot.Url == "/orders");
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
        Assert.Equal("已保存", viewModel.Editor.CurrentHttpSaveStateText);
        Assert.False(viewModel.Editor.HasCurrentHttpUnsavedChanges);
        viewModel.Editor.CurrentHttpInterfaceName = "接口 1 已编辑";
        Assert.True(viewModel.ActiveWorkspaceTab?.HasUnsavedChanges);
        Assert.Equal("有未保存修改", viewModel.Editor.CurrentHttpSaveStateText);
        Assert.True(viewModel.Editor.HasCurrentHttpUnsavedChanges);

        await viewModel.Workflow.SaveCurrentEditorAsync();
        Assert.Equal("已保存", viewModel.Editor.CurrentHttpSaveStateText);
        Assert.False(viewModel.Editor.HasCurrentHttpUnsavedChanges);

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
    public async Task LoadWorkspaceItem_ShouldShowRouteNameForUnnamedSavedInterface()
    {
        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "osp 服务", "FILE", @"C:\temp\osp.json", "https://osp.demo.local", 1);
        // 模拟保存时未显式命名的接口：保存口径会把生成名（方法+路径）落为接口名
        requestCaseService.Cases.Add(new RequestCaseDto
        {
            Id = "case-unnamed-home",
            ProjectId = "project-1",
            EntryType = "http-interface",
            Name = "GET /home",
            GroupName = "Home",
            RequestSnapshot = new RequestSnapshotDto { Method = "GET", Url = "/home" },
            UpdatedAt = DateTime.UtcNow
        });
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();

        var interfaceItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "GET /home");
        Assert.NotNull(interfaceItem);

        await viewModel.Catalog.LoadWorkspaceItem(interfaceItem);

        // 编辑器名称框须与侧栏树、工作区标签一致显示路由名，而非"未命名接口"
        Assert.Equal("GET /home", viewModel.Editor.CurrentHttpInterfaceName);
        Assert.Equal("GET /home", viewModel.ActiveWorkspaceTab?.HeaderText);
    }

    [Fact]
    public async Task UnnamedNewInterface_ShouldFollowMethodAndUrlAsName()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());

        await viewModel.InitializeAsync();
        viewModel.Workspace.OpenHttpInterfaceWorkspaceCommand.Execute(null);
        Assert.Equal("新建 HTTP 接口", viewModel.Editor.CurrentHttpInterfaceName);

        viewModel.Editor.RequestUrl = "/home";

        // 未显式命名时，名称随方法+路径实时生成（保存口径一致），不再停留"未命名接口"
        Assert.Equal("GET /home", viewModel.Editor.CurrentHttpInterfaceName);

        viewModel.Editor.CurrentHttpInterfaceName = "首页";

        // 用户显式命名后以用户名为准
        Assert.Equal("首页", viewModel.Editor.CurrentHttpInterfaceName);
    }

    [Fact]
    public async Task LoadWorkspaceItem_ShouldNotGenerateRequestCodeUntilDialogOpens()
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

        Assert.Equal(RequestEditorContentMode.HttpWorkbench, viewModel.Editor.CurrentContentMode);
        Assert.True(viewModel.Editor.ShowHttpWorkbenchContent);
        Assert.Equal(string.Empty, viewModel.Editor.RequestCodeCurlCommand);

        await viewModel.Editor.OpenRequestCodeDialogCommand.ExecuteAsync(null);

        Assert.Contains("curl --request GET", viewModel.Editor.RequestCodeCurlCommand);
        Assert.Contains("/endpoint-1", viewModel.Editor.RequestCodeCurlCommand);
    }

    [Fact]
    public async Task CopyRequestCode_ShouldCopySelectedLanguageAndTabTextWithHintFeedback()
    {
        var clipboard = new ViewModelSharedTestDoubles.FakeClipboardService();
        var viewModel = CreateViewModel(new FakeApiWorkspaceService(), clipboardService: clipboard);
        var editor = viewModel.Editor;
        editor.RequestCodeCurlCommand = "curl-demo";
        editor.RequestCodeWgetCommand = "wget-demo";
        editor.RequestCodePowerShellCommand = "ps-demo";
        editor.RequestCodeCSharpCommand = "cs-demo";

        // 默认 Shell 语言 + curl 页签
        Assert.Equal("curl-demo", editor.ResolveCurrentRequestCodeText());
        await editor.CopyRequestCodeCommand.ExecuteAsync(null);
        Assert.Equal("curl-demo", clipboard.CopiedTexts[0]);
        Assert.Equal("已复制到剪贴板", editor.RequestCodeCopiedHint);

        // 切页签清除提示并切换复制目标
        editor.SelectedRequestCodeTab = 2;
        Assert.Equal(string.Empty, editor.RequestCodeCopiedHint);
        await editor.CopyRequestCodeCommand.ExecuteAsync(null);
        Assert.Equal(2, clipboard.CopiedTexts.Count);
        Assert.Equal("ps-demo", clipboard.CopiedTexts[1]);

        // 切语言复制 C#，切语言同样清除提示
        editor.ShowCSharpRequestCodeLanguageCommand.Execute(null);
        Assert.Equal(string.Empty, editor.RequestCodeCopiedHint);
        await editor.CopyRequestCodeCommand.ExecuteAsync(null);
        Assert.Equal(3, clipboard.CopiedTexts.Count);
        Assert.Equal("cs-demo", clipboard.CopiedTexts[2]);

        // 复制失败走失败提示，不静默
        clipboard.ShouldSucceed = false;
        await editor.CopyRequestCodeCommand.ExecuteAsync(null);
        Assert.Equal("复制失败，请手动选择代码复制。", editor.RequestCodeCopiedHint);
    }

    [Fact]
    public async Task Settings_OpenRenameDialog_ShouldPrefillCurrentNameAndSaveThroughService()
    {
        var projectWorkspaceService = new ViewModelSharedTestDoubles.FakeProjectWorkspaceService
        {
            ProjectsResult =
            [
                new ProjectWorkspaceDto { Id = "project-1", Name = "旧名", Description = "旧简介", IsDefault = true }
            ]
        };
        var dialogService = new ViewModelSharedTestDoubles.FakeDialogService();
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            projectWorkspaceService: projectWorkspaceService,
            dialogService: dialogService);

        await viewModel.Settings.OpenRenameProjectDialogCommand.ExecuteAsync(null);

        var dialog = Assert.IsType<ProjectInfoEditDialogViewModel>(
            Assert.Single(dialogService.Requests).DataContext);
        Assert.Equal("编辑项目名称", dialog.Title);
        Assert.Equal("测试项目", dialog.Value);

        dialog.Value = "新名字";
        await dialog.SaveCommand.ExecuteAsync(null);

        // 保存按全量口径回写（IsDefault 保留），并同步设置页行展示
        Assert.NotNull(projectWorkspaceService.LastSavedProject);
        Assert.Equal("新名字", projectWorkspaceService.LastSavedProject!.Name);
        Assert.Equal("旧简介", projectWorkspaceService.LastSavedProject.Description);
        Assert.True(projectWorkspaceService.LastSavedProject.IsDefault);
        Assert.Equal("新名字", viewModel.Project.Name);
    }

    [Fact]
    public async Task Settings_DescriptionSave_ShouldKeepNameAndSyncLocalDescription()
    {
        var projectWorkspaceService = new ViewModelSharedTestDoubles.FakeProjectWorkspaceService
        {
            ProjectsResult =
            [
                new ProjectWorkspaceDto { Id = "project-1", Name = "测试项目", Description = "旧简介", IsDefault = false }
            ]
        };
        var dialogService = new ViewModelSharedTestDoubles.FakeDialogService();
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            projectWorkspaceService: projectWorkspaceService,
            dialogService: dialogService);

        await viewModel.Settings.OpenEditProjectDescriptionDialogCommand.ExecuteAsync(null);

        var dialog = Assert.IsType<ProjectInfoEditDialogViewModel>(
            Assert.Single(dialogService.Requests).DataContext);
        Assert.Equal("编辑项目简介", dialog.Title);
        Assert.Equal("用于验证项目设置数据管理", dialog.Value);

        dialog.Value = "新简介";
        await dialog.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(projectWorkspaceService.LastSavedProject);
        Assert.Equal("测试项目", projectWorkspaceService.LastSavedProject!.Name);
        Assert.Equal("新简介", projectWorkspaceService.LastSavedProject.Description);
        Assert.Equal("新简介", viewModel.Project.Description);
    }

    [Fact]
    public async Task Settings_SaveFailure_ShouldSurfaceMessageAndNotSyncLocal()
    {
        var projectWorkspaceService = new ViewModelSharedTestDoubles.FakeProjectWorkspaceService
        {
            SaveShouldFail = true,
            ProjectsResult =
            [
                new ProjectWorkspaceDto { Id = "project-1", Name = "测试项目", Description = string.Empty, IsDefault = false }
            ]
        };
        var dialogService = new ViewModelSharedTestDoubles.FakeDialogService();
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            projectWorkspaceService: projectWorkspaceService,
            dialogService: dialogService);

        await viewModel.Settings.OpenRenameProjectDialogCommand.ExecuteAsync(null);

        var dialog = Assert.IsType<ProjectInfoEditDialogViewModel>(
            Assert.Single(dialogService.Requests).DataContext);
        var closed = false;
        dialog.RequestClose += (_, _) => closed = true;

        dialog.Value = "新名字";
        await dialog.SaveCommand.ExecuteAsync(null);

        Assert.False(closed);
        Assert.Equal("项目名称不能为空。", dialog.ErrorMessage);
        Assert.Equal("测试项目", viewModel.Project.Name);
    }

    [Fact]
    public async Task UseCasesPanel_RenameCase_ShouldUpdateNameAndRefreshDrawer()
    {
        var requestCaseService = new FakeRequestCaseService();
        var dialogService = new ViewModelSharedTestDoubles.FakeDialogService();
        var viewModel = CreateViewModel(new FakeApiWorkspaceService(), requestCaseService, dialogService: dialogService);
        var panel = viewModel.UseCasesPanel;
        panel.SetProjectContext("project-1");
        panel.UpsertCaseItem(new RequestCaseDto
        {
            Id = "case-1",
            ProjectId = "project-1",
            EntryType = "http-case",
            Name = "旧用例名",
            RequestSnapshot = new RequestSnapshotDto { Method = "GET", Url = "/get" },
            HasLoadedDetail = true,
            UpdatedAt = DateTime.UtcNow
        });
        var item = panel.RequestCases.Single();
        Assert.Equal("旧用例名", item.Name);

        await panel.RenameCaseCommand.ExecuteAsync(item);

        var dialog = Assert.IsType<ProjectInfoEditDialogViewModel>(
            Assert.Single(dialogService.Requests).DataContext);
        Assert.Equal("重命名用例", dialog.Title);
        Assert.Equal("用例名称", dialog.FieldLabel);
        Assert.Equal("旧用例名", dialog.Value);

        dialog.Value = "新用例名";
        await dialog.SaveCommand.ExecuteAsync(null);

        // 改名后抽屉按 Id upsert 出新条目，名称刷新
        var renamedItem = panel.RequestCases.Single(caseItem => caseItem.Id == "case-1");
        Assert.Equal("新用例名", renamedItem.Name);
    }

    [Fact]
    public async Task UseCasesPanel_DuplicateCase_ShouldUseCopySuffixAndDeleteAfterConfirmation()
    {
        var requestCaseService = new FakeRequestCaseService();
        var dialogService = new ViewModelSharedTestDoubles.FakeDialogService();
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            requestCaseService,
            dialogService: dialogService);
        var panel = viewModel.UseCasesPanel;
        panel.SetProjectContext("project-1");
        var sourceCase = new RequestCaseDto
        {
            Id = "case-1",
            ProjectId = "project-1",
            EntryType = ProjectTabRequestEntryTypes.HttpCase,
            ParentId = "interface-1",
            Name = "成功响应",
            RequestSnapshot = new RequestSnapshotDto { Method = "POST", Url = "/orders" },
            HasLoadedDetail = true,
            UpdatedAt = DateTime.UtcNow
        };
        requestCaseService.Cases.Add(sourceCase);
        panel.UpsertCaseItem(sourceCase);

        await panel.DuplicateCaseCommand.ExecuteAsync(panel.RequestCases.Single());

        var duplicate = Assert.Single(panel.RequestCases, item => item.Id != "case-1");
        Assert.Equal("成功响应_copy", duplicate.Name);
        Assert.Contains(requestCaseService.Cases, item => item.Id == duplicate.Id && item.Name == "成功响应_copy");

        await panel.RequestDeleteCaseCommand.ExecuteAsync(duplicate);

        Assert.DoesNotContain(panel.RequestCases, item => item.Id == duplicate.Id);
        Assert.DoesNotContain(requestCaseService.Cases, item => item.Id == duplicate.Id);
        var confirmation = Assert.Single(dialogService.Requests, request =>
            request.ViewType == typeof(RequestCaseDeleteConfirmDialogView));
        Assert.IsType<RequestCaseDeleteConfirmDialogViewModel>(confirmation.DataContext);
    }

    [Fact]
    public async Task RequestEditorWorkspaceView_ShouldInstantiateAfterLoadingHttpInterface()
    {
        EnsureAvaloniaInitialized();

        var apiWorkspaceService = new FakeApiWorkspaceService();
        var requestCaseService = new FakeRequestCaseService();
        apiWorkspaceService.SeedDocument("project-1", "支付服务", "FILE", @"C:\temp\pay-swagger.json", "https://pay.demo.local", 1);
        var viewModel = CreateViewModel(apiWorkspaceService, requestCaseService);

        await viewModel.InitializeAsync();
        await viewModel.Import.LoadImportedDocumentsAsync(manageBusyState: false);

        var interfaceItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "接口 1");
        Assert.NotNull(interfaceItem);
        await viewModel.Catalog.LoadWorkspaceItem(interfaceItem);

        var exception = Record.Exception(() => new RequestEditorWorkspaceView
        {
            DataContext = viewModel
        });
        var requestCodeDialogException = Record.Exception(() => new RequestCodeDialogView());
        // 实际请求面板依赖响应区样式 token，实例化冒烟防止 StaticResource 缺失在运行期才暴露
        var actualRequestPanelException = Record.Exception(() => new ApixPress.App.Views.Controls.ActualRequestPanelView());
        // 弹窗 chrome 由 Ursa 提供，冒烟四个小弹窗内容视图。注意：UI 实例化测试统一并入本用例，
        // 不再新增独立实例化测试——新增方法会扰动 xUnit 集合内调度并触发 Avalonia 跨线程竞态
        // （ContentHost 系列曾因此复现 "different thread owns it"，2026-09-16）。
        var quickRequestSaveDialogException = Record.Exception(() => new ApixPress.App.Views.Controls.QuickRequestSaveDialogView());
        var httpCaseSaveDialogException = Record.Exception(() => new ApixPress.App.Views.Controls.HttpCaseSaveDialogView());
        var createFolderDialogException = Record.Exception(() => new ApixPress.App.Views.Controls.WorkspaceCreateFolderDialogView());
        var deleteConfirmDialogException = Record.Exception(() => new ApixPress.App.Views.Controls.WorkspaceDeleteConfirmDialogView());
        var requestCaseDeleteConfirmDialogException = Record.Exception(() => new ApixPress.App.Views.Controls.RequestCaseDeleteConfirmDialogView());
        var bodyTabException = Record.Exception(() => new ApixPress.App.Views.Controls.HttpInterfaceBodyTabView
        {
            DataContext = viewModel
        });
        var responseSearchException = Record.Exception(() =>
        {
            var editor = new ResponseBodyEditor
            {
                Text = "order order",
                SearchMatchOffsets = new[] { 0, 6 },
                SearchMatchLength = 5,
                CurrentSearchMatchOffset = 6
            };
            editor.Measure(new Size(800, 300));
            editor.Arrange(new Rect(0, 0, 800, 300));
            Assert.Equal("order", editor.SelectedText);
        });
        // 项目信息编辑弹窗（名称/简介二选一）实例化冒烟，防止 StaticResource 缺失在运行期才暴露
        var projectInfoEditDialogException = Record.Exception(() => new ApixPress.App.Views.Controls.ProjectInfoEditDialogView
        {
            DataContext = new ProjectInfoEditDialogViewModel(true, "示例项目", _ => Task.FromResult<string?>(null))
        });

        Assert.Null(exception);
        Assert.Null(requestCodeDialogException);
        Assert.Null(actualRequestPanelException);
        Assert.Null(projectInfoEditDialogException);
        Assert.Null(quickRequestSaveDialogException);
        Assert.Null(httpCaseSaveDialogException);
        Assert.Null(createFolderDialogException);
        Assert.Null(deleteConfirmDialogException);
        Assert.Null(requestCaseDeleteConfirmDialogException);
        Assert.Null(bodyTabException);
        Assert.Null(responseSearchException);
    }

    private static void EnsureAvaloniaInitialized()
    {
        if (_isAvaloniaInitialized)
        {
            return;
        }

        // 主工程已改用 Win32+Skia 显式装配（不再引用 Avalonia.Desktop），
        // UsePlatformDetect 扩展随包移除，这里保持等价平台初始化；
        // HarfBuzz 必须显式注册，缺失时文本整形在 Setup 期直接抛异常
        AppBuilder.Configure<App>()
            .UseWin32()
            .UseSkia()
            .UseHarfBuzz()
            .SetupWithoutStarting();
        _isAvaloniaInitialized = true;
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
        Assert.Equal("当前标签有未保存修改，已保留原标签并打开新的工作标签。", viewModel.StatusMessage);
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
        Assert.True(viewModel.Shell.IsInterfaceManagementSection);
        Assert.Equal(ProjectWorkspaceContentMode.RequestEditor, viewModel.Shell.CurrentContentMode);
        Assert.True(viewModel.ActiveWorkspaceTab?.IsQuickRequestTab);
        Assert.Equal("https://demo.local/orders", viewModel.Editor.RequestUrl);
        Assert.True(viewModel.ResponseSection.HasResponse);
        Assert.Contains("\"items\":[]", viewModel.ResponseSection.BodyText);
    }

    [Fact]
    public async Task SendRequestCommand_ShouldRecordEnvironmentInHistorySnapshot()
    {
        var requestExecutionService = new FakeRequestExecutionService();
        var requestHistoryService = new FakeRequestHistoryService();
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            requestHistoryService: requestHistoryService,
            requestExecutionService: requestExecutionService);
        await viewModel.InitializeAsync();

        viewModel.Workspace.OpenQuickRequestWorkspaceCommand.Execute(null);
        viewModel.Editor.RequestUrl = "https://demo.local/orders";
        await viewModel.SendRequestCommand.ExecuteAsync(null);

        var recorded = Assert.Single(requestHistoryService.Items);
        Assert.Equal("env-1", recorded.RequestSnapshot.EnvironmentId);
        Assert.Equal("开发", recorded.RequestSnapshot.EnvironmentName);
    }

    [Fact]
    public async Task LoadHistoryRequestAsync_RelativeUrl_ShouldOpenHttpInterfaceTabAndRestoreEnvironment()
    {
        var environmentService = new FakeEnvironmentVariableService();
        environmentService.SeededEnvironments.Add(new ProjectEnvironmentDto
        {
            Id = "env-2",
            Name = "测试",
            BaseUrl = "https://test.demo.local",
            IsActive = false,
            SortOrder = 2
        });
        var requestHistoryService = new FakeRequestHistoryService();
        requestHistoryService.Items.Add(new RequestHistoryItemDto
        {
            Id = "history-1",
            Timestamp = new DateTime(2026, 10, 25, 10, 0, 0, DateTimeKind.Utc),
            HasResponse = true,
            StatusCode = 200,
            RequestSnapshot = new RequestSnapshotDto
            {
                Method = "GET",
                Url = "/api/iam/menu-tree",
                EnvironmentId = "env-2",
                EnvironmentName = "测试"
            },
            ResponseSnapshot = new ResponseSnapshotDto
            {
                StatusCode = 200,
                Content = "{\"code\":0}"
            }
        });
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            requestHistoryService: requestHistoryService,
            environmentService: environmentService);
        await viewModel.InitializeAsync();
        await viewModel.Shell.ShowRequestHistoryCommand.ExecuteAsync(null);

        var historyItem = Assert.Single(viewModel.RequestHistory);
        await viewModel.LoadHistoryRequestAsync(historyItem);

        var tab = viewModel.ActiveWorkspaceTab;
        Assert.NotNull(tab);
        Assert.True(tab!.IsHttpInterfaceTab);
        Assert.Equal("/api/iam/menu-tree", viewModel.Editor.RequestUrl);
        Assert.Equal("env-2", viewModel.EnvironmentPanel.SelectedEnvironment?.Id);
        Assert.True(viewModel.ResponseSection.HasResponse);
    }

    [Fact]
    public async Task LoadHistoryRequestAsync_RelativeUrlWithoutEnvironment_ShouldKeepCurrentEnvironment()
    {
        var requestHistoryService = new FakeRequestHistoryService();
        requestHistoryService.Items.Add(new RequestHistoryItemDto
        {
            Id = "history-1",
            Timestamp = new DateTime(2026, 10, 25, 10, 0, 0, DateTimeKind.Utc),
            RequestSnapshot = new RequestSnapshotDto
            {
                Method = "GET",
                Url = "/api/users"
            }
        });
        var viewModel = CreateViewModel(new FakeApiWorkspaceService(), requestHistoryService: requestHistoryService);
        await viewModel.InitializeAsync();
        await viewModel.Shell.ShowRequestHistoryCommand.ExecuteAsync(null);

        var historyItem = Assert.Single(viewModel.RequestHistory);
        await viewModel.LoadHistoryRequestAsync(historyItem);

        Assert.True(viewModel.ActiveWorkspaceTab?.IsHttpInterfaceTab);
        Assert.Equal("/api/users", viewModel.Editor.RequestUrl);
        Assert.Equal("env-1", viewModel.EnvironmentPanel.SelectedEnvironment?.Id);
    }

    [Fact]
    public async Task SelectingHistoryItem_ShouldLoadItsRequestDetailsForPreview()
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
                Url = "https://demo.local/orders",
                QueryParameters = [new RequestKeyValueDto { Name = "page", Value = "2" }],
                Headers = [new RequestKeyValueDto { Name = "X-Trace", Value = "history" }]
            },
            ResponseSnapshot = new ResponseSnapshotDto
            {
                StatusCode = 200,
                DurationMs = 18,
                SizeBytes = 256,
                Content = "{\"items\":[] }"
            }
        });
        var viewModel = CreateViewModel(new FakeApiWorkspaceService(), requestHistoryService: requestHistoryService);
        await viewModel.InitializeAsync();
        await viewModel.Shell.ShowRequestHistoryCommand.ExecuteAsync(null);

        var historyItem = Assert.Single(viewModel.RequestHistory);
        viewModel.HistoryPanel.SelectedHistoryItem = historyItem;

        await WaitUntilAsync(() => requestHistoryService.GetDetailCallCount == 1);

        Assert.Same(historyItem, viewModel.HistoryPanel.SelectedHistoryItem);
        Assert.True(historyItem.HasQueryParameters);
        Assert.True(historyItem.HasRequestHeaders);
        Assert.True(viewModel.HistoryPanel.SelectedResponseSection.HasResponse);
        Assert.Equal("HTTP 200", viewModel.HistoryPanel.SelectedResponseSection.StatusText);
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
    public async Task RequestEditor_ShouldOpenRequestCodeDialogWithCurlCommand()
    {
        var dialogService = new ViewModelSharedTestDoubles.FakeDialogService();
        var viewModel = CreateViewModel(new FakeApiWorkspaceService(), dialogService: dialogService);

        viewModel.Workspace.OpenQuickRequestWorkspaceCommand.Execute(null);
        viewModel.Editor.SelectedMethod = "POST";
        viewModel.Editor.RequestUrl = "https://demo.local/orders";
        viewModel.ConfigTab.SelectedBodyMode = BodyModes.RawJson;
        viewModel.ConfigTab.RequestBody = "{\"name\":\"test\"}";
        viewModel.ConfigTab.Headers.Add(new RequestParameterItemViewModel
        {
            Name = "Authorization",
            Value = "Bearer token"
        });

        await viewModel.Editor.OpenRequestCodeDialogCommand.ExecuteAsync(null);

        var request = Assert.Single(dialogService.Requests);
        Assert.Equal(typeof(RequestCodeDialogView), request.ViewType);
        Assert.Same(viewModel.Editor, request.DataContext);
        Assert.True(request.ShowCloseButton);
        Assert.Equal("生成代码", viewModel.Editor.CurrentRequestCodeTitle);
        Assert.Contains("curl --request POST", viewModel.Editor.CurrentRequestCodeCurlCommand);
        Assert.Contains("https://demo.local/orders", viewModel.Editor.CurrentRequestCodeCurlCommand);
        Assert.Contains("--header \"Authorization: Bearer token\"", viewModel.Editor.CurrentRequestCodeCurlCommand);
        Assert.Contains("--data-raw \"{\\\"name\\\":\\\"test\\\"}\"", viewModel.Editor.CurrentRequestCodeCurlCommand);
        Assert.Contains("wget --method=POST", viewModel.Editor.CurrentRequestCodeWgetCommand);
        Assert.Contains("--header=\"Authorization: Bearer token\"", viewModel.Editor.CurrentRequestCodeWgetCommand);
        Assert.Contains("--body-data=\"{\\\"name\\\":\\\"test\\\"}\"", viewModel.Editor.CurrentRequestCodeWgetCommand);
        Assert.Contains("Invoke-WebRequest", viewModel.Editor.CurrentRequestCodePowerShellCommand);
        Assert.Contains("-Method POST", viewModel.Editor.CurrentRequestCodePowerShellCommand);
        Assert.Contains("-Headers $headers", viewModel.Editor.CurrentRequestCodePowerShellCommand);
        Assert.Contains("-Body \"{`\"name`\":`\"test`\"}\"", viewModel.Editor.CurrentRequestCodePowerShellCommand);
        Assert.Contains("using var httpClient = new HttpClient();", viewModel.Editor.CurrentRequestCodeCSharpCommand);
        Assert.Contains("new HttpRequestMessage(HttpMethod.Post, \"https://demo.local/orders\")", viewModel.Editor.CurrentRequestCodeCSharpCommand);
        Assert.Contains("request.Headers.TryAddWithoutValidation(\"Authorization\", \"Bearer token\")", viewModel.Editor.CurrentRequestCodeCSharpCommand);
        Assert.Contains("new StringContent(\"{\\\"name\\\":\\\"test\\\"}\", Encoding.UTF8, \"application/json\")", viewModel.Editor.CurrentRequestCodeCSharpCommand);

        viewModel.Editor.ShowCSharpRequestCodeLanguageCommand.Execute(null);
        Assert.True(viewModel.Editor.IsCSharpRequestCodeLanguageSelected);
        Assert.False(viewModel.Editor.IsShellRequestCodeLanguageSelected);
        Assert.Equal("C#", viewModel.Editor.CurrentRequestCodeLanguageTitle);

        viewModel.Editor.ShowShellRequestCodeLanguageCommand.Execute(null);
        Assert.True(viewModel.Editor.IsShellRequestCodeLanguageSelected);

        viewModel.Editor.CloseRequestCodeDialogCommand.Execute(null);

        // 关闭后清空弹窗内容，避免残留上一请求的代码片段
        Assert.Equal(string.Empty, viewModel.Editor.RequestCodeCurlCommand);
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
    public async Task InterfaceRootCommand_ShouldOpenAuthByDefaultAndSaveBearerSettings()
    {
        var projectHttpSettingsService = new FakeProjectHttpSettingsService();
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            projectHttpSettingsService: projectHttpSettingsService);
        await viewModel.InitializeAsync();
        var root = Assert.Single(viewModel.Catalog.InterfaceTreeItems);

        await viewModel.Catalog.LoadWorkspaceItem(root);
        viewModel.InterfaceRoot.SelectedAuthModeIndex = 1;
        viewModel.InterfaceRoot.BearerToken = "{{apiKey}}";
        await viewModel.InterfaceRoot.SaveAuthCommand.ExecuteAsync(null);

        Assert.True(viewModel.Shell.ShowInterfaceRootWorkspace);
        Assert.NotNull(viewModel.ActiveWorkspaceTab);
        Assert.Equal("根目录", viewModel.ActiveWorkspaceTab!.HeaderText);
        Assert.Contains(viewModel.ActiveWorkspaceTab, viewModel.VisibleWorkspaceTabs);
        Assert.True(viewModel.InterfaceRoot.IsAuthSelected);
        Assert.False(viewModel.InterfaceRoot.IsAllInterfacesSelected);
        Assert.Equal(1, projectHttpSettingsService.SaveCallCount);
        Assert.Equal(ProjectHttpAuthSettingsDto.ModeBearer, projectHttpSettingsService.CurrentSettings.AuthMode);
        Assert.Equal("{{apiKey}}", projectHttpSettingsService.CurrentSettings.BearerToken);
    }

    [Fact]
    public async Task CloseInterfaceTabOpenedFromInterfaceRoot_ShouldKeepInterfaceRootTabActive()
    {
        var requestCaseService = new FakeRequestCaseService();
        requestCaseService.Cases.Add(new RequestCaseDto
        {
            Id = "interface-1",
            ProjectId = "project-1",
            EntryType = ProjectTabRequestEntryTypes.HttpInterface,
            Name = "查询用户",
            GroupName = "接口",
            FolderPath = "用户",
            HasLoadedDetail = true,
            RequestSnapshot = new RequestSnapshotDto
            {
                EndpointId = "manual:GET /users",
                Method = "GET",
                Url = "/users"
            },
            UpdatedAt = DateTime.UtcNow
        });
        var viewModel = CreateViewModel(new FakeApiWorkspaceService(), requestCaseService);
        await viewModel.InitializeAsync();
        var root = Assert.Single(viewModel.Catalog.InterfaceTreeItems);

        await viewModel.Catalog.LoadWorkspaceItem(root);
        var rootTab = viewModel.ActiveWorkspaceTab;
        Assert.NotNull(rootTab);
        Assert.True(rootTab!.IsInterfaceRootTab);
        Assert.True(viewModel.Shell.ShowInterfaceRootWorkspace);

        viewModel.InterfaceRoot.ShowAllInterfacesCommand.Execute(null);
        var overviewItem = Assert.Single(viewModel.InterfaceRoot.HttpInterfaces);
        await overviewItem.OpenCommand.ExecuteAsync(null);

        var interfaceTab = viewModel.ActiveWorkspaceTab;
        Assert.NotNull(interfaceTab);
        Assert.NotSame(rootTab, interfaceTab);
        Assert.True(interfaceTab!.IsHttpInterfaceTab);
        var visibleTabsBeforeClose = viewModel.VisibleWorkspaceTabs.ToList();
        Assert.Contains(rootTab, visibleTabsBeforeClose);
        Assert.Contains(interfaceTab, visibleTabsBeforeClose);
        Assert.True(viewModel.Shell.ShowRequestEditorWorkspace);

        viewModel.Workspace.CloseWorkspaceTabCommand.Execute(interfaceTab);

        Assert.Same(rootTab, viewModel.ActiveWorkspaceTab);
        var visibleTabsAfterClose = viewModel.VisibleWorkspaceTabs.ToList();
        Assert.Contains(rootTab, visibleTabsAfterClose);
        Assert.DoesNotContain(interfaceTab, visibleTabsAfterClose);
        Assert.True(viewModel.Shell.ShowInterfaceRootWorkspace);
        Assert.False(viewModel.Shell.ShowRequestEditorWorkspace);
        Assert.Equal(ProjectWorkspaceContentMode.InterfaceRoot, viewModel.Shell.CurrentContentMode);
    }

    [Fact]
    public async Task SendRequestCommand_ShouldApplyGlobalBearerAuthOnlyForHttpInterface()
    {
        var requestExecutionService = new FakeRequestExecutionService();
        var projectHttpSettingsService = new FakeProjectHttpSettingsService
        {
            CurrentSettings = new ProjectHttpAuthSettingsDto
            {
                ProjectId = "project-1",
                AuthMode = ProjectHttpAuthSettingsDto.ModeBearer,
                BearerToken = "{{apiKey}}"
            }
        };
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            requestExecutionService: requestExecutionService,
            projectHttpSettingsService: projectHttpSettingsService);
        await viewModel.InitializeAsync();

        Assert.False(viewModel.Shell.ShowInterfaceRootWorkspace);

        viewModel.Workspace.OpenHttpInterfaceWorkspaceCommand.Execute(null);
        viewModel.Editor.RequestUrl = "/orders";
        await viewModel.SendRequestCommand.ExecuteAsync(null);

        var httpRequest = requestExecutionService.LastRequest;
        Assert.NotNull(httpRequest);
        Assert.Contains(httpRequest!.Headers, item =>
            item.Name == "Authorization"
            && item.Value == "Bearer {{apiKey}}"
            && item.IsEnabled);

        viewModel.Workspace.OpenQuickRequestWorkspaceCommand.Execute(null);
        viewModel.Editor.RequestUrl = "https://demo.local/ping";
        await viewModel.SendRequestCommand.ExecuteAsync(null);

        var quickRequest = requestExecutionService.LastRequest;
        Assert.NotNull(quickRequest);
        Assert.DoesNotContain(quickRequest!.Headers, item => item.Name == "Authorization");
    }

    [Fact]
    public async Task SendRequestCommand_ShouldApplyGlobalBasicAuthForHttpInterface()
    {
        var requestExecutionService = new FakeRequestExecutionService();
        var projectHttpSettingsService = new FakeProjectHttpSettingsService
        {
            CurrentSettings = new ProjectHttpAuthSettingsDto
            {
                ProjectId = "project-1",
                AuthMode = ProjectHttpAuthSettingsDto.ModeBasic,
                BasicUsername = "demo",
                BasicPassword = "secret"
            }
        };
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            requestExecutionService: requestExecutionService,
            projectHttpSettingsService: projectHttpSettingsService);
        await viewModel.InitializeAsync();

        viewModel.Workspace.OpenHttpInterfaceWorkspaceCommand.Execute(null);
        viewModel.Editor.RequestUrl = "/orders";
        await viewModel.SendRequestCommand.ExecuteAsync(null);

        var httpRequest = requestExecutionService.LastRequest;
        Assert.NotNull(httpRequest);
        Assert.Contains(httpRequest!.Headers, item =>
            item.Name == "Authorization"
            && item.Value == "Basic ZGVtbzpzZWNyZXQ="
            && item.IsEnabled);
    }

    [Fact]
    public async Task SendRequestCommand_ShouldNormalizeMultilineQuickRequestPaste()
    {
        var requestExecutionService = new FakeRequestExecutionService();
        var viewModel = CreateViewModel(
            new FakeApiWorkspaceService(),
            requestExecutionService: requestExecutionService);

        viewModel.Workspace.OpenQuickRequestWorkspaceCommand.Execute(null);
        viewModel.Editor.RequestUrl = """
            curl -X POST "https://demo.local/orders?status=open" \
              -H "Authorization: Bearer token"
            """;

        Assert.Equal("https://demo.local/orders?status=open", viewModel.Editor.RequestUrl);

        await viewModel.SendRequestCommand.ExecuteAsync(null);

        Assert.Equal(1, requestExecutionService.SendCallCount);
        Assert.NotNull(requestExecutionService.LastRequest);
        Assert.Equal("https://demo.local/orders?status=open", requestExecutionService.LastRequest!.Url);
    }

    [Fact]
    public void RequestEditor_ShouldSkipResolvedPreviewForOversizedQuickRequestUrl()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());
        var longUrl = $"https://demo.local/{new string('a', 5000)}";

        viewModel.Workspace.OpenQuickRequestWorkspaceCommand.Execute(null);
        viewModel.Editor.RequestUrl = longUrl;

        // URL 全量重算已并入 200ms 防抖：等防抖回调完成后断言（与真实输入路径契约一致）
        Assert.True(SpinWait.SpinUntil(
            () => viewModel.Editor.HasResolvedRequestPreview,
            TimeSpan.FromSeconds(2)));
        Assert.Equal(longUrl, viewModel.Editor.RequestUrl);
        Assert.Contains("请求地址过长", viewModel.Editor.ResolvedRequestPreviewText);
    }

    [Fact]
    public void RequestEditor_ShouldNotMutateRequestNameWhenResolvingHttpInterfacePreview()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());
        var requestNameChangedCount = 0;

        viewModel.Workspace.OpenHttpInterfaceWorkspaceCommand.Execute(null);
        viewModel.Editor.RequestUrl = "/time";
        viewModel.ActiveWorkspaceTab!.ConfigTab.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(RequestConfigTabViewModel.RequestName))
            {
                requestNameChangedCount++;
            }
        };

        // URL 全量重算已并入 200ms 防抖：等防抖回调完成后再取预览（与真实输入路径契约一致）
        Assert.True(SpinWait.SpinUntil(
            () => viewModel.Editor.HasResolvedRequestPreview,
            TimeSpan.FromSeconds(2)));
        var previewText = viewModel.Editor.ResolvedRequestPreviewText;

        Assert.Equal(0, requestNameChangedCount);
        Assert.Equal(string.Empty, viewModel.ActiveWorkspaceTab.ConfigTab.RequestName);
        Assert.Contains("发送预览", previewText);
    }

    [Fact]
    public void RequestEditor_ShouldRefreshDocumentPreviewWhenResponseChanges()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());

        viewModel.Workspace.OpenHttpInterfaceWorkspaceCommand.Execute(null);
        viewModel.Editor.RequestUrl = "https://demo.local/orders";

        Assert.Equal("等待调试后生成响应示例", viewModel.Editor.CurrentHttpDocumentResponseSummary);
        Assert.Equal("{ }", viewModel.Editor.CurrentHttpDocumentBodyPreview);

        viewModel.ResponseSection.ApplyResult(
            ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto
            {
                StatusCode = 200,
                DurationMs = 18,
                SizeBytes = 12,
                Content = "{\"ok\":true}",
                Headers =
                [
                    new ResponseHeaderDto
                    {
                        Name = "Content-Type",
                        Value = "application/json"
                    }
                ]
            }),
            viewModel.ActiveWorkspaceTab!.BuildSnapshot());

        Assert.Equal("成功 (200)", viewModel.Editor.CurrentHttpDocumentResponseSummary);
        Assert.Contains("\"ok\": true", viewModel.Editor.CurrentHttpDocumentBodyPreview);
    }

    [Fact]
    public void RequestEditor_ShouldRefreshCurlSnippetWhenQueryParametersChange()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());

        viewModel.Workspace.OpenHttpInterfaceWorkspaceCommand.Execute(null);
        viewModel.Editor.RequestUrl = "https://demo.local/orders";
        viewModel.Editor.ShowHttpDocumentPreviewModeCommand.Execute(null);

        Assert.DoesNotContain("status=open", viewModel.Editor.CurrentHttpDocumentCurlSnippet);

        viewModel.ConfigTab.QueryParameters.Add(new RequestParameterItemViewModel
        {
            ParameterType = RequestParameterKind.Query,
            Name = "status",
            Value = "open",
            IsEnabled = true
        });

        Assert.Contains("status=open", viewModel.Editor.CurrentHttpDocumentCurlSnippet);
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
    public async Task CancelRequestCommand_ShouldCancelPendingRequestAndHideLoading()
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
        Assert.True(SpinWait.SpinUntil(() => viewModel.ResponseSection.IsLoading, TimeSpan.FromSeconds(1)));

        viewModel.CancelRequestCommand.Execute(null);
        await sendTask;

        Assert.False(viewModel.ResponseSection.IsLoading);
        Assert.False(viewModel.ResponseSection.HasResponse);
        Assert.Equal("已取消当前请求。", viewModel.StatusMessage);
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

        Assert.True(viewModel.SaveHttpCaseCommand.CanExecute(null));
        var dialogService = new ViewModelSharedTestDoubles.FakeDialogService();
        Assert.True(viewModel.SaveHttpCaseCommand.CanExecute(null));
        await viewModel.SaveHttpCaseCommand.ExecuteAsync(null);
        Assert.Empty(requestCaseService.Cases);

        viewModel.HttpCaseSave.DraftName = "成功响应";
        await viewModel.HttpCaseSave.ConfirmSaveCommand.ExecuteAsync(null);

        Assert.Equal("接口和请求用例「成功响应」已保存。", viewModel.StatusMessage);

        var savedInterface = Assert.Single(requestCaseService.Cases, item => item.EntryType == ProjectTabRequestEntryTypes.HttpInterface);
        var savedCase = Assert.Single(requestCaseService.Cases, item => item.EntryType == ProjectTabRequestEntryTypes.HttpCase);

        Assert.Equal(savedInterface.Id, savedCase.ParentId);
        Assert.Equal("订单", savedCase.FolderPath);

        var folderItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "订单 (1)");
        var interfaceItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "创建订单");

        // 接口节点默认收缩，用例在展开后作为下一级节点展示，同时保留计数芯片
        Assert.NotNull(folderItem);
        Assert.NotNull(interfaceItem);
        Assert.True(interfaceItem!.HasChildren);
        Assert.False(interfaceItem.IsExpanded);
        interfaceItem.EnsureChildrenLoaded();
        var firstCaseNode = Assert.Single(interfaceItem.Children);
        Assert.Equal("成功响应", firstCaseNode.Title);
        Assert.Equal(1, interfaceItem.CaseCount);
        Assert.True(interfaceItem.HasCases);

        // 第二个用例：重新打开接口本体清掉编辑中的用例上下文（EditingCaseId），保存应新建而非覆盖
        await viewModel.Catalog.LoadWorkspaceItem(interfaceItem);
        viewModel.HttpCaseSave.DraftName = "异常响应";
        await viewModel.HttpCaseSave.ConfirmSaveCommand.ExecuteAsync(null);

        // 导航重建被抑制时，接口节点计数须定向刷新 1 → 2
        Assert.Equal(2, interfaceItem.CaseCount);
        interfaceItem.EnsureChildrenLoaded();
        Assert.Equal(2, interfaceItem.Children.Count);
        Assert.Equal(2, requestCaseService.Cases.Count(item => item.EntryType == ProjectTabRequestEntryTypes.HttpCase));
    }

    [Fact]
    public async Task SaveExistingHttpCase_ShouldOnlyMentionRequestCaseInStatus()
    {
        var requestCaseService = new FakeRequestCaseService();
        var parentInterface = new RequestCaseDto
        {
            Id = "interface-1",
            ProjectId = "project-1",
            EntryType = ProjectTabRequestEntryTypes.HttpInterface,
            Name = "用户接口",
            GroupName = "用户",
            RequestSnapshot = new RequestSnapshotDto
            {
                EndpointId = "endpoint-1",
                Method = "GET",
                Url = "/users",
                Name = "用户接口"
            },
            UpdatedAt = DateTime.UtcNow
        };
        var requestCase = new RequestCaseDto
        {
            Id = "case-1",
            ProjectId = "project-1",
            EntryType = ProjectTabRequestEntryTypes.HttpCase,
            Name = "成功",
            GroupName = "用例",
            ParentId = parentInterface.Id,
            RequestSnapshot = new RequestSnapshotDto
            {
                EndpointId = "endpoint-1",
                Method = "GET",
                Url = "/users?id=1",
                Name = "用户接口"
            },
            HasLoadedDetail = true,
            UpdatedAt = DateTime.UtcNow
        };
        requestCaseService.Cases.AddRange([parentInterface, requestCase]);
        var viewModel = CreateViewModel(new FakeApiWorkspaceService(), requestCaseService);

        await viewModel.InitializeAsync();
        await viewModel.Catalog.LoadSavedRequestAsync(requestCase);

        viewModel.Editor.CurrentHttpCaseName = "异常响应";
        await viewModel.Workflow.SaveCurrentEditorAsync();

        Assert.Equal("请求用例「异常响应」已保存。", viewModel.StatusMessage);
        Assert.DoesNotContain("接口和", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Equal(1, requestCaseService.Cases.Count(item => item.EntryType == ProjectTabRequestEntryTypes.HttpInterface));
        Assert.Equal(1, requestCaseService.Cases.Count(item => item.EntryType == ProjectTabRequestEntryTypes.HttpCase));
        Assert.Equal("异常响应", Assert.Single(requestCaseService.Cases, item => item.EntryType == ProjectTabRequestEntryTypes.HttpCase).Name);
    }

    [Fact]
    public async Task UseCasesPanel_ShouldShowOnlyCurrentInterfaceCasesAndLoadSelectedCase()
    {
        var requestCaseService = new FakeRequestCaseService();
        requestCaseService.Cases.AddRange(
        [
            new RequestCaseDto
            {
                Id = "interface-1",
                ProjectId = "project-1",
                EntryType = ProjectTabRequestEntryTypes.HttpInterface,
                Name = "用户接口",
                GroupName = "接口",
                RequestSnapshot = new RequestSnapshotDto
                {
                    Name = "用户接口",
                    Method = "GET",
                    Url = "/users"
                },
                UpdatedAt = DateTime.UtcNow
            },
            new RequestCaseDto
            {
                Id = "case-1",
                ProjectId = "project-1",
                EntryType = ProjectTabRequestEntryTypes.HttpCase,
                Name = "成功",
                GroupName = "用例",
                ParentId = "interface-1",
                RequestSnapshot = new RequestSnapshotDto
                {
                    Name = "用户接口",
                    Method = "GET",
                    Url = "/users?id=1",
                    BodyMode = BodyModes.RawJson,
                    BodyContent = "{\"id\":1}"
                },
                UpdatedAt = DateTime.UtcNow
            },
            new RequestCaseDto
            {
                Id = "case-2",
                ProjectId = "project-1",
                EntryType = ProjectTabRequestEntryTypes.HttpCase,
                Name = "不存在用户",
                GroupName = "用例",
                ParentId = "interface-2",
                RequestSnapshot = new RequestSnapshotDto
                {
                    Name = "其他接口",
                    Method = "POST",
                    Url = "/users"
                },
                UpdatedAt = DateTime.UtcNow
            }
        ]);
        var viewModel = CreateViewModel(new FakeApiWorkspaceService(), requestCaseService);

        await viewModel.InitializeAsync();

        var interfaceItem = FindExplorerItemByTitle(viewModel.Catalog.InterfaceCatalogItems, "用户接口");
        Assert.NotNull(interfaceItem);
        Assert.Equal(1, interfaceItem.CaseCount);
        await viewModel.Catalog.LoadWorkspaceItem(interfaceItem);
        await WaitUntilAsync(() => viewModel.UseCasesPanel.CurrentInterfaceName == "用户接口");

        var currentCases = viewModel.UseCasesPanel.CurrentInterfaceCaseItems;
        var currentCase = Assert.Single(currentCases);
        Assert.Equal("成功", currentCase.Name);
        Assert.Equal("用户接口", viewModel.UseCasesPanel.CurrentInterfaceName);
        Assert.Equal("共 1 个用例", viewModel.UseCasesPanel.CurrentInterfaceCaseCountText);

        await viewModel.UseCasesPanel.LoadCaseCommand.ExecuteAsync(currentCase);

        Assert.Equal("case-1", viewModel.ActiveWorkspaceTab!.EditingCaseId);
        Assert.Equal("/users?id=1", viewModel.Editor.RequestUrl);
        Assert.Equal(BodyModes.RawJson, viewModel.Editor.ConfigTab.SelectedBodyMode);
        Assert.Equal("{\"id\":1}", viewModel.Editor.ConfigTab.RequestBody);
        Assert.True(currentCase.IsSelected);

        viewModel.Editor.RequestUrl = "/users?id=2";
        viewModel.Editor.ConfigTab.RequestBody = "{\"id\":2}";

        await viewModel.Workflow.SaveCurrentEditorAsync();

        var updatedCase = Assert.Single(requestCaseService.Cases, item => item.Id == "case-1");
        var unchangedInterface = Assert.Single(requestCaseService.Cases, item => item.Id == "interface-1");
        Assert.Equal("case-1", viewModel.ActiveWorkspaceTab!.EditingCaseId);
        Assert.Equal("成功", updatedCase.Name);
        Assert.Equal("interface-1", updatedCase.ParentId);
        Assert.Equal("/users?id=2", updatedCase.RequestSnapshot.Url);
        Assert.Equal("{\"id\":2}", updatedCase.RequestSnapshot.BodyContent);
        Assert.Equal("/users", unchangedInterface.RequestSnapshot.Url);
        Assert.Equal(3, requestCaseService.Cases.Count);
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
    public void OpenHttpInterfaceWorkspace_ShouldPlaceNewInterfaceAtRootFolder()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());

        viewModel.Workspace.OpenHttpInterfaceWorkspaceCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.Editor.CurrentInterfaceFolderPath);
    }

    [Fact]
    public void OpenHttpInterfaceWorkspace_ShouldAssignFolderPathFromParameter()
    {
        var viewModel = CreateViewModel(new FakeApiWorkspaceService());

        viewModel.Workspace.OpenHttpInterfaceWorkspaceCommand.Execute("WeatherForecast/子目录");

        Assert.Equal("WeatherForecast/子目录", viewModel.Editor.CurrentInterfaceFolderPath);
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

        await viewModel.Catalog.RequestDeleteWorkspaceTreeItemCommand.ExecuteAsync(interfaceItem);

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

        await viewModel.Catalog.RequestDeleteWorkspaceTreeItemCommand.ExecuteAsync(interfaceItem);
        await viewModel.Catalog.ConfirmDeleteCommand.ExecuteAsync(null);

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
