using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.DependencyInjection;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Implementations;

public sealed class EnvironmentVariableService : IEnvironmentVariableService, ITransientDependency
{
    private readonly IEnvironmentVariableRepository _environmentVariableRepository;
    private readonly IProjectEnvironmentRepository _projectEnvironmentRepository;

    public EnvironmentVariableService(
        IEnvironmentVariableRepository environmentVariableRepository,
        IProjectEnvironmentRepository projectEnvironmentRepository)
    {
        _environmentVariableRepository = environmentVariableRepository;
        _projectEnvironmentRepository = projectEnvironmentRepository;
    }

    public async Task<IReadOnlyList<ProjectEnvironmentDto>> GetEnvironmentsAsync(string projectId, CancellationToken cancellationToken)
    {
        var items = await _projectEnvironmentRepository.GetByProjectAsync(projectId, cancellationToken);
        return items.Select(ToEnvironmentDto).ToList();
    }

    public async Task<IResultModel<ProjectEnvironmentDto>> SaveEnvironmentAsync(ProjectEnvironmentDto environment, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(environment.ProjectId))
        {
            return ResultModel<ProjectEnvironmentDto>.Failure("环境必须归属到一个项目。", "environment_project_required");
        }

        if (string.IsNullOrWhiteSpace(environment.Name))
        {
            return ResultModel<ProjectEnvironmentDto>.Failure("环境名称不能为空。", "environment_name_required");
        }

        var existingByName = await _projectEnvironmentRepository.GetByNameAsync(environment.ProjectId, environment.Name, cancellationToken);
        if (existingByName is not null && !string.Equals(existingByName.Id, environment.Id, StringComparison.OrdinalIgnoreCase))
        {
            return ResultModel<ProjectEnvironmentDto>.Failure("同一项目下环境名称不能重复。", "environment_name_duplicated");
        }

        var existing = string.IsNullOrWhiteSpace(environment.Id)
            ? null
            : await _projectEnvironmentRepository.GetByIdAsync(environment.Id, cancellationToken);
        var currentEnvironments = await _projectEnvironmentRepository.GetByProjectAsync(environment.ProjectId, cancellationToken);
        var normalizedBaseUrl = NormalizeBaseUrl(environment.BaseUrl);

        var entity = new ProjectEnvironmentEntity
        {
            Id = string.IsNullOrWhiteSpace(environment.Id) ? Guid.NewGuid().ToString("N") : environment.Id,
            ProjectId = environment.ProjectId,
            Name = environment.Name,
            BaseUrl = normalizedBaseUrl,
            IsActive = environment.IsActive || currentEnvironments.Count == 0 || existing?.IsActive == true,
            SortOrder = existing?.SortOrder ?? environment.SortOrder,
            CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (entity.SortOrder <= 0 && currentEnvironments.Count > 0)
        {
            entity.SortOrder = currentEnvironments.Max(item => item.SortOrder) + 1;
        }

        await _projectEnvironmentRepository.UpsertAsync(entity, cancellationToken);
        if (entity.IsActive || !currentEnvironments.Any(item => item.IsActive && item.Id != entity.Id))
        {
            await _projectEnvironmentRepository.SetActiveAsync(entity.ProjectId, entity.Id, cancellationToken);
            entity.IsActive = true;
        }

        return ResultModel<ProjectEnvironmentDto>.Success(ToEnvironmentDto(entity));
    }

    public async Task<IResultModel<ProjectEnvironmentDto>> SetActiveEnvironmentAsync(string projectId, string environmentId, CancellationToken cancellationToken)
    {
        var environment = await _projectEnvironmentRepository.GetByIdAsync(environmentId, cancellationToken);
        if (environment is null || !string.Equals(environment.ProjectId, projectId, StringComparison.OrdinalIgnoreCase))
        {
            return ResultModel<ProjectEnvironmentDto>.Failure("未找到目标环境。", "environment_not_found");
        }

        await _projectEnvironmentRepository.SetActiveAsync(projectId, environmentId, cancellationToken);
        environment.IsActive = true;
        environment.UpdatedAt = DateTime.UtcNow;
        return ResultModel<ProjectEnvironmentDto>.Success(ToEnvironmentDto(environment));
    }

