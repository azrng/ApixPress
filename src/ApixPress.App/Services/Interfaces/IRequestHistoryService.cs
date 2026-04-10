using ApixPress.App.Models.DTOs;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Interfaces;

public interface IRequestHistoryService
{
    Task<IReadOnlyList<RequestHistoryItemDto>> GetHistoryAsync(string projectId, CancellationToken cancellationToken);

    Task<IResultModel<RequestHistoryItemDto>> AddAsync(string projectId, RequestSnapshotDto request, ResponseSnapshotDto? response, CancellationToken cancellationToken);

    Task<IResultModel<bool>> ClearAsync(string projectId, CancellationToken cancellationToken);

    Task<IResultModel<bool>> DeleteAsync(string projectId, string id, CancellationToken cancellationToken);
}

public sealed class RequestHistoryItemDto
{
    public string Id { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public RequestSnapshotDto RequestSnapshot { get; init; } = new();
    public ResponseSnapshotDto? ResponseSnapshot { get; init; }
}
