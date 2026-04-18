using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Implementations;
using ApixPress.App.Services.Implementations;
using Azrng.Core.Json;
using Microsoft.Extensions.Options;

namespace ApixPress.App.Tests.Repositories;

public sealed partial class WorkspacePersistenceTests
{
    private static SysTextJsonSerializer CreateSerializer()
    {
        return new SysTextJsonSerializer(Options.Create(new DefaultJsonSerializerOptions()));
    }

    [Fact]
    public async Task ProjectWorkspaceService_ShouldCreateDefaultProjectAndInitialEnvironment()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var service = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var environmentService = new EnvironmentVariableService(new EnvironmentVariableRepository(factory), environmentRepository);

        var saveResult = await service.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "支付中心",
            Description = "默认项目"
        }, CancellationToken.None);

        Assert.True(saveResult.IsSuccess);
        Assert.NotNull(saveResult.Data);
        Assert.True(saveResult.Data!.IsDefault);

        var startupProject = await service.GetStartupProjectAsync(CancellationToken.None);
        Assert.NotNull(startupProject);
        Assert.Equal(saveResult.Data.Id, startupProject!.Id);

        var environments = await environmentService.GetEnvironmentsAsync(saveResult.Data.Id, CancellationToken.None);
        var environment = Assert.Single(environments);
        Assert.Equal("开发", environment.Name);
        Assert.True(environment.IsActive);
    }

    [Fact]
    public async Task RequestCaseService_ShouldPersistAndLoadCaseWithinProject()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var projectService = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var serializer = CreateSerializer();
        var caseRepository = new RequestCaseRepository(factory);
        var caseService = new RequestCaseService(caseRepository, serializer);

        var projectA = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "项目 A"
        }, CancellationToken.None)).Data!;
        var projectB = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "项目 B"
        }, CancellationToken.None)).Data!;

        var saveResult = await caseService.SaveAsync(new RequestCaseDto
        {
            ProjectId = projectA.Id,
            EntryType = "quick-request",
            Name = "获取用户详情",
            GroupName = "用户",
            Tags = ["smoke", "demo"],
            Description = "保存当前 GET 请求",
            RequestSnapshot = new RequestSnapshotDto
            {
                Method = "GET",
                Url = "https://api.example.com/users/42"
            },
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        Assert.True(saveResult.IsSuccess);

        var projectACases = await caseService.GetCasesAsync(projectA.Id, CancellationToken.None);
        var saved = Assert.Single(projectACases);
        Assert.Equal("quick-request", saved.EntryType);
        Assert.Equal("获取用户详情", saved.Name);
        Assert.Equal("用户", saved.GroupName);
        Assert.Equal("https://api.example.com/users/42", saved.RequestSnapshot.Url);
        Assert.Equal(2, saved.Tags.Count);

        var projectBCases = await caseService.GetCasesAsync(projectB.Id, CancellationToken.None);
        Assert.Empty(projectBCases);
    }

    [Fact]
    public async Task RequestCaseService_ShouldPersistHttpInterfaceHierarchy()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var projectService = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var serializer = CreateSerializer();
        var caseRepository = new RequestCaseRepository(factory);
        var caseService = new RequestCaseService(caseRepository, serializer);

        var project = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "接口树项目"
        }, CancellationToken.None)).Data!;

        var interfaceResult = await caseService.SaveAsync(new RequestCaseDto
        {
            ProjectId = project.Id,
            EntryType = "http-interface",
            Name = "获取单个用户信息",
            GroupName = "接口",
            FolderPath = "用户",
            Description = "接口定义",
            RequestSnapshot = new RequestSnapshotDto
            {
                Method = "GET",
                Url = "/users/{id}"
            },
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        Assert.True(interfaceResult.IsSuccess);
        Assert.NotNull(interfaceResult.Data);

        var caseResult = await caseService.SaveAsync(new RequestCaseDto
        {
            ProjectId = project.Id,
            EntryType = "http-case",
            ParentId = interfaceResult.Data!.Id,
            Name = "成功",
            GroupName = "用例",
            FolderPath = "用户",
            Description = "成功响应",
            RequestSnapshot = new RequestSnapshotDto
            {
                Method = "GET",
                Url = "/users/{id}"
            },
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        Assert.True(caseResult.IsSuccess);

        var cases = await caseService.GetCasesAsync(project.Id, CancellationToken.None);
        var savedInterface = Assert.Single(cases, item => item.EntryType == "http-interface");
        var savedCase = Assert.Single(cases, item => item.EntryType == "http-case");

        Assert.Equal("用户", savedInterface.FolderPath);
        Assert.Equal("/users/{id}", savedInterface.RequestSnapshot.Url);
        Assert.Equal(savedInterface.Id, savedCase.ParentId);
        Assert.Equal("用户", savedCase.FolderPath);
    }

    [Fact]
    public async Task RequestCaseService_ShouldUpdateSavedHttpInterfaceNameById()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var projectService = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var serializer = CreateSerializer();
        var caseRepository = new RequestCaseRepository(factory);
        var caseService = new RequestCaseService(caseRepository, serializer);

        var project = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "重命名项目"
        }, CancellationToken.None)).Data!;

        var created = await caseService.SaveAsync(new RequestCaseDto
        {
            ProjectId = project.Id,
            EntryType = "http-interface",
            Name = "获取用户列表",
            GroupName = "接口",
            FolderPath = "用户",
            Description = "初始接口名",
            RequestSnapshot = new RequestSnapshotDto
            {
                Method = "GET",
                Url = "/users"
            },
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.NotNull(created.Data);

        var renamed = await caseService.SaveAsync(new RequestCaseDto
        {
            Id = created.Data!.Id,
            ProjectId = project.Id,
            EntryType = "http-interface",
            Name = "查询用户列表",
            GroupName = "接口",
            FolderPath = "用户",
            Description = "更新后的接口名",
            RequestSnapshot = new RequestSnapshotDto
            {
                Method = "GET",
                Url = "/users"
            },
            UpdatedAt = DateTime.UtcNow.AddMinutes(1)
        }, CancellationToken.None);

        Assert.True(renamed.IsSuccess);

        var cases = await caseService.GetCasesAsync(project.Id, CancellationToken.None);
        var savedInterface = Assert.Single(cases);

        Assert.Equal(created.Data.Id, savedInterface.Id);
        Assert.Equal("查询用户列表", savedInterface.Name);
        Assert.Equal("更新后的接口名", savedInterface.Description);
    }

    [Fact]
    public async Task RequestCaseService_ShouldSyncImportedHttpInterfacesByMethodAndPath()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var projectService = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var serializer = CreateSerializer();
        var caseRepository = new RequestCaseRepository(factory);
        var caseService = new RequestCaseService(caseRepository, serializer);

        var project = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "导入同步项目"
        }, CancellationToken.None)).Data!;

        await caseService.SyncImportedHttpInterfacesAsync(project.Id,
        [
            new ApiEndpointDto
            {
                GroupName = "用户",
                Name = "查询用户列表",
                Method = "GET",
                Path = "/users"
            },
            new ApiEndpointDto
            {
                GroupName = "订单",
                Name = "创建订单",
                Method = "POST",
                Path = "/orders",
                Description = "从 Swagger 导入"
            }
        ], CancellationToken.None);

        var initialCases = await caseService.GetCasesAsync(project.Id, CancellationToken.None);
        Assert.Equal(2, initialCases.Count);
        var importedUserInterface = Assert.Single(initialCases, item => item.RequestSnapshot.Url == "/users");

        await caseService.SaveAsync(new RequestCaseDto
        {
            ProjectId = project.Id,
            EntryType = "http-case",
            ParentId = importedUserInterface.Id,
            Name = "成功",
            GroupName = "用例",
            FolderPath = "用户",
            RequestSnapshot = new RequestSnapshotDto
            {
                Method = "GET",
                Url = "/users"
            },
            UpdatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        await caseService.SyncImportedHttpInterfacesAsync(project.Id,
        [
            new ApiEndpointDto
            {
                GroupName = "用户中心",
                Name = "获取用户列表",
                Method = "GET",
                Path = "/users",
                Description = "已更新"
            }
        ], CancellationToken.None);

        var syncedCases = await caseService.GetCasesAsync(project.Id, CancellationToken.None);
        var syncedInterface = Assert.Single(syncedCases, item => item.EntryType == "http-interface");

        Assert.Equal(importedUserInterface.Id, syncedInterface.Id);
        Assert.Equal("获取用户列表", syncedInterface.Name);
        Assert.Equal("用户中心", syncedInterface.FolderPath);
        Assert.Equal("已更新", syncedInterface.Description);
        Assert.Equal("swagger-import:GET /users", syncedInterface.RequestSnapshot.EndpointId);
        Assert.DoesNotContain(syncedCases, item => item.RequestSnapshot.Url == "/orders");
        var syncedCase = Assert.Single(syncedCases, item => item.EntryType == "http-case");
        Assert.Equal(syncedInterface.Id, syncedCase.ParentId);
    }

    [Fact]
    public async Task EnvironmentVariableService_ShouldReturnEnabledDictionaryForActiveEnvironment()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var projectService = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var environmentService = new EnvironmentVariableService(new EnvironmentVariableRepository(factory), environmentRepository);

        var project = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "环境测试"
        }, CancellationToken.None)).Data!;

        var environments = await environmentService.GetEnvironmentsAsync(project.Id, CancellationToken.None);
        var environment = Assert.Single(environments);

        await environmentService.SaveEnvironmentAsync(new ProjectEnvironmentDto
        {
            Id = environment.Id,
            ProjectId = project.Id,
            Name = environment.Name,
            BaseUrl = "https://api.example.com",
            IsActive = true,
            SortOrder = environment.SortOrder
        }, CancellationToken.None);

        await environmentService.SaveVariableAsync(new EnvironmentVariableDto
        {
            EnvironmentId = environment.Id,
            Key = "tenantId",
            Value = "demo",
            IsEnabled = true
        }, CancellationToken.None);

        await environmentService.SaveVariableAsync(new EnvironmentVariableDto
        {
            EnvironmentId = environment.Id,
            Key = "token",
            Value = "secret",
            IsEnabled = false
        }, CancellationToken.None);

        var active = await environmentService.GetActiveDictionaryAsync(environment.Id, CancellationToken.None);

        Assert.Single(active);
        Assert.Equal("demo", active["tenantId"]);
        Assert.False(active.ContainsKey("token"));
    }

    [Fact]
    public async Task EnvironmentVariableService_ShouldTrimTrailingSlashWhenSavingBaseUrl()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var projectService = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var environmentService = new EnvironmentVariableService(new EnvironmentVariableRepository(factory), environmentRepository);

        var project = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "BaseUrl 规整测试"
        }, CancellationToken.None)).Data!;

        var environment = Assert.Single(await environmentService.GetEnvironmentsAsync(project.Id, CancellationToken.None));

        var saved = (await environmentService.SaveEnvironmentAsync(new ProjectEnvironmentDto
        {
            Id = environment.Id,
            ProjectId = project.Id,
            Name = environment.Name,
            BaseUrl = " http://localhost:5172/ ",
            IsActive = true,
            SortOrder = environment.SortOrder
        }, CancellationToken.None)).Data!;

        Assert.Equal("http://localhost:5172", saved.BaseUrl);

        var reloaded = Assert.Single(await environmentService.GetEnvironmentsAsync(project.Id, CancellationToken.None));
        Assert.Equal("http://localhost:5172", reloaded.BaseUrl);
    }

    [Fact]
    public async Task EnvironmentVariableService_ShouldBatchSaveVariablesForEnvironment()
    {
        using var factory = new TestSqliteConnectionFactory();
        await factory.InitializeAsync();

        var projectRepository = new ProjectWorkspaceRepository(factory);
        var environmentRepository = new ProjectEnvironmentRepository(factory);
        var projectService = new ProjectWorkspaceService(projectRepository, environmentRepository);
        var environmentService = new EnvironmentVariableService(new EnvironmentVariableRepository(factory), environmentRepository);

        var project = (await projectService.SaveAsync(new ProjectWorkspaceDto
        {
            Name = "批量环境变量测试"
        }, CancellationToken.None)).Data!;

        var environment = Assert.Single(await environmentService.GetEnvironmentsAsync(project.Id, CancellationToken.None));
        var saveResult = await environmentService.SaveVariablesAsync(
            environment,
            [
                new EnvironmentVariableDto
                {
                    EnvironmentId = environment.Id,
                    Key = "tenantId",
                    Value = "demo",
                    IsEnabled = true
                },
                new EnvironmentVariableDto
                {
                    EnvironmentId = environment.Id,
                    Key = "region",
                    Value = "cn",
                    IsEnabled = true
                }
            ],
            CancellationToken.None);

        Assert.True(saveResult.IsSuccess);
        Assert.NotNull(saveResult.Data);
        Assert.Equal(2, saveResult.Data!.Count);

        var reloaded = await environmentService.GetVariablesAsync(environment.Id, CancellationToken.None);
        Assert.Equal(2, reloaded.Count);
        Assert.Contains(reloaded, item => item.Key == "tenantId" && item.Value == "demo");
        Assert.Contains(reloaded, item => item.Key == "region" && item.Value == "cn");
    }

}
