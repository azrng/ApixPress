using ApixPress.App.Models.DTOs;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Interfaces;

public interface IRequestCaseService
{
    Task<IReadOnlyList<RequestCaseDto>> GetCasesAsync(string projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RequestCaseDto>> GetCaseDetailsAsync(string projectId, CancellationToken cancellationToken);

    Task<RequestCaseDto?> GetDetailAsync(string projectId, string id, CancellationToken cancellationToken);

    Task<IResultModel<RequestCaseDto>> SaveAsync(RequestCaseDto requestCase, CancellationToken cancellationToken);

    /// <summary>在指定父路径下新建目录，父路径为空表示根目录；目录层级最多两级。</summary>
    Task<IResultModel<RequestCaseDto>> CreateFolderAsync(string projectId, string parentFolderPath, string name, CancellationToken cancellationToken);

    Task<IResultModel<int>> SaveRangeAsync(IEnumerable<RequestCaseDto> requestCases, CancellationToken cancellationToken);

    Task<ImportedHttpInterfaceSyncResultDto> SyncImportedHttpInterfacesAsync(string projectId, IReadOnlyList<ApiEndpointDto> endpoints, CancellationToken cancellationToken);

    Task<IResultModel<RequestCaseDto>> DuplicateAsync(string projectId, string id, CancellationToken cancellationToken);

    Task<IResultModel<bool>> DeleteAsync(string projectId, string id, CancellationToken cancellationToken);

    Task DeleteRangeAsync(string projectId, IReadOnlyList<string> ids, CancellationToken cancellationToken);
}
