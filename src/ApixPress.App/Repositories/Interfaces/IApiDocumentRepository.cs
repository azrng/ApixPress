using ApixPress.App.Models.Entities;

namespace ApixPress.App.Repositories.Interfaces;

public interface IApiDocumentRepository
{
    Task<IReadOnlyList<ApiDocumentEntity>> GetDocumentsAsync(string projectId, CancellationToken cancellationToken);

    Task<ApiDocumentEntity?> GetByIdAsync(string projectId, string documentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApiEndpointEntity>> GetEndpointsByDocumentIdAsync(string documentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApiProjectEndpointEntity>> GetEndpointsByProjectIdAsync(string projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RequestParameterEntity>> GetParametersByEndpointIdsAsync(IEnumerable<string> endpointIds, CancellationToken cancellationToken);

    Task DeleteEndpointsByIdsAsync(IEnumerable<string> endpointIds, CancellationToken cancellationToken);

    Task SaveDocumentGraphAsync(
        ApiDocumentEntity document,
        IReadOnlyList<ApiEndpointEntity> endpoints,
        IReadOnlyList<RequestParameterEntity> parameters,
        CancellationToken cancellationToken);
}
