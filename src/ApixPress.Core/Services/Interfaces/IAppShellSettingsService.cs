using ApixPress.App.Models.DTOs;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Interfaces;

public interface IAppShellSettingsService
{
    Task<IResultModel<AppShellSettingsDto>> LoadAsync(CancellationToken cancellationToken);

    Task<IResultModel<AppShellSettingsDto>> SaveAsync(AppShellSettingsDto settings, CancellationToken cancellationToken);
}
