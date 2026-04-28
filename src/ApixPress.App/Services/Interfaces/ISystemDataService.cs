using Azrng.Core.Results;

namespace ApixPress.App.Services.Interfaces;

public interface ISystemDataService
{
    Task<IResultModel<bool>> ClearProjectAsync(string projectId, CancellationToken cancellationToken);

    Task<IResultModel<bool>> ClearAllAsync(CancellationToken cancellationToken);
}
