using ApixPress.App.Models.DTOs;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Interfaces;

public interface IProjectHttpSettingsService
{
    Task<ProjectHttpAuthSettingsDto> GetAuthSettingsAsync(string projectId, CancellationToken cancellationToken);

    Task<IResultModel<ProjectHttpAuthSettingsDto>> SaveAuthSettingsAsync(ProjectHttpAuthSettingsDto settings, CancellationToken cancellationToken);
}
