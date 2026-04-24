using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using ApixPress.App.Services.Interfaces;
using Azrng.Core;
using Azrng.Core.DependencyInjection;
using Azrng.Core.Json;
using Azrng.Core.Results;
using System.Text.Json;

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

    public async Task<IReadOnlyList<RequestHistoryItemDto>> GetHistoryAsync(string projectId, CancellationToken cancellationToken)
    {
        const int limit = 50;
        var entities = await _requestHistoryRepository.GetHistoryAsync(projectId, limit, cancellationToken);
        return entities.Select(ToSummaryDto).ToList();
    }

    public async Task<RequestHistoryItemDto?> GetDetailAsync(string projectId, string id, CancellationToken cancellationToken)
    {
        var entity = await _requestHistoryRepository.GetByIdAsync(projectId, id, cancellationToken);
        return entity is null ? null : ToDetailDto(entity);
    }

    public async Task<IResultModel<RequestHistoryItemDto>> AddAsync(string projectId, RequestSnapshotDto request, ResponseSnapshotDto? response, CancellationToken cancellationToken)
    {
        var entity = new RequestHistoryEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            ProjectId = projectId,
            Timestamp = DateTime.UtcNow,
            RequestSnapshotJson = _serializer.ToJson(request),
            ResponseSnapshotJson = response is not null ? _serializer.ToJson(response) : "{}"
        };

        await _requestHistoryRepository.UpsertAsync(entity, cancellationToken);
        return ResultModel<RequestHistoryItemDto>.Success(CreateDetailDto(entity.Id, entity.Timestamp, request, response));
    }

    public async Task<IResultModel<bool>> ClearAsync(string projectId, CancellationToken cancellationToken)
    {
        await _requestHistoryRepository.ClearAsync(projectId, cancellationToken);
        return ResultModel<bool>.Success(true);
    }

    public async Task<IResultModel<bool>> DeleteAsync(string projectId, string id, CancellationToken cancellationToken)
    {
        await _requestHistoryRepository.DeleteAsync(projectId, id, cancellationToken);
        return ResultModel<bool>.Success(true);
    }

    private RequestHistoryItemDto ToSummaryDto(RequestHistoryEntity entity)
    {
        var requestSnapshot = _serializer.ToObject<RequestSnapshotDto>(entity.RequestSnapshotJson) ?? new RequestSnapshotDto();
        var responseSummary = ParseResponseSummary(entity.ResponseSnapshotJson);
        return new RequestHistoryItemDto
        {
            Id = entity.Id,
            Timestamp = entity.Timestamp,
            HasResponse = responseSummary.HasResponse,
            StatusCode = responseSummary.StatusCode,
            DurationMs = responseSummary.DurationMs,
            SizeBytes = responseSummary.SizeBytes,
            RequestSnapshot = requestSnapshot,
            ResponseSnapshot = null
        };
    }

    private RequestHistoryItemDto ToDetailDto(RequestHistoryEntity entity)
    {
        var requestSnapshot = _serializer.ToObject<RequestSnapshotDto>(entity.RequestSnapshotJson) ?? new RequestSnapshotDto();
        var responseSnapshot = _serializer.ToObject<ResponseSnapshotDto>(entity.ResponseSnapshotJson);
        return CreateDetailDto(entity.Id, entity.Timestamp, requestSnapshot, responseSnapshot);
    }

    private static RequestHistoryItemDto CreateDetailDto(
        string id,
        DateTime timestamp,
        RequestSnapshotDto requestSnapshot,
        ResponseSnapshotDto? responseSnapshot)
    {
        return new RequestHistoryItemDto
        {
            Id = id,
            Timestamp = timestamp,
            HasResponse = responseSnapshot is not null,
            StatusCode = responseSnapshot?.StatusCode,
            DurationMs = responseSnapshot?.DurationMs ?? 0,
            SizeBytes = responseSnapshot?.SizeBytes ?? 0,
            RequestSnapshot = requestSnapshot,
            ResponseSnapshot = responseSnapshot
        };
    }

    private static (bool HasResponse, int? StatusCode, long DurationMs, long SizeBytes) ParseResponseSummary(string responseSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(responseSnapshotJson) || string.Equals(responseSnapshotJson, "{}", StringComparison.Ordinal))
        {
            return (false, null, 0, 0);
        }

        try
        {
            using var document = JsonDocument.Parse(responseSnapshotJson);
            var root = document.RootElement;
            var hasMeaningfulResponse = root.ValueKind == JsonValueKind.Object && root.EnumerateObject().Any();
            if (!hasMeaningfulResponse)
            {
                return (false, null, 0, 0);
            }

            return (
                true,
                root.TryGetProperty(nameof(ResponseSnapshotDto.StatusCode), out var statusCodeElement) && statusCodeElement.ValueKind != JsonValueKind.Null
                    ? statusCodeElement.GetInt32()
                    : null,
                root.TryGetProperty(nameof(ResponseSnapshotDto.DurationMs), out var durationElement) ? durationElement.GetInt64() : 0,
                root.TryGetProperty(nameof(ResponseSnapshotDto.SizeBytes), out var sizeElement) ? sizeElement.GetInt64() : 0);
        }
        catch (JsonException)
        {
            return (true, null, 0, 0);
        }
    }
}
