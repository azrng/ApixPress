using ApixPress.App.Models.DTOs;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Interfaces;

public interface IApplicationUpdateService
{
    string ChannelName { get; }

    bool IsConfigured { get; }

    Task<IResultModel<AppUpdateCheckResultDto>> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken);

    /// <summary>在当前主进程内下载更新包并校验哈希、解压到临时工作区；进度通过 progress 上报给界面，可随时取消。</summary>
    Task<IResultModel<AppUpdateStagedPackageDto>> PrepareUpdatePackageAsync(
        AppUpdateCheckResultDto updateInfo,
        IProgress<AppUpdateProgressDto>? progress,
        CancellationToken cancellationToken);

    /// <summary>基于已就绪的更新包生成替换脚本并隐藏启动：等待当前进程退出后覆盖安装目录并自动重启主程序。</summary>
    Task<IResultModel<bool>> ApplyStagedUpdateAsync(AppUpdateStagedPackageDto stagedPackage, CancellationToken cancellationToken);
}
