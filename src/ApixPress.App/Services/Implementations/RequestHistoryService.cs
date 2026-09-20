using ApixPress.App.Helpers;
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

    public Task<IReadOnlyList<RequestHistoryItemDto>> GetHistoryAsync(string projectId, CancellationToken cancellationToken)
    {
        return PerfLog.MeasureAsync<IReadOnlyList<RequestHistoryItemDto>>("历史·列表加载", async () =>
        {
            const int limit = 50;
            var entities = await _requestHistoryRepository.GetHistoryAsync(projectId, limit, cancellationToken);
            // 50 行完整请求快照 JSON 的逐行反序列化是 CPU 密集操作：不能落回 UI 线程 continuation
            return await Task.Run(() => entities.Select(ToSummaryDto).ToList(), cancellationToken);
        });
    }

    public Task<RequestHistoryItemDto?> GetDetailAsync(string projectId, string id, CancellationToken cancellationToken)
    {
        return PerfLog.MeasureAsync("历史·详情加载", async () =>
        {
            var entity = await _requestHistoryRepository.GetByIdAsync(projectId, id, cancellationToken);
            return entity is null
                ? null
                : await Task.Run(() => ToDetailDto(entity), cancellationToken);
        });
    }

    public Task<IResultModel<RequestHistoryItemDto>> AddAsync(string projectId, RequestSnapshotDto request, ResponseSnapshotDto? response, CancellationToken cancellationToken)
    {
        return PerfLog.MeasureAsync("历史·写入", async () =>
        {
            // 快照序列化可能包含 MB 级响应正文，移出 UI continuation，避免发送完成后卡住历史面板。
            var serializedSnapshots = await Task.Run(() =>
            (
                Request: _serializer.ToJson(request),
                Response: response is not null ? _serializer.ToJson(response) : "{}"
            ), cancellationToken);
            var entity = new RequestHistoryEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                ProjectId = projectId,
                Timestamp = DateTime.UtcNow,
                RequestSnapshotJson = serializedSnapshots.Request,
                ResponseSnapshotJson = serializedSnapshots.Response
            };

            await _requestHistoryRepository.UpsertAsync(entity, cancellationToken);
            return ResultModel<RequestHistoryItemDto>.Success(CreateDetailDto(entity.Id, entity.Timestamp, request, response));
        });
    }

    public async Task<IResultModel<bool>> ClearAsync(string projectId, CancellationToken cancellationToken)
    {
        await _requestHistoryRepository.ClearAsync(projectId, cancellationToken);
        return ResultModel<bool>.Success(true);
    }

    public async Task<IResultModel<bool>> ClearAllAsync(CancellationToken cancellationToken)
    {
        await _requestHistoryRepository.ClearAllAsync(cancellationToken);
        return ResultModel<bool>.Success(true);
    }

    public async Task<IResultModel<bool>> DeleteAsync(string projectId, string id, CancellationToken cancellationToken)
    {
        await _requestHistoryRepository.DeleteAsync(projectId, id, cancellationToken);
        return ResultModel<bool>.Success(true);
    }

    public Task<int> CleanupOlderThanAsync(TimeSpan retention, CancellationToken cancellationToken)
    {
        return PerfLog.MeasureAsync<int>("历史·定时清理", async () =>
        {
            var cutoff = DateTime.UtcNow - retention;
            return await _requestHistoryRepository.DeleteOlderThanAsync(cutoff, cancellationToken);
        });
    }

    private RequestHistoryItemDto ToSummaryDto(RequestHistoryEntity entity)
    {
        var requestSnapshot = BuildSummarySnapshot(entity);
        return new RequestHistoryItemDto
        {
            Id = entity.Id,
            Timestamp = entity.Timestamp,
            HasResponse = entity.HasResponse,
            StatusCode = entity.StatusCode,
            DurationMs = entity.DurationMs,
            SizeBytes = entity.SizeBytes,
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

    private RequestSnapshotDto BuildSummarySnapshot(RequestHistoryEntity entity)
    {
        if (!string.IsNullOrWhiteSpace(entity.RequestMethod)
            || !string.IsNullOrWhiteSpace(entity.RequestUrl)
            || !string.IsNullOrWhiteSpace(entity.RequestName))
        {
            return new RequestSnapshotDto
            {
                Name = entity.RequestName,
                Method = entity.RequestMethod,
                Url = entity.RequestUrl
            };
        }

        // 兼容测试替身和旧仓储实现：摘要字段为空时才回退到完整快照。
        return _serializer.ToObject<RequestSnapshotDto>(entity.RequestSnapshotJson) ?? new RequestSnapshotDto();
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
}
