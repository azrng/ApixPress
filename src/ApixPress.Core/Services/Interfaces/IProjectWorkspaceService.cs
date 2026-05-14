using ApixPress.App.Models.DTOs;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Interfaces;

public interface IProjectWorkspaceService
{
    Task<IReadOnlyList<ProjectWorkspaceDto>> GetProjectsAsync(CancellationToken cancellationToken);

    Task<ProjectWorkspaceDto?> GetStartupProjectAsync(CancellationToken cancellationToken);

    Task<IResultModel<ProjectWorkspaceDto>> SaveAsync(ProjectWorkspaceDto project, CancellationToken cancellationToken);

    Task<IResultModel<ProjectWorkspaceDto>> SetDefaultAsync(string projectId, CancellationToken cancellationToken);

    Task<IResultModel<bool>> DeleteAsync(string projectId, CancellationToken cancellationToken);
}
