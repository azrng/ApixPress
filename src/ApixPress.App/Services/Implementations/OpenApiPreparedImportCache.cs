using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;

namespace ApixPress.App.Services.Implementations;

internal sealed class OpenApiPreparedImportCache
{
    private static readonly TimeSpan PreparedImportCacheLifetime = TimeSpan.FromMinutes(10);
    // 解析图会与原始字符串同时驻留，按原始文本字符数的近似托管内存占用控制总量。
    private const int MaxCacheEntries = 6;
    private const long MaxCacheBytes = 96L * 1024 * 1024;

    private readonly Dictionary<string, PreparedImportPayload> _preparedImports = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _preparedImportCacheLock = new();
    private long _cachedBytes;

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
            EvictExpiredEntries();

            var cacheKey = BuildPreparedImportKey(projectId, sourceType, sourceValue);
            var estimatedBytes = EstimatePayloadBytes(graph);
            RemoveEntry(cacheKey);
            if (estimatedBytes > MaxCacheBytes)
            {
                return;
            }

            while (_preparedImports.Count >= MaxCacheEntries || _cachedBytes + estimatedBytes > MaxCacheBytes)
            {
                var overflowKeys = _preparedImports
                    .OrderBy(item => item.Value.CachedAt)
                    .Take(1)
                    .Select(item => item.Key)
                    .ToList();
                foreach (var key in overflowKeys)
                {
                    RemoveEntry(key);
                }
            }

            _preparedImports[cacheKey] = new PreparedImportPayload(graph, preview, DateTime.UtcNow, estimatedBytes);
            _cachedBytes += estimatedBytes;
        }
    }

    public void Clear(string projectId, string sourceType, string sourceValue)
    {
        lock (_preparedImportCacheLock)
        {
            RemoveEntry(BuildPreparedImportKey(projectId, sourceType, sourceValue));
        }
    }

    private void EvictExpiredEntries()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _preparedImports
            .Where(item => now - item.Value.CachedAt > PreparedImportCacheLifetime)
            .Select(item => item.Key)
            .ToList();
        foreach (var key in expiredKeys)
        {
            RemoveEntry(key);
        }
    }

    private void RemoveEntry(string key)
    {
        if (_preparedImports.Remove(key, out var payload))
        {
            _cachedBytes -= payload.EstimatedBytes;
        }
    }

    private static long EstimatePayloadBytes(ParsedDocumentGraph graph)
    {
        var rawJsonLength = graph.Document.RawJson?.Length ?? 0;
        return checked((long)rawJsonLength * sizeof(char) * 3);
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
            RemoveEntry(cacheKey);
            payload = null!;
            return false;
        }

        if (removeAfterRead)
        {
            RemoveEntry(cacheKey);
        }

        return true;
    }

    private static string BuildPreparedImportKey(string projectId, string sourceType, string sourceValue)
    {
        return $"{projectId}::{sourceType.Trim().ToUpperInvariant()}::{sourceValue.Trim()}";
    }

    private sealed record PreparedImportPayload(
        ParsedDocumentGraph Graph,
        ApiImportPreviewDto Preview,
        DateTime CachedAt,
        long EstimatedBytes);
}
