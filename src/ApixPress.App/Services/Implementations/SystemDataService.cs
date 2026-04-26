using ApixPress.App.Repositories.Interfaces;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.DependencyInjection;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Implementations;

public sealed class SystemDataService : ISystemDataService, ITransientDependency
{
    private readonly ISystemDataRepository _systemDataRepository;

    public SystemDataService(ISystemDataRepository systemDataRepository)
    {
        _systemDataRepository = systemDataRepository;
    }

    public async Task<IResultModel<bool>> ClearAllAsync(CancellationToken cancellationToken)
    {
        await _systemDataRepository.ClearAllAsync(cancellationToken);
        return ResultModel<bool>.Success(true);
    }
}
