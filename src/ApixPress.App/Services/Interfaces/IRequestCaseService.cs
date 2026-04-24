using ApixPress.App.Models.DTOs;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Interfaces;

public interface IRequestCaseService
{
    Task<IReadOnlyList<RequestCaseDto>> GetCasesAsync(string projectId, CancellationToken cancellationToken);

    Task<RequestCaseDto?> GetDetailAsync(string projectId, string id, CancellationToken cancellationToken);

    Task<IResultModel<RequestCaseDto>> SaveAsync(RequestCaseDto requestCase, CancellationToken cancellationToken);

    Task<ImportedHttpInterfaceSyncResultDto> SyncImportedHttpInterfacesAsync(string projectId, IReadOnlyList<ApiEndpointDto> endpoints, CancellationToken cancellationToken);

    Task<IResultModel<RequestCaseDto>> DuplicateAsync(string projectId, string id, CancellationToken cancellationToken);

    Task<IResultModel<bool>> DeleteAsync(string projectId, string id, CancellationToken cancellationToken);

    Task DeleteRangeAsync(string projectId, IReadOnlyList<string> ids, CancellationToken cancellationToken);
}
