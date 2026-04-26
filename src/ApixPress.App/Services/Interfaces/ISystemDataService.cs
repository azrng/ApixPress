using Azrng.Core.Results;

namespace ApixPress.App.Services.Interfaces;

public interface ISystemDataService
{
    Task<IResultModel<bool>> ClearAllAsync(CancellationToken cancellationToken);
}
