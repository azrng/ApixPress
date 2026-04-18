using ApixPress.App.Models.Entities;

namespace ApixPress.App.Repositories.Interfaces;

public interface IRequestCaseRepository
{
    Task<IReadOnlyList<RequestCaseEntity>> GetCasesAsync(string projectId, CancellationToken cancellationToken);

    Task<RequestCaseEntity?> GetByIdAsync(string projectId, string id, CancellationToken cancellationToken);

    Task UpsertAsync(RequestCaseEntity entity, CancellationToken cancellationToken);

    Task UpsertRangeAsync(IReadOnlyList<RequestCaseEntity> entities, CancellationToken cancellationToken);

    Task DeleteAsync(string projectId, string id, CancellationToken cancellationToken);

    Task DeleteRangeAsync(string projectId, IEnumerable<string> ids, CancellationToken cancellationToken);
}
