using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.DependencyInjection;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Implementations;

public sealed class ProjectHttpSettingsService : IProjectHttpSettingsService, ITransientDependency
{
    private readonly IProjectHttpSettingsRepository _repository;

    public ProjectHttpSettingsService(IProjectHttpSettingsRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProjectHttpAuthSettingsDto> GetAuthSettingsAsync(string projectId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return new ProjectHttpAuthSettingsDto();
        }

        var entity = await _repository.GetAuthSettingsAsync(projectId, cancellationToken);
        return entity is null
            ? new ProjectHttpAuthSettingsDto { ProjectId = projectId }
            : ToDto(entity);
    }

    public async Task<IResultModel<ProjectHttpAuthSettingsDto>> SaveAuthSettingsAsync(ProjectHttpAuthSettingsDto settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ProjectId))
        {
            return ResultModel<ProjectHttpAuthSettingsDto>.Failure("请先选择项目后再保存接口鉴权。", "project_required");
        }

        var normalized = Normalize(settings);
        if (string.Equals(normalized.AuthMode, ProjectHttpAuthSettingsDto.ModeBearer, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(normalized.BearerToken))
        {
            return ResultModel<ProjectHttpAuthSettingsDto>.Failure("请输入 Bearer Token。", "bearer_token_required");
        }

        await _repository.UpsertAuthSettingsAsync(ToEntity(normalized), cancellationToken);
        return ResultModel<ProjectHttpAuthSettingsDto>.Success(normalized);
    }

    private static ProjectHttpAuthSettingsDto Normalize(ProjectHttpAuthSettingsDto settings)
    {
        var authMode = string.Equals(settings.AuthMode, ProjectHttpAuthSettingsDto.ModeBearer, StringComparison.OrdinalIgnoreCase)
            ? ProjectHttpAuthSettingsDto.ModeBearer
            : ProjectHttpAuthSettingsDto.ModeNone;

        return new ProjectHttpAuthSettingsDto
        {
            ProjectId = settings.ProjectId.Trim(),
            AuthMode = authMode,
            BearerToken = authMode == ProjectHttpAuthSettingsDto.ModeBearer ? settings.BearerToken.Trim() : string.Empty,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static ProjectHttpAuthSettingsDto ToDto(ProjectHttpAuthSettingsEntity entity)
    {
        return new ProjectHttpAuthSettingsDto
        {
            ProjectId = entity.ProjectId,
            AuthMode = string.IsNullOrWhiteSpace(entity.AuthMode) ? ProjectHttpAuthSettingsDto.ModeNone : entity.AuthMode,
            BearerToken = entity.BearerToken,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static ProjectHttpAuthSettingsEntity ToEntity(ProjectHttpAuthSettingsDto dto)
    {
        return new ProjectHttpAuthSettingsEntity
        {
            ProjectId = dto.ProjectId,
            AuthMode = dto.AuthMode,
            BearerToken = dto.BearerToken,
            UpdatedAt = dto.UpdatedAt
        };
    }
}
