using ApixPress.App.Models.Entities;

namespace ApixPress.App.Repositories.Interfaces;

public interface IRequestHistoryRepository
{
    Task<IReadOnlyList<RequestHistoryEntity>> GetHistoryAsync(string projectId, int limit, CancellationToken cancellationToken);

    Task UpsertAsync(RequestHistoryEntity entity, CancellationToken cancellationToken);

    Task DeleteAsync(string projectId, string id, CancellationToken cancellationToken);

    Task ClearAsync(string projectId, CancellationToken cancellationToken);
}
