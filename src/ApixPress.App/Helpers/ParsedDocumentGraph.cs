using ApixPress.App.Models.Entities;

namespace ApixPress.App.Helpers;

public sealed class ParsedDocumentGraph
{
    public required ApiDocumentEntity Document { get; init; }
    public required IReadOnlyList<ApiEndpointEntity> Endpoints { get; init; }
    public required IReadOnlyList<RequestParameterEntity> Parameters { get; init; }
}
