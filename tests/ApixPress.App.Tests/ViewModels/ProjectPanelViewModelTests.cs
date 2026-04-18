using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels;
using Azrng.Core.Results;

namespace ApixPress.App.Tests.ViewModels;

public sealed class ProjectPanelViewModelTests
{
    [Fact]
    public async Task CreateProjectCommand_ShouldCreateProjectThroughCreationViewModel()
    {
        var service = new FakeProjectWorkspaceService();
        var viewModel = new ProjectPanelViewModel(service);
        var createdCount = 0;
        viewModel.ProjectCreated += () => createdCount++;

        viewModel.Creation.DraftProjectName = "订单项目";
        viewModel.Creation.DraftProjectDescription = "订单接口";

        await viewModel.Creation.CreateProjectCommand.ExecuteAsync(null);

        var created = Assert.Single(viewModel.Projects);
        Assert.Equal("订单项目", created.Name);
        Assert.Equal("订单接口", created.Description);
        Assert.True(created.IsDefault);
        Assert.Same(created, viewModel.SelectedProject);
        Assert.Equal(string.Empty, viewModel.Creation.DraftProjectName);
        Assert.Equal(string.Empty, viewModel.Creation.DraftProjectDescription);
        Assert.Equal(1, createdCount);
    }

    [Fact]
    public async Task UseProjectTemplateCommand_ShouldSkipExistingProjectNames()
    {
        var service = new FakeProjectWorkspaceService();
        service.SeedProjects(
        [
            new("project-1", "新项目 1", "已存在", true),
            new("project-2", "新项目 2", "已存在", false)
        ]);
        var viewModel = new ProjectPanelViewModel(service);
        await viewModel.LoadProjectsAsync();

        viewModel.Creation.UseProjectTemplateCommand.Execute(null);

        Assert.Equal("新项目 3", viewModel.Creation.DraftProjectName);
    }

    private sealed class FakeProjectWorkspaceService : IProjectWorkspaceService
    {
        private readonly List<ProjectWorkspaceDto> _projects = [];

        public void SeedProjects(IEnumerable<(string Id, string Name, string Description, bool IsDefault)> projects)
        {
            _projects.Clear();
            _projects.AddRange(projects.Select(project => new ProjectWorkspaceDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                IsDefault = project.IsDefault
            }));
        }

        public Task<IReadOnlyList<ProjectWorkspaceDto>> GetProjectsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ProjectWorkspaceDto>>(_projects
                .Select(CloneProject)
                .ToList());
        }

        public Task<ProjectWorkspaceDto?> GetStartupProjectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<ProjectWorkspaceDto?>(_projects.FirstOrDefault(item => item.IsDefault) is { } project
                ? CloneProject(project)
                : null);
        }

        public Task<IResultModel<ProjectWorkspaceDto>> SaveAsync(ProjectWorkspaceDto project, CancellationToken cancellationToken)
        {
            var saved = new ProjectWorkspaceDto
            {
                Id = string.IsNullOrWhiteSpace(project.Id) ? Guid.NewGuid().ToString("N") : project.Id,
                Name = project.Name,
                Description = project.Description,
                IsDefault = project.IsDefault,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt
            };

            _projects.RemoveAll(item => string.Equals(item.Id, saved.Id, StringComparison.OrdinalIgnoreCase));
            _projects.Add(saved);
            return Task.FromResult<IResultModel<ProjectWorkspaceDto>>(ResultModel<ProjectWorkspaceDto>.Success(CloneProject(saved)));
        }

        public Task<IResultModel<ProjectWorkspaceDto>> SetDefaultAsync(string projectId, CancellationToken cancellationToken)
        {
            var updatedProjects = _projects
                .Select(project => new ProjectWorkspaceDto
                {
                    Id = project.Id,
                    Name = project.Name,
                    Description = project.Description,
                    IsDefault = string.Equals(project.Id, projectId, StringComparison.OrdinalIgnoreCase),
                    CreatedAt = project.CreatedAt,
                    UpdatedAt = project.UpdatedAt
                })
                .ToList();
            _projects.Clear();
            _projects.AddRange(updatedProjects);
            var selected = _projects.FirstOrDefault(item => item.IsDefault);

            return Task.FromResult<IResultModel<ProjectWorkspaceDto>>(selected is null
                ? ResultModel<ProjectWorkspaceDto>.Failure("项目不存在")
                : ResultModel<ProjectWorkspaceDto>.Success(CloneProject(selected)));
        }

        public Task<IResultModel<bool>> DeleteAsync(string projectId, CancellationToken cancellationToken)
        {
            _projects.RemoveAll(item => string.Equals(item.Id, projectId, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult<IResultModel<bool>>(ResultModel<bool>.Success(true));
        }

        private static ProjectWorkspaceDto CloneProject(ProjectWorkspaceDto project)
        {
            return new ProjectWorkspaceDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                IsDefault = project.IsDefault,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt
            };
        }
    }
}
