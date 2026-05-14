using ApixPress.App.Models.DTOs;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Interfaces;

public interface IApplicationUpdateService
{
    string ChannelName { get; }

    bool IsConfigured { get; }

    Task<IResultModel<AppUpdateCheckResultDto>> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken);

    Task<IResultModel<bool>> StartUpdateAsync(AppUpdateCheckResultDto updateInfo, CancellationToken cancellationToken);
}
