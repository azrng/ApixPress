using ApixPress.App.Models.DTOs;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Interfaces;

public interface IRequestHistoryService
{
    Task<IReadOnlyList<RequestHistoryItemDto>> GetHistoryAsync(string projectId, CancellationToken cancellationToken);

    Task<RequestHistoryPageDto> GetHistoryPageAsync(
        string projectId,
        DateTime? beforeTimestamp,
        string? beforeId,
        CancellationToken cancellationToken);

    /// <summary>读取用于备份的完整历史记录。</summary>
    Task<IReadOnlyList<RequestHistoryItemDto>> GetAllHistoryAsync(string projectId, CancellationToken cancellationToken);

    /// <summary>将备份中的历史记录导入目标项目。</summary>
    Task<RequestHistoryItemDto?> GetDetailAsync(string projectId, string id, CancellationToken cancellationToken);

    Task<IResultModel<RequestHistoryItemDto>> AddAsync(string projectId, RequestSnapshotDto request, ResponseSnapshotDto? response, CancellationToken cancellationToken);

    Task<IResultModel<bool>> ClearAsync(string projectId, CancellationToken cancellationToken);

    Task<IResultModel<bool>> ClearAllAsync(CancellationToken cancellationToken);

    Task<IResultModel<bool>> DeleteAsync(string projectId, string id, CancellationToken cancellationToken);

    /// <summary>按保留期清理过期历史记录，返回删除条数（定时保留策略用）。</summary>
    Task<int> CleanupOlderThanAsync(TimeSpan retention, CancellationToken cancellationToken);
}
