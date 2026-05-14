using Azrng.Core.Results;

namespace ApixPress.App.Services.Interfaces;

public interface IApplicationRestartService
{
    Task<IResultModel<bool>> RestartAsync(CancellationToken cancellationToken);
}
