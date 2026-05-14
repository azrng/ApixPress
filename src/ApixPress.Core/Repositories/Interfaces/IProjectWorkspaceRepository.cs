using ApixPress.App.Models.Entities;

namespace ApixPress.App.Repositories.Interfaces;

public interface IProjectWorkspaceRepository
{
    Task<IReadOnlyList<ProjectWorkspaceEntity>> GetProjectsAsync(CancellationToken cancellationToken);

    Task<ProjectWorkspaceEntity?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task<ProjectWorkspaceEntity?> GetByNameAsync(string name, CancellationToken cancellationToken);

    Task UpsertAsync(ProjectWorkspaceEntity entity, CancellationToken cancellationToken);

    Task SetDefaultAsync(string id, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);
}
