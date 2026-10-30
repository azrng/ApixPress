using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using ApixPress.App.Services.Implementations;
using Azrng.Core.Json;
using Microsoft.Extensions.Options;

namespace ApixPress.App.Tests.Services;

public sealed class RequestHistoryServiceTests
{
    [Fact]
    public async Task GetHistoryAsync_ShouldUseProjectedSummaryFields()
    {
        var serializer = CreateSerializer();
        var repository = new StubRequestHistoryRepository
        {
            HistoryItems =
            [
                new RequestHistoryEntity
                {
                    Id = "history-1",
                    ProjectId = "project-1",
                    Timestamp = new DateTime(2026, 4, 24, 10, 0, 0, DateTimeKind.Utc),
                    RequestSnapshotJson = "this full snapshot is not loaded for a summary row",
                    RequestName = "用户列表",
                    RequestMethod = "GET",
                    RequestUrl = "/users",
                    ResponseSnapshotJson = "{}",
                    HasResponse = true,
                    StatusCode = 204,
                    DurationMs = 33,
                    SizeBytes = 4096
                }
            ]
        };
        var service = new RequestHistoryService(repository, serializer);

        var item = Assert.Single(await service.GetHistoryAsync("project-1", CancellationToken.None));

        Assert.True(item.HasResponse);
        Assert.Equal(204, item.StatusCode);
        Assert.Equal(33, item.DurationMs);
        Assert.Equal(4096, item.SizeBytes);
        Assert.Null(item.ResponseSnapshot);
        Assert.Equal("用户列表", item.RequestSnapshot.Name);
        Assert.Equal("/users", item.RequestSnapshot.Url);
    }

    [Fact]
    public async Task ClearAllAsync_ShouldClearAllRequestHistory()
    {
        var repository = new StubRequestHistoryRepository();
        var service = new RequestHistoryService(repository, CreateSerializer());

        var result = await service.ClearAllAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(repository.ClearAllCalled);
    }

    [Fact]
    public async Task GetHistoryPageAsync_ShouldReturnFiftyItemsAndAdvanceWithTimestampAndIdCursor()
    {
        var timestamp = new DateTime(2026, 4, 24, 10, 0, 0, DateTimeKind.Utc);
        var repository = new StubRequestHistoryRepository
        {
            HistoryItems = Enumerable.Range(0, 52)
                .Select(index => new RequestHistoryEntity
                {
                    Id = $"history-{index:D3}",
                    ProjectId = "project-1",
                    Timestamp = timestamp.AddSeconds(-index),
                    RequestSnapshotJson = "{}",
                    RequestName = $"请求 {index}"
                })
                .ToList()
        };
        var service = new RequestHistoryService(repository, CreateSerializer());

        var firstPage = await service.GetHistoryPageAsync("project-1", null, null, CancellationToken.None);
        var secondPage = await service.GetHistoryPageAsync(
            "project-1",
            firstPage.Items[^1].Timestamp,
            firstPage.Items[^1].Id,
            CancellationToken.None);

        Assert.Equal(50, firstPage.Items.Count);
        Assert.True(firstPage.HasMore);
        Assert.Equal("history-000", firstPage.Items[0].Id);
        Assert.Equal(["history-050", "history-051"], secondPage.Items.Select(item => item.Id));
        Assert.False(secondPage.HasMore);
    }

    private static SysTextJsonSerializer CreateSerializer()
    {
        return new SysTextJsonSerializer(Options.Create(new DefaultJsonSerializerOptions()));
    }

    private sealed class StubRequestHistoryRepository : IRequestHistoryRepository
    {
        public IReadOnlyList<RequestHistoryEntity> HistoryItems { get; init; } = [];

        public bool ClearAllCalled { get; private set; }
        public int LastHistoryPageLimit { get; private set; }

        public Task<IReadOnlyList<RequestHistoryEntity>> GetAllAsync(string projectId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<RequestHistoryEntity>>([]);

        public Task<IReadOnlyList<RequestHistoryEntity>> GetHistoryAsync(string projectId, int limit, CancellationToken cancellationToken)
        {
            return Task.FromResult(HistoryItems);
        }

        public Task<IReadOnlyList<RequestHistoryEntity>> GetHistoryPageAsync(
            string projectId,
            DateTime? beforeTimestamp,
            string? beforeId,
            int limit,
            CancellationToken cancellationToken)
        {
            LastHistoryPageLimit = limit;
            var query = HistoryItems.AsEnumerable();
            if (beforeTimestamp is not null)
            {
                query = query.Where(item => item.Timestamp < beforeTimestamp.Value
                    || (item.Timestamp == beforeTimestamp.Value
                        && string.CompareOrdinal(item.Id, beforeId) < 0));
            }

            return Task.FromResult<IReadOnlyList<RequestHistoryEntity>>(query
                .OrderByDescending(item => item.Timestamp)
                .ThenByDescending(item => item.Id, StringComparer.Ordinal)
                .Take(limit)
                .ToList());
        }

        public Task<RequestHistoryEntity?> GetByIdAsync(string projectId, string id, CancellationToken cancellationToken)
        {
            return Task.FromResult<RequestHistoryEntity?>(HistoryItems.FirstOrDefault(item => item.Id == id));
        }

        public Task UpsertAsync(RequestHistoryEntity entity, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(string projectId, string id, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task ClearAsync(string projectId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task ClearAllAsync(CancellationToken cancellationToken)
        {
            ClearAllCalled = true;
            return Task.CompletedTask;
        }

        public Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
