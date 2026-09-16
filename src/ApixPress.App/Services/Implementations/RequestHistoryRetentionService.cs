using ApixPress.App.Helpers;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.DependencyInjection;

namespace ApixPress.App.Services.Implementations;

/// <summary>
/// 请求历史保留策略服务：启动即清理一次，之后每 24 小时重复；
/// 单条历史快照可达 MB 级，无保留策略时 SQLite 文件与 WAL 会随使用无限膨胀。
/// 清理在线程池执行，失败仅落 CrashLog，不打扰用户。
/// </summary>
public sealed class RequestHistoryRetentionService : IRequestHistoryRetentionService, ISingletonDependency
{
    /// <summary>历史保留期：删除 7 天前的记录（用户确认的策略，2026-09-16）。</summary>
    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(7);

    /// <summary>清理周期：与保留期同量级即可，无需更频繁。</summary>
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);

    private readonly IRequestHistoryService _requestHistoryService;
    private System.Threading.Timer? _timer;
    private int _isRunning;

    public RequestHistoryRetentionService(IRequestHistoryService requestHistoryService)
    {
        _requestHistoryService = requestHistoryService;
    }

    public void Start()
    {
        if (_timer is not null)
        {
            return;
        }

        // 启动即清一次（DB 初始化完成后调用），再挂 24h 周期；进程退出时线程池定时器随之消亡
        _ = RunCleanupAsync(CancellationToken.None);
        _timer = new System.Threading.Timer(
            _ => _ = RunCleanupAsync(CancellationToken.None),
            null,
            CleanupInterval,
            CleanupInterval);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        // 定时器回调与启动首清可能重叠：防重入，避免并发 delete 互相等待锁
        if (Interlocked.Exchange(ref _isRunning, 1) != 0)
        {
            return;
        }

        try
        {
            await _requestHistoryService.CleanupOlderThanAsync(RetentionPeriod, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            // 后台维护任务失败不弹窗打扰用户，落崩溃日志便于排查
            CrashLog.Write(exception, "HistoryRetention");
        }
        finally
        {
            Interlocked.Exchange(ref _isRunning, 0);
        }
    }
}
