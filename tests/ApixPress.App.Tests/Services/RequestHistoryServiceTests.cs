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
                    RequestSnapshotJson = serializer.ToJson(new RequestSnapshotDto
                    {
                        Method = "GET",
                        Url = "/users"
                    }),
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
        Assert.Equal("/users", item.RequestSnapshot.Url);
    }

    private static SysTextJsonSerializer CreateSerializer()
    {
        return new SysTextJsonSerializer(Options.Create(new DefaultJsonSerializerOptions()));
    }

    private sealed class StubRequestHistoryRepository : IRequestHistoryRepository
    {
        public IReadOnlyList<RequestHistoryEntity> HistoryItems { get; init; } = [];

        public Task<IReadOnlyList<RequestHistoryEntity>> GetHistoryAsync(string projectId, int limit, CancellationToken cancellationToken)
        {
            return Task.FromResult(HistoryItems);
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
    }
}
