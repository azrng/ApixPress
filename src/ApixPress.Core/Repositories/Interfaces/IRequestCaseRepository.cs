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

    /// <summary>在单一事务内完成删除 + 批量 Upsert，保证导入同步的原子性。</summary>
    Task SyncImportedRangeAsync(
        string projectId,
        IReadOnlyList<string> deletedIds,
        IReadOnlyList<RequestCaseEntity> upserts,
        CancellationToken cancellationToken);
}
