using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.DependencyInjection;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Implementations;

public sealed class ProjectWorkspaceService : IProjectWorkspaceService, ITransientDependency
{
    private readonly IProjectWorkspaceRepository _projectWorkspaceRepository;
    private readonly IProjectEnvironmentRepository _projectEnvironmentRepository;

    public ProjectWorkspaceService(
        IProjectWorkspaceRepository projectWorkspaceRepository,
        IProjectEnvironmentRepository projectEnvironmentRepository)
    {
        _projectWorkspaceRepository = projectWorkspaceRepository;
        _projectEnvironmentRepository = projectEnvironmentRepository;
    }

    public async Task<IReadOnlyList<ProjectWorkspaceDto>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        var projects = await _projectWorkspaceRepository.GetProjectsAsync(cancellationToken);
        return projects.Select(ToDto).ToList();
    }

    public async Task<ProjectWorkspaceDto?> GetStartupProjectAsync(CancellationToken cancellationToken)
    {
        var projects = await _projectWorkspaceRepository.GetProjectsAsync(cancellationToken);
        var selected = projects.FirstOrDefault(item => item.IsDefault) ?? projects.FirstOrDefault();
        return selected is null ? null : ToDto(selected);
    }

    public async Task<IResultModel<ProjectWorkspaceDto>> SaveAsync(ProjectWorkspaceDto project, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(project.Name))
        {
            return ResultModel<ProjectWorkspaceDto>.Failure("项目名称不能为空。", "project_name_required");
        }

        var existingByName = await _projectWorkspaceRepository.GetByNameAsync(project.Name, cancellationToken);
        if (existingByName is not null && !string.Equals(existingByName.Id, project.Id, StringComparison.OrdinalIgnoreCase))
        {
            return ResultModel<ProjectWorkspaceDto>.Failure("项目名称已存在，请修改后重试。", "project_name_duplicated");
        }

        var existing = string.IsNullOrWhiteSpace(project.Id)
            ? null
            : await _projectWorkspaceRepository.GetByIdAsync(project.Id, cancellationToken);
        var currentProjects = await _projectWorkspaceRepository.GetProjectsAsync(cancellationToken);
        var entity = new ProjectWorkspaceEntity
        {
            Id = string.IsNullOrWhiteSpace(project.Id) ? Guid.NewGuid().ToString("N") : project.Id,
            Name = project.Name,
            Description = project.Description,
            IsDefault = project.IsDefault || currentProjects.Count == 0 || existing?.IsDefault == true,
            CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _projectWorkspaceRepository.UpsertAsync(entity, cancellationToken);
        if (entity.IsDefault || !currentProjects.Any(item => item.IsDefault && item.Id != entity.Id))
        {
            await _projectWorkspaceRepository.SetDefaultAsync(entity.Id, cancellationToken);
            entity.IsDefault = true;
        }

        if (existing is null)
        {
            var initialEnvironmentId = Guid.NewGuid().ToString("N");
            await _projectEnvironmentRepository.UpsertAsync(new ProjectEnvironmentEntity
            {
                Id = initialEnvironmentId,
                ProjectId = entity.Id,
                Name = "开发",
                BaseUrl = string.Empty,
                IsActive = true,
                SortOrder = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }, cancellationToken);
            await _projectEnvironmentRepository.SetActiveAsync(entity.Id, initialEnvironmentId, cancellationToken);
        }

        return ResultModel<ProjectWorkspaceDto>.Success(ToDto(entity));
    }

    public async Task<IResultModel<ProjectWorkspaceDto>> SetDefaultAsync(string projectId, CancellationToken cancellationToken)
    {
        var project = await _projectWorkspaceRepository.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ResultModel<ProjectWorkspaceDto>.Failure("未找到目标项目。", "project_not_found");
        }

        await _projectWorkspaceRepository.SetDefaultAsync(projectId, cancellationToken);
        project.IsDefault = true;
        project.UpdatedAt = DateTime.UtcNow;
        return ResultModel<ProjectWorkspaceDto>.Success(ToDto(project));
    }

    public async Task<IResultModel<bool>> DeleteAsync(string projectId, CancellationToken cancellationToken)
    {
        var project = await _projectWorkspaceRepository.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ResultModel<bool>.Failure("未找到待删除的项目。", "project_not_found");
        }

        await _projectWorkspaceRepository.DeleteAsync(projectId, cancellationToken);
        var remaining = await _projectWorkspaceRepository.GetProjectsAsync(cancellationToken);
        if (remaining.Count > 0 && !remaining.Any(item => item.IsDefault))
        {
            await _projectWorkspaceRepository.SetDefaultAsync(remaining[0].Id, cancellationToken);
        }

        return ResultModel<bool>.Success(true);
    }

    private static ProjectWorkspaceDto ToDto(ProjectWorkspaceEntity entity)
    {
        return new ProjectWorkspaceDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            IsDefault = entity.IsDefault,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
