using ApixPress.App.Models.Entities;

namespace ApixPress.App.Repositories.Interfaces;

public interface IRequestHistoryRepository
{
    Task<IReadOnlyList<RequestHistoryEntity>> GetHistoryAsync(string projectId, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<RequestHistoryEntity>> GetHistoryPageAsync(
        string projectId,
        DateTime? beforeTimestamp,
        string? beforeId,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>读取项目完整历史快照，用于备份。</summary>
    Task<IReadOnlyList<RequestHistoryEntity>> GetAllAsync(string projectId, CancellationToken cancellationToken);

    Task<RequestHistoryEntity?> GetByIdAsync(string projectId, string id, CancellationToken cancellationToken);

    Task UpsertAsync(RequestHistoryEntity entity, CancellationToken cancellationToken);

    Task DeleteAsync(string projectId, string id, CancellationToken cancellationToken);

    Task ClearAsync(string projectId, CancellationToken cancellationToken);

    Task ClearAllAsync(CancellationToken cancellationToken);

    /// <summary>删除时间戳早于截止时刻的历史记录，返回删除条数（保留策略用）。</summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken);
}