    public async Task<IResultModel<bool>> DeleteEnvironmentAsync(string projectId, string environmentId, CancellationToken cancellationToken)
    {
        var environments = await _projectEnvironmentRepository.GetByProjectAsync(projectId, cancellationToken);
        var target = environments.FirstOrDefault(item => string.Equals(item.Id, environmentId, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return ResultModel<bool>.Failure("未找到待删除的环境。", "environment_not_found");
        }

        if (environments.Count <= 1)
        {
            return ResultModel<bool>.Failure("每个项目至少需要保留一个环境。", "environment_delete_last_forbidden");
        }

        await _projectEnvironmentRepository.DeleteAsync(environmentId, cancellationToken);
        if (target.IsActive)
        {
            var replacement = environments.First(item => !string.Equals(item.Id, environmentId, StringComparison.OrdinalIgnoreCase));
            await _projectEnvironmentRepository.SetActiveAsync(projectId, replacement.Id, cancellationToken);
        }

        return ResultModel<bool>.Success(true);
    }

    public async Task<IReadOnlyList<EnvironmentVariableDto>> GetVariablesAsync(string environmentId, CancellationToken cancellationToken)
    {
        var items = await _environmentVariableRepository.GetByEnvironmentAsync(environmentId, cancellationToken);
        return items.Select(ToVariableDto).ToList();
    }

    public async Task<IResultModel<EnvironmentVariableDto>> SaveVariableAsync(EnvironmentVariableDto variable, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(variable.EnvironmentId))
        {
            return ResultModel<EnvironmentVariableDto>.Failure("请先选择环境后再保存变量。", "environment_variable_environment_required");
        }

        if (string.IsNullOrWhiteSpace(variable.Key))
        {
            return ResultModel<EnvironmentVariableDto>.Failure("环境变量键不能为空。", "environment_key_required");
        }

        if (string.Equals(variable.Key, "baseUrl", StringComparison.OrdinalIgnoreCase))
        {
            return ResultModel<EnvironmentVariableDto>.Failure("BaseUrl 已作为环境独立字段维护，请不要重复保存为变量。", "environment_key_baseurl_reserved");
        }

        var environment = await _projectEnvironmentRepository.GetByIdAsync(variable.EnvironmentId, cancellationToken);
        if (environment is null)
        {
            return ResultModel<EnvironmentVariableDto>.Failure("未找到变量所属环境。", "environment_not_found");
        }

        var entity = new EnvironmentVariableEntity
        {
            Id = string.IsNullOrWhiteSpace(variable.Id) ? Guid.NewGuid().ToString("N") : variable.Id,
            EnvironmentId = variable.EnvironmentId,
            EnvironmentName = environment.Name,
            Key = variable.Key,
            Value = variable.Value,
            IsEnabled = variable.IsEnabled
        };

        await _environmentVariableRepository.UpsertAsync(entity, cancellationToken);
        return ResultModel<EnvironmentVariableDto>.Success(ToVariableDto(entity));
    }

    public async Task<IResultModel<bool>> DeleteVariableAsync(string id, CancellationToken cancellationToken)
    {
        await _environmentVariableRepository.DeleteAsync(id, cancellationToken);
        return ResultModel<bool>.Success(true);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetActiveDictionaryAsync(string environmentId, CancellationToken cancellationToken)
    {
        var variables = await GetVariablesAsync(environmentId, cancellationToken);
        return variables.Where(item => item.IsEnabled)
            .GroupBy(item => item.Key)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);
    }

    private static ProjectEnvironmentDto ToEnvironmentDto(ProjectEnvironmentEntity entity)
    {
        return new ProjectEnvironmentDto
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            Name = entity.Name,
            BaseUrl = entity.BaseUrl,
            IsActive = entity.IsActive,
            SortOrder = entity.SortOrder,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static string NormalizeBaseUrl(string? baseUrl)
    {
        var normalized = (baseUrl ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? string.Empty
            : normalized.TrimEnd('/');
    }

    private static EnvironmentVariableDto ToVariableDto(EnvironmentVariableEntity entity)
    {
        return new EnvironmentVariableDto
        {
            Id = entity.Id,
            EnvironmentId = entity.EnvironmentId,
            EnvironmentName = entity.EnvironmentName,
            Key = entity.Key,
            Value = entity.Value,
            IsEnabled = entity.IsEnabled
        };
    }
}
