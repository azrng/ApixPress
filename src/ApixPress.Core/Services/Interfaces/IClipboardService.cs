namespace ApixPress.App.Services.Interfaces;

/// <summary>
/// 剪贴板写入服务：隔离 ViewModel 对 TopLevel 的直接依赖
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// 复制文本到系统剪贴板；主窗口不可用时返回 false
    /// </summary>
    Task<bool> CopyTextAsync(string text, CancellationToken cancellationToken = default);
}
