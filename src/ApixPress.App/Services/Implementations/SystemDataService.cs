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

    public async Task<IResultModel<bool>> ClearProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return ResultModel<bool>.Failure("项目 ID 不能为空。", "project_id_required");
        }

        var cleared = await _systemDataRepository.ClearProjectAsync(projectId, cancellationToken);
        return cleared
            ? ResultModel<bool>.Success(true)
            : ResultModel<bool>.Failure("未找到待清空的项目。", "project_not_found");
    }

    public async Task<IResultModel<bool>> ClearAllAsync(CancellationToken cancellationToken)
    {
        await _systemDataRepository.ClearAllAsync(cancellationToken);
        return ResultModel<bool>.Success(true);
    }
}
