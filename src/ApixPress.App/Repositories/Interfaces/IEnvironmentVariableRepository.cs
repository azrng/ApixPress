using ApixPress.App.Models.Entities;

namespace ApixPress.App.Repositories.Interfaces;

public interface IEnvironmentVariableRepository
{
    Task<IReadOnlyList<EnvironmentVariableEntity>> GetByEnvironmentAsync(string environmentId, CancellationToken cancellationToken);

    Task UpsertAsync(EnvironmentVariableEntity entity, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);
}
