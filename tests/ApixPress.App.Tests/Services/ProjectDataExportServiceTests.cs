using System.Text.Json;
using FakeRequestCaseService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeRequestCaseService;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using ApixPress.App.Services.Implementations;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.Results;

namespace ApixPress.App.Tests.Services;

public sealed class ProjectDataExportServiceTests
{
    [Fact]
    public async Task ExportAsync_ShouldWriteReadableProjectPackageJson()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"project-export-{Guid.NewGuid():N}.apixpkg.json");
        var requestCaseService = new FakeRequestCaseService();
        requestCaseService.Cases.Add(new RequestCaseDto
        {
            Id = "interface-1",
            ProjectId = "project-1",
            EntryType = "http-interface",
            Name = "查询订单",
            GroupName = "接口",
            FolderPath = "订单",
            Description = "订单查询接口",
            Tags = ["orders", "read"],
            RequestSnapshot = new RequestSnapshotDto
            {
                EndpointId = "swagger-import:GET /orders",
                Name = "查询订单",
                Method = "GET",
                Url = "/orders",
                QueryParameters =
                [
                    new RequestKeyValueDto
                    {
                        Name = "page",
                        Value = "1",
                        IsEnabled = true
                    }
                ]
            },
            UpdatedAt = new DateTime(2026, 4, 27, 2, 0, 0, DateTimeKind.Utc)
        });
        requestCaseService.Cases.Add(new RequestCaseDto
        {
            Id = "case-1",
            ProjectId = "project-1",
            EntryType = "http-case",
            Name = "查询订单-成功",
            GroupName = "接口",
            FolderPath = "订单",
            ParentId = "interface-1",
            Description = "查询成功场景",
            Tags = ["happy-path"],
            RequestSnapshot = new RequestSnapshotDto
            {
                EndpointId = "swagger-import:GET /orders",
                Name = "查询订单-成功",
                Method = "GET",
                Url = "/orders",
                Headers =
                [
                    new RequestKeyValueDto
                    {
                        Name = "Authorization",
                        Value = "Bearer demo-token",
                        IsEnabled = true
                    }
                ]
            },
            UpdatedAt = new DateTime(2026, 4, 27, 3, 0, 0, DateTimeKind.Utc)
        });

        var service = new ProjectDataExportService(new FakeApiDocumentRepository(), requestCaseService);

        try
        {
            var result = await service.ExportAsync(new ProjectDataExportRequestDto
            {
                ProjectId = "project-1",
                ProjectName = "订单项目",
                ProjectDescription = "导出测试",
                OutputFilePath = tempFile
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(1, result.Data!.InterfaceCount);
            Assert.Equal(1, result.Data.TestCaseCount);

            var bytes = await File.ReadAllBytesAsync(tempFile);
            Assert.True(bytes.Length > 3);
            Assert.Equal(0xEF, bytes[0]);
            Assert.Equal(0xBB, bytes[1]);
            Assert.Equal(0xBF, bytes[2]);

            var json = await File.ReadAllTextAsync(tempFile);
            Assert.Contains(Environment.NewLine, json);
            Assert.Contains("\"订单项目\"", json);
            Assert.DoesNotContain("\\u8ba2\\u5355\\u9879\\u76ee", json);

            var package = JsonSerializer.Deserialize<ProjectDataExportPackageDto>(json);
            Assert.NotNull(package);
            Assert.Equal(ProjectDataExportPackageDto.CurrentSchemaVersion, package!.SchemaVersion);
            Assert.Equal("project-1", package.Project.Id);
            Assert.Equal("订单项目", package.Project.Name);
            Assert.Single(package.Interfaces);
            Assert.Single(package.TestCases);
            Assert.Equal("查询订单", package.Interfaces[0].Name);
            Assert.Equal("orders", package.Interfaces[0].Tags[0]);
            Assert.Equal("/orders", package.Interfaces[0].RequestSnapshot.Url);
            Assert.Equal("interface-1", package.TestCases[0].ParentId);
            Assert.Equal("Authorization", package.TestCases[0].RequestSnapshot.Headers[0].Name);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExportAsync_Backup_ShouldDropOrphanCasesWhenInterfacesExcluded()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"project-export-{Guid.NewGuid():N}.apixpkg.json");
        var requestCaseService = new FakeRequestCaseService();
        requestCaseService.Cases.Add(CreateCase("interface-1", "接口一", "http-interface", string.Empty, "GET", "/orders"));
        requestCaseService.Cases.Add(CreateCase("case-1", "成功", "http-case", "interface-1", "GET", "/orders"));
        var service = new ProjectDataExportService(new FakeApiDocumentRepository(), requestCaseService);

        try
        {
            var result = await service.ExportAsync(new ProjectDataExportRequestDto
            {
                ProjectId = "project-1",
                ProjectName = "订单项目",
                IncludeInterfaces = false,
                IncludeRequestCases = true,
                OutputFilePath = tempFile
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            // 孤儿规则：父接口被排除时用例不导出，避免导入端出现无归属用例
            Assert.Equal(0, result.Data!.InterfaceCount);
            Assert.Equal(0, result.Data.TestCaseCount);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExportAsync_Backup_ShouldExcludeCasesWhenCaseScopeOff()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"project-export-{Guid.NewGuid():N}.apixpkg.json");
        var requestCaseService = new FakeRequestCaseService();
        requestCaseService.Cases.Add(CreateCase("interface-1", "接口一", "http-interface", string.Empty, "GET", "/orders"));
        requestCaseService.Cases.Add(CreateCase("case-1", "成功", "http-case", "interface-1", "GET", "/orders"));
        var service = new ProjectDataExportService(new FakeApiDocumentRepository(), requestCaseService);

        try
        {
            var result = await service.ExportAsync(new ProjectDataExportRequestDto
            {
                ProjectId = "project-1",
                ProjectName = "订单项目",
                IncludeInterfaces = true,
                IncludeRequestCases = false,
                OutputFilePath = tempFile
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Data!.InterfaceCount);
            Assert.Equal(0, result.Data.TestCaseCount);
            var package = JsonSerializer.Deserialize<ProjectDataExportPackageDto>(await File.ReadAllTextAsync(tempFile));
            Assert.Empty(package!.TestCases);
            Assert.Empty(package.Environments);
            Assert.Empty(package.History);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExportAsync_Backup_ShouldIncludeEnvironmentsAndHistoryWhenSelected()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"project-export-{Guid.NewGuid():N}.apixpkg.json");
        var requestCaseService = new FakeRequestCaseService();
        var environmentRepository = new FakeProjectEnvironmentRepository
        {
            Environments =
            [
                new ProjectEnvironmentEntity
                {
                    Id = "env-1", ProjectId = "project-1", Name = "开发", BaseUrl = "https://api.demo.local/",
                    IsActive = true, SortOrder = 1
                }
            ]
        };
        var variableRepository = new FakeEnvironmentVariableRepository
        {
            VariablesByEnvironment = new Dictionary<string, IReadOnlyList<EnvironmentVariableEntity>>(StringComparer.OrdinalIgnoreCase)
            {
                ["env-1"] =
                [
                    new EnvironmentVariableEntity
                    {
                        Id = "var-1", EnvironmentId = "env-1", EnvironmentName = "开发",
                        Key = "token", Value = "demo-value", IsEnabled = true
                    }
                ]
            }
        };
        var historyService = new FakeRequestHistoryService
        {
            History =
            [
                new RequestHistoryItemDto
                {
                    Id = "history-1",
                    Timestamp = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc),
                    HasResponse = false,
                    RequestSnapshot = new RequestSnapshotDto { Method = "GET", Url = "/orders" }
                }
            ]
        };
        var service = new ProjectDataExportService(
            new FakeApiDocumentRepository(),
            requestCaseService,
            environmentRepository,
            variableRepository,
            new FakeRequestCaseDeliveryRepository(),
            new FakeProjectExportSettingsRepository(),
            historyService,
            null,
            null);

        try
        {
            var result = await service.ExportAsync(new ProjectDataExportRequestDto
            {
                ProjectId = "project-1",
                ProjectName = "订单项目",
                IncludeEnvironments = true,
                IncludeRequestHistory = true,
                OutputFilePath = tempFile
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Data!.EnvironmentCount);
            Assert.Equal(1, result.Data.VariableCount);
            Assert.Equal(1, result.Data.HistoryCount);
            var package = JsonSerializer.Deserialize<ProjectDataExportPackageDto>(await File.ReadAllTextAsync(tempFile));
            Assert.Single(package!.Environments);
            Assert.Equal("开发", package.Environments[0].Name);
            Assert.Equal("token", package.Environments[0].Variables[0].Key);
            Assert.Single(package.History);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExportAsync_Html_ShouldWriteSingleDocumentFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"project-export-{Guid.NewGuid():N}.html");
        var requestCaseService = new FakeRequestCaseService();
        requestCaseService.Cases.Add(CreateCase("interface-1", "查询订单", "http-interface", "订单", "GET", "/orders"));
        requestCaseService.Cases.Add(CreateCase("case-1", "成功", "http-case", "interface-1", "GET", "/orders"));
        var service = new ProjectDataExportService(
            new FakeApiDocumentRepository(),
            requestCaseService,
            null,
            null,
            null,
            null,
            null,
            new ProjectDataHtmlRenderer(),
            null);

        try
        {
            var result = await service.ExportAsync(new ProjectDataExportRequestDto
            {
                ProjectId = "project-1",
                ProjectName = "订单项目",
                Format = ProjectDataExportFormat.Html,
                OutputFilePath = tempFile
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(ProjectDataExportFormat.Html, result.Data!.Format);
            Assert.Equal(1, result.Data.InterfaceCount);
            Assert.Equal(1, result.Data.TestCaseCount);
            Assert.Equal(1, result.Data.FileCount);
            var html = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("订单项目", html);
            Assert.Contains("/orders", html);
            Assert.Contains("请求用例示例", html);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExportAsync_Markdown_ShouldWriteIndexAndFolderFiles()
    {
        var parentDirectory = Path.Combine(Path.GetTempPath(), $"project-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(parentDirectory);
        var requestCaseService = new FakeRequestCaseService();
        requestCaseService.Cases.Add(new RequestCaseDto
        {
            Id = "interface-1",
            ProjectId = "project-1",
            EntryType = "http-interface",
            Name = "查询订单",
            GroupName = "接口",
            FolderPath = "订单",
            RequestSnapshot = new RequestSnapshotDto { Method = "GET", Url = "/orders" }
        });
        var service = new ProjectDataExportService(
            new FakeApiDocumentRepository(),
            requestCaseService,
            null,
            null,
            null,
            null,
            null,
            null,
            new ProjectDataMarkdownRenderer());

        try
        {
            var result = await service.ExportAsync(new ProjectDataExportRequestDto
            {
                ProjectId = "project-1",
                ProjectName = "订单项目",
                Format = ProjectDataExportFormat.Markdown,
                OutputDirectoryPath = parentDirectory
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var outputDirectory = result.Data!.OutputPath;
            Assert.EndsWith("订单项目-docs", Path.GetFileName(outputDirectory));
            Assert.Equal(2, result.Data.FileCount);
            Assert.True(File.Exists(Path.Combine(outputDirectory, "README.md")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "订单.md")));
            var index = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "README.md"));
            Assert.Contains("接口目录", index);
            Assert.Contains("订单.md", index);
        }
        finally
        {
            Directory.Delete(parentDirectory, true);
        }
    }

    [Fact]
    public async Task ExportAsync_Markdown_ShouldFailWhenTargetDirectoryNotEmpty()
    {
        var parentDirectory = Path.Combine(Path.GetTempPath(), $"project-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(parentDirectory, "订单项目-docs"));
        await File.WriteAllTextAsync(Path.Combine(parentDirectory, "订单项目-docs", "old.md"), "旧内容");
        var service = new ProjectDataExportService(
            new FakeApiDocumentRepository(),
            new FakeRequestCaseService(),
            null,
            null,
            null,
            null,
            null,
            null,
            new ProjectDataMarkdownRenderer());

        try
        {
            var result = await service.ExportAsync(new ProjectDataExportRequestDto
            {
                ProjectId = "project-1",
                ProjectName = "订单项目",
                Format = ProjectDataExportFormat.Markdown,
                OutputDirectoryPath = parentDirectory
            }, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Contains("已存在且非空", result.Message);
        }
        finally
        {
            Directory.Delete(parentDirectory, true);
        }
    }

    [Fact]
    public async Task SaveExportSettingsAsync_ShouldRoundTripThroughRepository()
    {
        var settingsRepository = new FakeProjectExportSettingsRepository();
        var service = new ProjectDataExportService(
            new FakeApiDocumentRepository(),
            new FakeRequestCaseService(),
            null,
            null,
            null,
            settingsRepository,
            null,
            null,
            null);

        var saveResult = await service.SaveExportSettingsAsync("project-1", new ProjectDataExportSettingsDto
        {
            Format = ProjectDataExportFormats.Markdown,
            IncludeRequestHistory = true,
            LastOutputDirectory = @"C:\temp"
        }, CancellationToken.None);
        Assert.True(saveResult.IsSuccess);

        var loadResult = await service.LoadExportSettingsAsync("project-1", CancellationToken.None);
        Assert.True(loadResult.IsSuccess);
        Assert.Equal(ProjectDataExportFormats.Markdown, loadResult.Data!.Format);
        Assert.True(loadResult.Data.IncludeRequestHistory);
        Assert.Equal(@"C:\temp", loadResult.Data.LastOutputDirectory);

        // 未知格式 token 归一化回备份，避免脏数据进入设置页
        var normalized = await service.SaveExportSettingsAsync("project-1", new ProjectDataExportSettingsDto
        {
            Format = "unknown"
        }, CancellationToken.None);
        Assert.True(normalized.IsSuccess);
        Assert.Equal(ProjectDataExportFormats.Backup, (await service.LoadExportSettingsAsync("project-1", CancellationToken.None)).Data!.Format);
    }

    [Fact]
    public async Task ExportRequestCaseDeliveryAsync_ShouldIncludeParentInterfaceAndAllEnvironments()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"request-delivery-{Guid.NewGuid():N}.apixdelivery.json");
        var requestCaseService = new FakeRequestCaseService();
        requestCaseService.Cases.Add(CreateCase("interface-1", "接口一", "http-interface", string.Empty, "GET", "/orders"));
        requestCaseService.Cases.Add(CreateCase("case-1", "成功", "http-case", "interface-1", "GET", "/orders"));
        var environmentRepository = new FakeProjectEnvironmentRepository
        {
            Environments =
            [
                new ProjectEnvironmentEntity
                {
                    Id = "env-1", ProjectId = "project-1", Name = "开发", BaseUrl = "https://api.demo.local/",
                    IsActive = true, SortOrder = 1, SourceProjectId = "project-1", SourceId = "env-source-1"
                },
                new ProjectEnvironmentEntity
                {
                    Id = "env-2", ProjectId = "project-1", Name = "生产", BaseUrl = "https://api.prod.local",
                    SortOrder = 2, SourceProjectId = "project-1", SourceId = "env-source-2"
                }
            ]
        };
        var variableRepository = new FakeEnvironmentVariableRepository
        {
            VariablesByEnvironment = new Dictionary<string, IReadOnlyList<EnvironmentVariableEntity>>(StringComparer.OrdinalIgnoreCase)
            {
                ["env-1"] =
                [
                    new EnvironmentVariableEntity
                    {
                        Id = "var-1", EnvironmentId = "env-1", EnvironmentName = "开发",
                        Key = "token", Value = "demo-value", IsEnabled = true
                    }
                ],
                ["env-2"] = []
            }
        };
        var service = new ProjectDataExportService(
            new FakeApiDocumentRepository(),
            requestCaseService,
            environmentRepository,
            variableRepository,
            new FakeRequestCaseDeliveryRepository());

        try
        {
            var result = await service.ExportRequestCaseDeliveryAsync(new RequestCaseDeliveryRequestDto
            {
                ProjectId = "project-1",
                ProjectName = "订单项目",
                SelectedRequestCaseIds = new HashSet<string>(["case-1"], StringComparer.OrdinalIgnoreCase),
                OutputFilePath = tempFile
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var package = JsonSerializer.Deserialize<RequestCaseDeliveryPackageDto>(await File.ReadAllTextAsync(tempFile));
            Assert.NotNull(package);
            Assert.Equal(RequestCaseDeliveryPackageDto.Format, package!.FormatName);
            Assert.Single(package.Interfaces);
            Assert.Single(package.RequestCases);
            Assert.Equal("interface-1", package.RequestCases[0].ParentInterfaceSourceId);
            Assert.Equal(2, package.Environments.Count);
            Assert.Equal("demo-value", package.Environments[0].Variables[0].Value);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task PreviewAndImportRequestCaseDeliveryAsync_ShouldExposeNewItemsAndBuildTransactionalPlan()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"request-delivery-{Guid.NewGuid():N}.apixdelivery.json");
        var package = new RequestCaseDeliveryPackageDto
        {
            Source = new RequestCaseDeliverySourceDto { ProjectId = "source-project", ProjectName = "来源项目" },
            Interfaces =
            [
                new RequestCaseDeliveryInterfaceDto
                {
                    SourceId = "source-interface", Name = "接口一", GroupName = "接口",
                    RequestSnapshot = new RequestSnapshotDto { Method = "GET", Url = "/orders" }
                }
            ],
            RequestCases =
            [
                new RequestCaseDeliveryCaseDto
                {
                    SourceId = "source-case", ParentInterfaceSourceId = "source-interface", Name = "成功",
                    GroupName = "用例", RequestSnapshot = new RequestSnapshotDto { Method = "GET", Url = "/orders" }
                }
            ],
            Environments =
            [
                new RequestCaseDeliveryEnvironmentDto
                {
                    SourceId = "source-environment", Name = "开发", BaseUrl = "https://api.demo.local",
                    Variables = [new RequestCaseDeliveryVariableDto { SourceId = "source-variable", Key = "token", Value = "demo" }]
                }
            ]
        };
        await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(package));
        var targetCases = new FakeRequestCaseService();
        var deliveryRepository = new FakeRequestCaseDeliveryRepository();
        var service = new ProjectDataExportService(
            new FakeApiDocumentRepository(),
            targetCases,
            new FakeProjectEnvironmentRepository(),
            new FakeEnvironmentVariableRepository(),
            deliveryRepository);

        try
        {
            var preview = await service.PreviewRequestCaseDeliveryAsync("target-project", tempFile, CancellationToken.None);

            Assert.True(preview.IsSuccess);
            Assert.NotNull(preview.Data);
            Assert.Equal(3, preview.Data!.NewCount);
            Assert.Equal(0, preview.Data.OrphanCount);

            var imported = await service.ImportRequestCaseDeliveryAsync(
                "target-project",
                tempFile,
                [],
                CancellationToken.None);

            Assert.True(imported.IsSuccess);
            Assert.NotNull(deliveryRepository.LastPlan);
            Assert.Single(deliveryRepository.LastPlan!.Endpoints);
            Assert.Equal(2, deliveryRepository.LastPlan.RequestCases.Count);
            Assert.Single(deliveryRepository.LastPlan.Environments);
            Assert.Single(deliveryRepository.LastPlan.Variables);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ImportRequestCaseDeliveryAsync_ShouldUseCopySuffixForDuplicateDecision()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"request-delivery-{Guid.NewGuid():N}.apixdelivery.json");
        var package = new RequestCaseDeliveryPackageDto
        {
            Source = new RequestCaseDeliverySourceDto { ProjectId = "source-project", ProjectName = "来源项目" },
            Interfaces =
            [
                new RequestCaseDeliveryInterfaceDto
                {
                    SourceId = "source-interface", Name = "接口一", GroupName = "接口",
                    RequestSnapshot = new RequestSnapshotDto { Method = "GET", Url = "/orders" }
                }
            ],
            RequestCases =
            [
                new RequestCaseDeliveryCaseDto
                {
                    SourceId = "source-case", ParentInterfaceSourceId = "source-interface", Name = "成功",
                    GroupName = "用例", RequestSnapshot = new RequestSnapshotDto { Method = "GET", Url = "/orders" }
                }
            ]
        };
        await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(package));
        var targetCases = new FakeRequestCaseService();
        targetCases.Cases.Add(CreateCase("existing-interface", "接口一", "http-interface", string.Empty, "GET", "/orders"));
        var deliveryRepository = new FakeRequestCaseDeliveryRepository();
        var service = new ProjectDataExportService(
            new FakeApiDocumentRepository(),
            targetCases,
            new FakeProjectEnvironmentRepository(),
            new FakeEnvironmentVariableRepository(),
            deliveryRepository);

        try
        {
            var result = await service.ImportRequestCaseDeliveryAsync(
                "target-project",
                tempFile,
                [new RequestCaseDeliveryDecisionDto
                {
                    Kind = "interface", SourceId = "source-interface", Action = RequestCaseDeliveryDecisionActions.Duplicate
                }],
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            var importedInterface = deliveryRepository.LastPlan!.RequestCases.Single(item => item.EntryType == "http-interface");
            Assert.Equal("接口一_copy", importedInterface.Name);
            Assert.Equal(1, result.Data!.DuplicatedCount);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task PreviewRequestCaseDeliveryAsync_ShouldMarkCaseAsOrphanWhenParentBusinessConflictIsSkipped()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"request-delivery-{Guid.NewGuid():N}.apixdelivery.json");
        var package = new RequestCaseDeliveryPackageDto
        {
            Source = new RequestCaseDeliverySourceDto { ProjectId = "source-project", ProjectName = "来源项目" },
            Interfaces =
            [
                new RequestCaseDeliveryInterfaceDto
                {
                    SourceId = "source-interface", Name = "同路径接口", GroupName = "接口",
                    RequestSnapshot = new RequestSnapshotDto { Method = "GET", Url = "/orders" }
                }
            ],
            RequestCases =
            [
                new RequestCaseDeliveryCaseDto
                {
                    SourceId = "source-case", ParentInterfaceSourceId = "source-interface", Name = "成功",
                    GroupName = "用例", RequestSnapshot = new RequestSnapshotDto { Method = "GET", Url = "/orders" }
                }
            ]
        };
        await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(package));
        var targetCases = new FakeRequestCaseService();
        targetCases.Cases.Add(new RequestCaseDto
        {
            Id = "target-interface", ProjectId = "target-project", EntryType = "http-interface",
            Name = "已有接口", GroupName = "接口",
            RequestSnapshot = new RequestSnapshotDto { Method = "GET", Url = "/orders" }
        });
        var service = new ProjectDataExportService(
            new FakeApiDocumentRepository(),
            targetCases,
            new FakeProjectEnvironmentRepository(),
            new FakeEnvironmentVariableRepository(),
            new FakeRequestCaseDeliveryRepository());

        try
        {
            var preview = await service.PreviewRequestCaseDeliveryAsync("target-project", tempFile, CancellationToken.None);

            Assert.True(preview.IsSuccess);
            Assert.Equal(RequestCaseDeliveryDecisionActions.Skip,
                preview.Data!.Items.Single(item => item.Kind == "interface").DefaultAction);
            var requestCaseItem = preview.Data.Items.Single(item => item.Kind == "request-case");
            Assert.Equal("孤儿", requestCaseItem.Status);
            Assert.Equal(1, preview.Data.OrphanCount);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ImportRequestCaseDeliveryAsync_ShouldPreserveExistingActiveEnvironment()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"request-delivery-{Guid.NewGuid():N}.apixdelivery.json");
        var package = new RequestCaseDeliveryPackageDto
        {
            Source = new RequestCaseDeliverySourceDto { ProjectId = "source-project", ProjectName = "来源项目" },
            Environments =
            [
                new RequestCaseDeliveryEnvironmentDto
                {
                    SourceId = "source-env", Name = "开发", BaseUrl = "https://api.demo.local", IsActive = false
                }
            ]
        };
        await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(package));
        var deliveryRepository = new FakeRequestCaseDeliveryRepository();
        var service = new ProjectDataExportService(
            new FakeApiDocumentRepository(),
            new FakeRequestCaseService(),
            new FakeProjectEnvironmentRepository
            {
                Environments =
                [
                    new ProjectEnvironmentEntity
                    {
                        Id = "target-env", ProjectId = "target-project", Name = "开发",
                        BaseUrl = "https://existing.local", IsActive = true,
                        SourceProjectId = "source-project", SourceId = "source-env"
                    }
                ]
            },
            new FakeEnvironmentVariableRepository(),
            deliveryRepository);

        try
        {
            var result = await service.ImportRequestCaseDeliveryAsync("target-project", tempFile, [], CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(deliveryRepository.LastPlan!.Environments.Single().IsActive);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static RequestCaseDto CreateCase(string id, string name, string entryType, string parentId, string method, string url)
    {
        return new RequestCaseDto
        {
            Id = id,
            ProjectId = "project-1",
            EntryType = entryType,
            Name = name,
            GroupName = entryType == "http-interface" ? "接口" : "用例",
            ParentId = parentId,
            RequestSnapshot = new RequestSnapshotDto { EndpointId = $"delivery-import:source-project:{id}", Name = name, Method = method, Url = url },
            SourceProjectId = "source-project",
            SourceId = id
        };
    }

    private sealed class FakeApiDocumentRepository : IApiDocumentRepository
    {
        public Task<IReadOnlyList<ApiDocumentEntity>> GetDocumentsAsync(string projectId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ApiDocumentEntity>>([]);
        }

        public Task<ApiDocumentEntity?> GetByIdAsync(string projectId, string documentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<ApiDocumentEntity?>(null);
        }

        public Task<IReadOnlyList<ApiEndpointEntity>> GetEndpointsByDocumentIdAsync(string documentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ApiEndpointEntity>>([]);
        }

        public Task<IReadOnlyList<ApiProjectEndpointEntity>> GetEndpointsByProjectIdAsync(string projectId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ApiProjectEndpointEntity>>([]);
        }

        public Task<IReadOnlyList<ApiEndpointEntity>> GetEndpointDetailsByProjectIdAsync(string projectId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ApiEndpointEntity>>([]);
        }

        public Task<IReadOnlyList<RequestParameterEntity>> GetParametersByEndpointIdsAsync(IEnumerable<string> endpointIds, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<RequestParameterEntity>>([]);
        }

        public Task DeleteEndpointsByIdsAsync(IEnumerable<string> endpointIds, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SaveDocumentGraphAsync(ApiDocumentEntity document, IReadOnlyList<ApiEndpointEntity> endpoints, IReadOnlyList<RequestParameterEntity> parameters, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProjectEnvironmentRepository : IProjectEnvironmentRepository
    {
        public IReadOnlyList<ProjectEnvironmentEntity> Environments { get; init; } = [];

        public Task<IReadOnlyList<ProjectEnvironmentEntity>> GetByProjectAsync(string projectId, CancellationToken cancellationToken)
            => Task.FromResult(Environments);
        public Task<ProjectEnvironmentEntity?> GetByIdAsync(string id, CancellationToken cancellationToken)
            => Task.FromResult(Environments.FirstOrDefault(item => item.Id == id));
        public Task<ProjectEnvironmentEntity?> GetByNameAsync(string projectId, string name, CancellationToken cancellationToken)
            => Task.FromResult(Environments.FirstOrDefault(item => item.ProjectId == projectId && item.Name == name));
        public Task UpsertAsync(ProjectEnvironmentEntity entity, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetActiveAsync(string projectId, string environmentId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeEnvironmentVariableRepository : IEnvironmentVariableRepository
    {
        public IReadOnlyDictionary<string, IReadOnlyList<EnvironmentVariableEntity>> VariablesByEnvironment { get; init; } =
            new Dictionary<string, IReadOnlyList<EnvironmentVariableEntity>>(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<EnvironmentVariableEntity>> GetByEnvironmentAsync(string environmentId, CancellationToken cancellationToken)
            => Task.FromResult(VariablesByEnvironment.GetValueOrDefault(environmentId) ?? []);
        public Task<IReadOnlyDictionary<string, IReadOnlyList<EnvironmentVariableEntity>>> GetByEnvironmentIdsAsync(IEnumerable<string> environmentIds, CancellationToken cancellationToken)
            => Task.FromResult(VariablesByEnvironment);
        public Task UpsertAsync(EnvironmentVariableEntity entity, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpsertRangeAsync(IReadOnlyList<EnvironmentVariableEntity> entities, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyDictionary<string, string>> GetEnabledDictionaryAsync(string environmentId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        public Task DeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeRequestCaseDeliveryRepository : IRequestCaseDeliveryRepository
    {
        public RequestCaseDeliveryImportPlan? LastPlan { get; private set; }

        public Task ImportAsync(RequestCaseDeliveryImportPlan plan, CancellationToken cancellationToken)
        {
            LastPlan = plan;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRequestHistoryService : IRequestHistoryService
    {
        public IReadOnlyList<RequestHistoryItemDto> History { get; init; } = [];

        public Task<IReadOnlyList<RequestHistoryItemDto>> GetHistoryAsync(string projectId, CancellationToken cancellationToken)
            => Task.FromResult(History);

        public Task<RequestHistoryItemDto?> GetDetailAsync(string projectId, string id, CancellationToken cancellationToken)
            => Task.FromResult(History.FirstOrDefault(item => item.Id == id));

        public Task<IResultModel<RequestHistoryItemDto>> AddAsync(string projectId, RequestSnapshotDto request, ResponseSnapshotDto? response, CancellationToken cancellationToken)
            => Task.FromResult(ResultModel<RequestHistoryItemDto>.Success(new RequestHistoryItemDto()));

        public Task<IResultModel<bool>> ClearAsync(string projectId, CancellationToken cancellationToken)
            => Task.FromResult(ResultModel<bool>.Success(true));

        public Task<IResultModel<bool>> ClearAllAsync(CancellationToken cancellationToken)
            => Task.FromResult(ResultModel<bool>.Success(true));

        public Task<IResultModel<bool>> DeleteAsync(string projectId, string id, CancellationToken cancellationToken)
            => Task.FromResult(ResultModel<bool>.Success(true));

        public Task<int> CleanupOlderThanAsync(TimeSpan retention, CancellationToken cancellationToken)
            => Task.FromResult(0);
    }

    private sealed class FakeProjectExportSettingsRepository : IProjectExportSettingsRepository
    {
        public Dictionary<string, ProjectExportSettingsEntity> Store { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<ProjectExportSettingsEntity?> GetAsync(string projectId, CancellationToken cancellationToken)
            => Task.FromResult(Store.GetValueOrDefault(projectId));

        public Task UpsertAsync(ProjectExportSettingsEntity entity, CancellationToken cancellationToken)
        {
            Store[entity.ProjectId] = entity;
            return Task.CompletedTask;
        }
    }
}
