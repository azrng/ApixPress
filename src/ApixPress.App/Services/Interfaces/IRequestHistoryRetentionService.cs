namespace ApixPress.App.Services.Interfaces;

/// <summary>
/// 请求历史保留策略：定期删除超过保留期的历史记录，防止 request_history 表无限膨胀。
/// </summary>
public interface IRequestHistoryRetentionService
{
    /// <summary>启动保留策略：立即清理一次，并按固定周期重复（应用生命周期内有效）。</summary>
    void Start();

    /// <summary>停止周期清理（窗口关闭时调用，进程退出兜底）。</summary>
    void Stop();

    /// <summary>执行一次清理（定时器与启动首清共用，可单独测试）。防重入：已有清理在跑时直接返回。</summary>
    Task RunCleanupAsync(CancellationToken cancellationToken);
}
