using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using ApixPress.App.Services.Implementations;
using ApixPress.App.Tests.ViewModels;
using Azrng.Core.Json;
using Microsoft.Extensions.Options;

namespace ApixPress.App.Tests.Services;

public sealed class RequestHistoryRetentionTests
{
    [Fact]
    public async Task CleanupOlderThanAsync_ShouldPassCutoffMinusRetentionToRepository()
    {
        var repository = new FakeHistoryRepository { DeletedCount = 3 };
        var service = CreateService(repository);
        var before = DateTime.UtcNow;

        var deleted = await service.CleanupOlderThanAsync(RequestHistoryRetentionService.RetentionPeriod, CancellationToken.None);

        Assert.Equal(3, deleted);
        var cutoff = Assert.Single(repository.DeleteCutoffs);
        // 保留期 7 天：截止时刻 = 当前 UTC - 7 天（允许测试执行的分钟级误差）
        var expected = before - RequestHistoryRetentionService.RetentionPeriod;
        Assert.True(Math.Abs((cutoff - expected).TotalMinutes) < 5, $"cutoff={cutoff}, expected≈{expected}");
    }

    [Fact]
    public async Task RunCleanupAsync_ShouldUseSevenDayRetention()
    {
        var historyService = new RecordingHistoryService();
        var retentionService = new RequestHistoryRetentionService(historyService);

        await retentionService.RunCleanupAsync(CancellationToken.None);

        var retention = Assert.Single(historyService.CleanupRetentions);
        Assert.Equal(TimeSpan.FromDays(7), retention);
    }

    [Fact]
    public async Task Start_ShouldRunImmediateCleanup_AndAllowRestartAfterStop()
    {
        var historyService = new RecordingHistoryService();
        var retentionService = new RequestHistoryRetentionService(historyService);

        retentionService.Start();
        var immediateRan = await SpinUntilAsync(() => historyService.CleanupCallCount >= 1);
        retentionService.Stop();

        Assert.True(immediateRan, "Start 后应立即执行一次清理");
        var callsAfterStop = historyService.CleanupCallCount;

        // Stop 后再 Start：服务可重新启动（窗口关闭→重开场景由进程重启覆盖，这里只验证幂等不重复挂定时器）
        retentionService.Start();
        Assert.True(await SpinUntilAsync(() => historyService.CleanupCallCount >= callsAfterStop + 1));
        retentionService.Stop();
    }

    [Fact]
    public async Task RunCleanupAsync_WhenServiceThrows_ShouldSwallowAndNotPropagate()
    {
        var historyService = new RecordingHistoryService { ThrowOnCleanup = true };
        var retentionService = new RequestHistoryRetentionService(historyService);

        // 后台维护任务的异常不允许外抛（调用方是 fire-and-forget 的定时器回调）
        await retentionService.RunCleanupAsync(CancellationToken.None);
    }

    private static RequestHistoryService CreateService(IRequestHistoryRepository repository)
    {
        var serializer = new SysTextJsonSerializer(Options.Create(new DefaultJsonSerializerOptions()));
        return new RequestHistoryService(repository, serializer);
    }

    private static async Task<bool> SpinUntilAsync(Func<bool> condition, int timeoutMilliseconds = 3000)
    {
        var deadline = Environment.TickCount64 + timeoutMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(20);
        }

        return condition();
    }

    private sealed class RecordingHistoryService : ViewModelSharedTestDoubles.FakeRequestHistoryService
    {
        public int CleanupCallCount { get; private set; }

        public bool ThrowOnCleanup { get; set; }

        public override Task<int> CleanupOlderThanAsync(TimeSpan retention, CancellationToken cancellationToken)
        {
            CleanupCallCount++;
            if (ThrowOnCleanup)
            {
                throw new InvalidOperationException("清理失败（测试注入）");
            }

            return base.CleanupOlderThanAsync(retention, cancellationToken);
        }
    }

    private sealed class FakeHistoryRepository : IRequestHistoryRepository
    {
        public List<DateTime> DeleteCutoffs { get; } = [];

        public int DeletedCount { get; set; }

        public Task<IReadOnlyList<RequestHistoryEntity>> GetHistoryAsync(string projectId, int limit, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<RequestHistoryEntity>>([]);
        }

        public Task<RequestHistoryEntity?> GetByIdAsync(string projectId, string id, CancellationToken cancellationToken)
        {
            return Task.FromResult<RequestHistoryEntity?>(null);
        }

        public Task UpsertAsync(RequestHistoryEntity entity, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string projectId, string id, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task ClearAsync(string projectId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task ClearAllAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken)
        {
            DeleteCutoffs.Add(cutoff);
            return Task.FromResult(DeletedCount);
        }
    }
}
