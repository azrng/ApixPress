using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using ApixPress.App.Services.Interfaces;
using Azrng.Core;
using Azrng.Core.DependencyInjection;
using Azrng.Core.Json;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Implementations;

public sealed class RequestHistoryService : IRequestHistoryService, ITransientDependency
{
    private readonly IRequestHistoryRepository _requestHistoryRepository;
    private readonly IJsonSerializer _serializer;

    public RequestHistoryService(IRequestHistoryRepository requestHistoryRepository, IJsonSerializer serializer)
    {
        _requestHistoryRepository = requestHistoryRepository;
        _serializer = serializer;
    }

    public async Task<IReadOnlyList<RequestHistoryItemDto>> GetHistoryAsync(CancellationToken cancellationToken)
    {
        const int limit = 50;
        var entities = await _requestHistoryRepository.GetHistoryAsync(limit, cancellationToken);
        return entities.Select(ToDto).ToList();
    }

    public async Task<IResultModel<RequestHistoryItemDto>> AddAsync(RequestSnapshotDto request, ResponseSnapshotDto? response, CancellationToken cancellationToken)
    {
        var entity = new RequestHistoryEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Timestamp = DateTime.UtcNow,
            RequestSnapshotJson = _serializer.ToJson(request),
            ResponseSnapshotJson = response is not null ? _serializer.ToJson(response) : "{}"
        };

        await _requestHistoryRepository.UpsertAsync(entity, cancellationToken);
        return ResultModel<RequestHistoryItemDto>.Success(ToDto(entity));
    }

    public async Task<IResultModel<bool>> ClearAsync(CancellationToken cancellationToken)
    {
        await _requestHistoryRepository.ClearAsync(cancellationToken);
        return ResultModel<bool>.Success(true);
    }

    public async Task<IResultModel<bool>> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await _requestHistoryRepository.DeleteAsync(id, cancellationToken);
        return ResultModel<bool>.Success(true);
    }

    private RequestHistoryItemDto ToDto(RequestHistoryEntity entity)
    {
        return new RequestHistoryItemDto
        {
            Id = entity.Id,
            Timestamp = entity.Timestamp,
            RequestSnapshot = _serializer.ToObject<RequestSnapshotDto>(entity.RequestSnapshotJson) ?? new RequestSnapshotDto(),
            ResponseSnapshot = _serializer.ToObject<ResponseSnapshotDto>(entity.ResponseSnapshotJson)
        };
    }
}
