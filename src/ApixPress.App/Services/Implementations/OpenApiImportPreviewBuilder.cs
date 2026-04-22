using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;

namespace ApixPress.App.Services.Implementations;

internal static class OpenApiImportPreviewBuilder
{
    public static ApiImportPreviewDto Build(
        ParsedDocumentGraph graph,
        IReadOnlyList<ApiProjectEndpointEntity> existingEndpoints)
    {
        var existingByKey = existingEndpoints
            .GroupBy(endpoint => BuildImportedEndpointKey(endpoint.Method, endpoint.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var distinctIncomingKeys = graph.Endpoints
            .Select(endpoint => BuildImportedEndpointKey(endpoint.Method, endpoint.Path))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var conflicts = graph.Endpoints
            .Select(endpoint => new
            {
                Endpoint = endpoint,
                Key = BuildImportedEndpointKey(endpoint.Method, endpoint.Path)
            })
            .Where(item => existingByKey.TryGetValue(item.Key, out _))
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var endpoint = group.Last().Endpoint;
                var existing = existingByKey[group.Key];
                return new ApiImportConflictDto
                {
                    ExistingDocumentId = existing.DocumentId,
                    ExistingDocumentName = existing.DocumentName,
                    ExistingEndpointId = existing.Id,
                    ExistingEndpointName = existing.Name,
                    ImportedEndpointName = endpoint.Name,
                    Method = endpoint.Method,
                    Path = endpoint.Path
                };
            })
            .OrderBy(item => item.Method, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ApiImportPreviewDto
        {
            DocumentName = graph.Document.Name,
            SourceType = graph.Document.SourceType,
            SourceValue = graph.Document.SourceValue,
            TotalEndpointCount = distinctIncomingKeys.Count,
            ConflictCount = conflicts.Count,
            NewEndpointCount = Math.Max(0, distinctIncomingKeys.Count - conflicts.Count),
            ConflictItems = conflicts
        };
    }

    public static string BuildImportedEndpointKey(RequestCaseDto requestCase)
    {
        return BuildImportedEndpointKey(requestCase.RequestSnapshot.Method, requestCase.RequestSnapshot.Url);
    }

    public static string BuildImportedEndpointKey(string method, string path)
    {
        return $"swagger-import:{method.ToUpperInvariant()} {path}";
    }
}
