using ApixPress.App.Models.Entities;

namespace ApixPress.App.Repositories.Interfaces;

public interface IProjectEnvironmentRepository
{
    Task<IReadOnlyList<ProjectEnvironmentEntity>> GetByProjectAsync(string projectId, CancellationToken cancellationToken);

    Task<ProjectEnvironmentEntity?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task<ProjectEnvironmentEntity?> GetByNameAsync(string projectId, string name, CancellationToken cancellationToken);

    Task UpsertAsync(ProjectEnvironmentEntity entity, CancellationToken cancellationToken);

    Task SetActiveAsync(string projectId, string environmentId, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);
}
