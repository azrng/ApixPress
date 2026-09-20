using ApixPress.App.Models.DTOs;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Interfaces;

public interface IRequestExecutionService
{
    Task<IResultModel<ResponseSnapshotDto>> SendAsync(
        RequestSnapshotDto request,
        ProjectEnvironmentDto environment,
        CancellationToken cancellationToken);
}
