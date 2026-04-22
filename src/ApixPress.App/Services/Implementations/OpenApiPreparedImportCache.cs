using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;

namespace ApixPress.App.Services.Implementations;

internal sealed class OpenApiPreparedImportCache
{
    private static readonly TimeSpan PreparedImportCacheLifetime = TimeSpan.FromMinutes(10);

    private readonly Dictionary<string, PreparedImportPayload> _preparedImports = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _preparedImportCacheLock = new();

    public bool TryGet(string projectId, string sourceType, string sourceValue, out ParsedDocumentGraph graph, out ApiImportPreviewDto preview)
    {
        lock (_preparedImportCacheLock)
        {
            if (!TryGetPayload(projectId, sourceType, sourceValue, removeAfterRead: false, out var payload))
            {
                graph = null!;
                preview = null!;
                return false;
            }

            graph = payload.Graph;
            preview = payload.Preview;
            return true;
        }
    }

    public bool TryTake(string projectId, string sourceType, string sourceValue, out ParsedDocumentGraph graph, out ApiImportPreviewDto preview)
    {
        lock (_preparedImportCacheLock)
        {
            if (!TryGetPayload(projectId, sourceType, sourceValue, removeAfterRead: true, out var payload))
            {
                graph = null!;
                preview = null!;
                return false;
            }

            graph = payload.Graph;
            preview = payload.Preview;
            return true;
        }
    }

    public void Cache(
        string projectId,
        string sourceType,
        string sourceValue,
        ParsedDocumentGraph graph,
        ApiImportPreviewDto preview)
    {
        lock (_preparedImportCacheLock)
        {
            _preparedImports[BuildPreparedImportKey(projectId, sourceType, sourceValue)] =
                new PreparedImportPayload(graph, preview, DateTime.UtcNow);
        }
    }

    public void Clear(string projectId, string sourceType, string sourceValue)
    {
        lock (_preparedImportCacheLock)
        {
            _preparedImports.Remove(BuildPreparedImportKey(projectId, sourceType, sourceValue));
        }
    }

    private bool TryGetPayload(
        string projectId,
        string sourceType,
        string sourceValue,
        bool removeAfterRead,
        out PreparedImportPayload payload)
    {
        var cacheKey = BuildPreparedImportKey(projectId, sourceType, sourceValue);
        if (!_preparedImports.TryGetValue(cacheKey, out payload!))
        {
            return false;
        }

        if (DateTime.UtcNow - payload.CachedAt > PreparedImportCacheLifetime)
        {
            _preparedImports.Remove(cacheKey);
            payload = null!;
            return false;
        }

        if (removeAfterRead)
        {
            _preparedImports.Remove(cacheKey);
        }

        return true;
    }

    private static string BuildPreparedImportKey(string projectId, string sourceType, string sourceValue)
    {
        return $"{projectId}::{sourceType.Trim().ToUpperInvariant()}::{sourceValue.Trim()}";
    }

    private sealed record PreparedImportPayload(ParsedDocumentGraph Graph, ApiImportPreviewDto Preview, DateTime CachedAt);
}
