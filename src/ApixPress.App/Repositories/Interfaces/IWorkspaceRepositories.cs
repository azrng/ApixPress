using ApixPress.App.Models.Entities;

namespace ApixPress.App.Repositories.Interfaces;

public interface IApiDocumentRepository
{
    Task<IReadOnlyList<ApiDocumentEntity>> GetDocumentsAsync(string projectId, CancellationToken cancellationToken);

    Task<ApiDocumentEntity?> GetByIdAsync(string projectId, string documentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApiEndpointEntity>> GetEndpointsByDocumentIdAsync(string documentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RequestParameterEntity>> GetParametersByEndpointIdsAsync(IEnumerable<string> endpointIds, CancellationToken cancellationToken);

    Task SaveDocumentGraphAsync(
        ApiDocumentEntity document,
        IReadOnlyList<ApiEndpointEntity> endpoints,
        IReadOnlyList<RequestParameterEntity> parameters,
        CancellationToken cancellationToken);
}

public interface IRequestCaseRepository
{
    Task<IReadOnlyList<RequestCaseEntity>> GetCasesAsync(string projectId, CancellationToken cancellationToken);

    Task<RequestCaseEntity?> GetByIdAsync(string projectId, string id, CancellationToken cancellationToken);

    Task UpsertAsync(RequestCaseEntity entity, CancellationToken cancellationToken);

    Task DeleteAsync(string projectId, string id, CancellationToken cancellationToken);
}

public interface IEnvironmentVariableRepository
{
    Task<IReadOnlyList<EnvironmentVariableEntity>> GetByEnvironmentAsync(string environmentId, CancellationToken cancellationToken);

    Task UpsertAsync(EnvironmentVariableEntity entity, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);
}

public interface IRequestHistoryRepository
{
    Task<IReadOnlyList<RequestHistoryEntity>> GetHistoryAsync(string projectId, int limit, CancellationToken cancellationToken);

    Task UpsertAsync(RequestHistoryEntity entity, CancellationToken cancellationToken);

    Task DeleteAsync(string projectId, string id, CancellationToken cancellationToken);

    Task ClearAsync(string projectId, CancellationToken cancellationToken);
}
