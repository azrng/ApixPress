using System.Windows.Input;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Tests.ViewModels;

public sealed class ProjectWorkspaceTreeBuilderTests
{
    [Fact]
    public void Build_ShouldPlaceUngroupedInterfaceAtRootWithoutDefaultModuleFolder()
    {
        var root = ProjectWorkspaceTreeBuilder.BuildInterfaceRoot(
            [CreateItem(CreateCase("http-interface", "get接口", folderPath: string.Empty))],
            CreateCommands());

        // 未分组接口直接落在接口根节点下，不再生成"默认模块"目录层
        var interfaceNode = Assert.Single(root.Children);
        Assert.Equal("http-interface", interfaceNode.NodeType);
        Assert.Equal("get接口", interfaceNode.Title);
    }

    [Fact]
    public void Build_ShouldCreateFolderNodeFromFolderRow()
    {
        var folderRow = CreateItem(CreateCase("folder", "WeatherForecast", folderPath: string.Empty));
        var root = ProjectWorkspaceTreeBuilder.BuildInterfaceRoot([folderRow], CreateCommands());

        var folder = Assert.Single(root.Children);
        Assert.Equal("folder", folder.NodeType);
        Assert.Equal("WeatherForecast", folder.Title);
        Assert.Equal("WeatherForecast", folder.FolderFullPath);
        Assert.Equal(1, folder.FolderDepth);
        Assert.True(folder.CanCreateSubfolder);
        Assert.NotNull(folder.SourceCase);
    }

    [Fact]
    public void Build_ShouldCapFolderDepthAtTwoLevels()
    {
        var root = ProjectWorkspaceTreeBuilder.BuildInterfaceRoot(
            [CreateItem(CreateCase("http-interface", "深层接口", folderPath: "a/b/c/d"))],
            CreateCommands());

        var level1 = Assert.Single(root.Children);
        Assert.Equal("folder", level1.NodeType);
        Assert.Equal("a (1)", level1.Title);
        Assert.Equal(1, level1.FolderDepth);
        level1.EnsureChildrenLoaded();

        var level2 = Assert.Single(level1.Children);
        Assert.Equal("folder", level2.NodeType);
        Assert.Equal("b (1)", level2.Title);
        Assert.Equal("a/b", level2.FolderFullPath);
        Assert.Equal(2, level2.FolderDepth);
        // 目录层级达到上限后不再提供“新建子目录”
        Assert.False(level2.CanCreateSubfolder);
        level2.EnsureChildrenLoaded();

        var interfaceNode = Assert.Single(level2.Children);
        Assert.Equal("http-interface", interfaceNode.NodeType);
        Assert.Equal("深层接口", interfaceNode.Title);
    }

    [Fact]
    public void Build_ShouldMergeFolderRowAndInterfacePathIntoSameFolder()
    {
        var folderRow = CreateItem(CreateCase("folder", "a", folderPath: string.Empty));
        var interfaceItem = CreateItem(CreateCase("http-interface", "接口A", folderPath: "a"));
        var root = ProjectWorkspaceTreeBuilder.BuildInterfaceRoot([folderRow, interfaceItem], CreateCommands());

        var folder = Assert.Single(root.Children);
        Assert.Equal("a (1)", folder.Title);
        Assert.NotNull(folder.SourceCase);
        folder.EnsureChildrenLoaded();

        var interfaceNode = Assert.Single(folder.Children);
        Assert.Equal("http-interface", interfaceNode.NodeType);
    }

    [Fact]
    public void Build_ShouldGroupCasesUnderInterface()
    {
        var interfaceId = "case-parent";
        var interfaceItem = CreateItem(CreateCase("http-interface", "获取信息", folderPath: string.Empty, id: interfaceId));
        var caseItem = CreateItem(CreateCase("http-case", "成功", folderPath: string.Empty, parentId: interfaceId));
        var root = ProjectWorkspaceTreeBuilder.BuildInterfaceRoot([interfaceItem, caseItem], CreateCommands());

        var interfaceNode = Assert.Single(root.Children);
        Assert.Equal("获取信息 (1)", interfaceNode.Title);
        interfaceNode.EnsureChildrenLoaded();

        var caseNode = Assert.Single(interfaceNode.Children);
        Assert.Equal("http-case", caseNode.NodeType);
        Assert.Equal("成功", caseNode.Title);
    }

    [Fact]
    public void BuildQuickRequests_ShouldIgnoreFolderRows()
    {
        var quickItems = ProjectWorkspaceTreeBuilder.BuildQuickRequests(
            [
                CreateItem(CreateCase("quick-request", "快捷1", folderPath: string.Empty)),
                CreateItem(CreateCase("folder", "目录A", folderPath: string.Empty))
            ],
            new FakeCommand());

        var quick = Assert.Single(quickItems);
        Assert.Equal("快捷1", quick.Title);
    }

    private static WorkspaceTreeItemCommands CreateCommands() =>
        new(new FakeCommand(), new FakeCommand(), new FakeCommand());

    private static RequestCaseDto CreateCase(
        string entryType,
        string name,
        string folderPath,
        string? id = null,
        string? parentId = null)
    {
        return new RequestCaseDto
        {
            Id = id ?? Guid.NewGuid().ToString("N"),
            ProjectId = "p1",
            EntryType = entryType,
            Name = name,
            FolderPath = folderPath,
            ParentId = parentId ?? string.Empty,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static RequestCaseItemViewModel CreateItem(RequestCaseDto source)
    {
        return new RequestCaseItemViewModel
        {
            Id = source.Id,
            Name = source.Name,
            SourceCase = source
        };
    }

    private sealed class FakeCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }
}
