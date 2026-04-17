using Azrng.Core.DependencyInjection;
using Azrng.Core.Exceptions;
using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Implementations;

public sealed class ApiWorkspaceService : IApiWorkspaceService, ITransientDependency
{
    private const long MaxSwaggerFileSizeBytes = 20 * 1024 * 1024;
    private static readonly TimeSpan UrlImportCacheLifetime = TimeSpan.FromMinutes(10);

    private readonly IApiDocumentRepository _apiDocumentRepository;
    private readonly Func<Uri, CancellationToken, Task<string>> _downloadOpenApiDocumentAsync;
    private readonly Dictionary<string, CachedUrlImportPayload> _cachedUrlImports = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _urlImportCacheLock = new();

    public ApiWorkspaceService(IApiDocumentRepository apiDocumentRepository)
        : this(apiDocumentRepository, static async (targetUri, cancellationToken) =>
        {
            using var httpClient = new HttpClient();
            return await httpClient.GetStringAsync(targetUri, cancellationToken);
        })
    {
    }

    public ApiWorkspaceService(
        IApiDocumentRepository apiDocumentRepository,
        Func<Uri, CancellationToken, Task<string>> downloadOpenApiDocumentAsync)
    {
        _apiDocumentRepository = apiDocumentRepository;
        _downloadOpenApiDocumentAsync = downloadOpenApiDocumentAsync;
    }

    public async Task<IReadOnlyList<ApiDocumentDto>> GetDocumentsAsync(string projectId, CancellationToken cancellationToken)
    {
        var documents = await _apiDocumentRepository.GetDocumentsAsync(projectId, cancellationToken);
        return documents
            .OrderByDescending(item => item.ImportedAt)
            .Select(ToDocumentDto)
            .ToList();
    }

    public async Task<IReadOnlyList<ApiEndpointDto>> GetEndpointsAsync(string documentId, CancellationToken cancellationToken)
    {
        var endpoints = await _apiDocumentRepository.GetEndpointsByDocumentIdAsync(documentId, cancellationToken);
        var parameters = await _apiDocumentRepository.GetParametersByEndpointIdsAsync(endpoints.Select(item => item.Id), cancellationToken);
        var parameterLookup = parameters.GroupBy(item => item.EndpointId).ToDictionary(group => group.Key, group => group.ToList());

        return endpoints.Select(endpoint => new ApiEndpointDto
        {
            Id = endpoint.Id,
            DocumentId = endpoint.DocumentId,
            GroupName = endpoint.GroupName,
            Name = endpoint.Name,
            Method = endpoint.Method,
            Path = endpoint.Path,
            Description = endpoint.Description,
            RequestBodyTemplate = endpoint.RequestBodyTemplate,
            Parameters = parameterLookup.TryGetValue(endpoint.Id, out var endpointParameters)
                ? endpointParameters.Select(ToParameterDto).ToList()
                : []
        }).ToList();
    }

    public async Task<ApiDocumentDto?> GetDocumentAsync(string projectId, string documentId, CancellationToken cancellationToken)
    {
        var document = await _apiDocumentRepository.GetByIdAsync(projectId, documentId, cancellationToken);
        return document is null ? null : ToDocumentDto(document);
    }

    public async Task<IResultModel<ApiDocumentDto>> ImportFromUrlAsync(string projectId, string url, CancellationToken cancellationToken)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var targetUri))
            {
                return ResultModel<ApiDocumentDto>.Failure("请输入有效的 Swagger/OpenAPI 文档 URL。");
            }

            var json = await GetCachedOrDownloadUrlDocumentAsync(projectId, url, targetUri, cancellationToken);
            return await ImportCoreAsync(projectId, "URL", url, json, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<ApiDocumentDto>.Failure("已取消 URL 导入。", "swagger_import_cancelled");
        }
        catch (Exception exception)
        {
            return ResultModel<ApiDocumentDto>.Failure($"URL 导入失败：{exception.Message}");
        }
        finally
        {
            ClearCachedUrlDocument(projectId, url);
        }
    }

    public async Task<IResultModel<ApiImportPreviewDto>> PreviewImportFromUrlAsync(string projectId, string url, CancellationToken cancellationToken)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var targetUri))
            {
                return ResultModel<ApiImportPreviewDto>.Failure("请输入有效的 Swagger/OpenAPI 文档 URL。");
            }

            var json = await DownloadAndCacheUrlDocumentAsync(projectId, url, targetUri, cancellationToken);
            var previewResult = await PreviewImportCoreAsync(projectId, "URL", url, json, cancellationToken);
            if (!previewResult.IsSuccess)
            {
                ClearCachedUrlDocument(projectId, url);
            }

            return previewResult;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<ApiImportPreviewDto>.Failure("已取消导入预检查。", "swagger_import_preview_cancelled");
        }
        catch (Exception exception)
        {
            return ResultModel<ApiImportPreviewDto>.Failure($"URL 导入预检查失败：{exception.Message}", "swagger_import_preview_failed");
        }
    }

    public async Task<IResultModel<ApiDocumentDto>> ImportFromFileAsync(string projectId, string filePath, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return ResultModel<ApiDocumentDto>.Failure("未找到指定的本地文件。", "swagger_file_not_found");
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxSwaggerFileSizeBytes)
            {
                return ResultModel<ApiDocumentDto>.Failure("Swagger 文件超过 20MB 限制。", "swagger_file_too_large");
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return await ImportCoreAsync(projectId, "FILE", filePath, json, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<ApiDocumentDto>.Failure("已取消本地文件导入。", "swagger_import_cancelled");
        }
        catch (Exception exception)
        {
            return ResultModel<ApiDocumentDto>.Failure($"本地文件导入失败：{exception.Message}", "swagger_import_file_failed");
        }
    }

    public async Task<IResultModel<ApiImportPreviewDto>> PreviewImportFromFileAsync(string projectId, string filePath, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return ResultModel<ApiImportPreviewDto>.Failure("未找到指定的本地文件。", "swagger_file_not_found");
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxSwaggerFileSizeBytes)
            {
                return ResultModel<ApiImportPreviewDto>.Failure("Swagger 文件超过 20MB 限制。", "swagger_file_too_large");
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return await PreviewImportCoreAsync(projectId, "FILE", filePath, json, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<ApiImportPreviewDto>.Failure("已取消导入预检查。", "swagger_import_preview_cancelled");
        }
        catch (Exception exception)
        {
            return ResultModel<ApiImportPreviewDto>.Failure($"本地文件导入预检查失败：{exception.Message}", "swagger_import_preview_failed");
        }
    }

    public async Task DeleteImportedHttpInterfacesAsync(string projectId, IReadOnlyList<RequestCaseDto> requestCases, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectId) || requestCases.Count == 0)
        {
            return;
        }

        var requestCaseKeys = requestCases
            .Select(BuildImportedEndpointKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var endpointIds = (await _apiDocumentRepository.GetEndpointsByProjectIdAsync(projectId, cancellationToken))
            .Where(endpoint => requestCaseKeys.Contains(BuildImportedEndpointKey(endpoint.Method, endpoint.Path)))
            .Select(endpoint => endpoint.Id)
            .ToList();

        await _apiDocumentRepository.DeleteEndpointsByIdsAsync(endpointIds, cancellationToken);
    }

    private async Task<IResultModel<ApiDocumentDto>> ImportCoreAsync(string projectId, string sourceType, string sourceValue, string json, CancellationToken cancellationToken)
    {
        try
        {
            var graph = OpenApiJsonParser.Parse(json, sourceType, sourceValue);
            graph.Document.ProjectId = projectId;
            await _apiDocumentRepository.SaveDocumentGraphAsync(graph.Document, graph.Endpoints, graph.Parameters, cancellationToken);
            return ResultModel<ApiDocumentDto>.Success(ToDocumentDto(graph.Document));
        }
        catch (BaseException exception)
        {
            return ResultModel<ApiDocumentDto>.Failure(exception.Message, exception.ErrorCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<ApiDocumentDto>.Failure("导入已取消。", "swagger_import_cancelled");
        }
        catch (Exception exception)
        {
            return ResultModel<ApiDocumentDto>.Failure($"导入失败：{exception.Message}", "swagger_import_failed");
        }
    }

    private async Task<IResultModel<ApiImportPreviewDto>> PreviewImportCoreAsync(
        string projectId,
        string sourceType,
        string sourceValue,
        string json,
        CancellationToken cancellationToken)
    {
        try
        {
            var graph = OpenApiJsonParser.Parse(json, sourceType, sourceValue);
            var existingEndpoints = await _apiDocumentRepository.GetEndpointsByProjectIdAsync(projectId, cancellationToken);
            return ResultModel<ApiImportPreviewDto>.Success(BuildImportPreview(graph, existingEndpoints));
        }
        catch (BaseException exception)
        {
            return ResultModel<ApiImportPreviewDto>.Failure(exception.Message, exception.ErrorCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<ApiImportPreviewDto>.Failure("导入预检查已取消。", "swagger_import_preview_cancelled");
        }
        catch (Exception exception)
        {
            return ResultModel<ApiImportPreviewDto>.Failure($"导入预检查失败：{exception.Message}", "swagger_import_preview_failed");
        }
    }

    private static ApiDocumentDto ToDocumentDto(ApiDocumentEntity entity)
    {
        var resolvedBaseUrl = string.IsNullOrWhiteSpace(entity.BaseUrl)
            ? OpenApiJsonParser.InferBaseUrlFromImportSource(entity.SourceType, entity.SourceValue)
            : entity.BaseUrl;

        return new ApiDocumentDto
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            Name = entity.Name,
            SourceType = entity.SourceType,
            SourceValue = entity.SourceValue,
            BaseUrl = resolvedBaseUrl,
            ImportedAt = entity.ImportedAt
        };
    }

    private static RequestParameterDto ToParameterDto(RequestParameterEntity entity)
    {
        return new RequestParameterDto
        {
            Id = entity.Id,
            EndpointId = entity.EndpointId,
            ParameterType = Enum.TryParse<RequestParameterKind>(entity.ParameterType, out var parameterType)
                ? parameterType
                : RequestParameterKind.Query,
            Name = entity.Name,
            DefaultValue = entity.DefaultValue,
            Description = entity.Description,
            Required = entity.Required
        };
    }

    private static ApiImportPreviewDto BuildImportPreview(
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

    private static string BuildImportedEndpointKey(RequestCaseDto requestCase)
    {
        return BuildImportedEndpointKey(requestCase.RequestSnapshot.Method, requestCase.RequestSnapshot.Url);
    }

    private static string BuildImportedEndpointKey(string method, string path)
    {
        return $"swagger-import:{method.ToUpperInvariant()} {path}";
    }

    private static bool MatchesImportedInterface(ApiProjectEndpointEntity endpoint, RequestCaseDto requestCase)
    {
        var endpointKey = BuildImportedEndpointKey(endpoint.Method, endpoint.Path);
        if (string.Equals(requestCase.RequestSnapshot.EndpointId, endpointKey, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(requestCase.RequestSnapshot.Method, endpoint.Method, StringComparison.OrdinalIgnoreCase)
            && string.Equals(requestCase.RequestSnapshot.Url, endpoint.Path, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> DownloadAndCacheUrlDocumentAsync(
        string projectId,
        string url,
        Uri targetUri,
        CancellationToken cancellationToken)
    {
        var json = await _downloadOpenApiDocumentAsync(targetUri, cancellationToken);
        CacheUrlDocument(projectId, url, json);
        return json;
    }

    private async Task<string> GetCachedOrDownloadUrlDocumentAsync(
        string projectId,
        string url,
        Uri targetUri,
        CancellationToken cancellationToken)
    {
        if (TryGetCachedUrlDocument(projectId, url, out var cachedJson))
        {
            return cachedJson;
        }

        return await DownloadAndCacheUrlDocumentAsync(projectId, url, targetUri, cancellationToken);
    }

    private bool TryGetCachedUrlDocument(string projectId, string url, out string json)
    {
        lock (_urlImportCacheLock)
        {
            var cacheKey = BuildCachedUrlImportKey(projectId, url);
            if (!_cachedUrlImports.TryGetValue(cacheKey, out var payload))
            {
                json = string.Empty;
                return false;
            }

            if (DateTime.UtcNow - payload.CachedAt > UrlImportCacheLifetime)
            {
                _cachedUrlImports.Remove(cacheKey);
                json = string.Empty;
                return false;
            }

            json = payload.Json;
            return true;
        }
    }

    private void CacheUrlDocument(string projectId, string url, string json)
    {
        lock (_urlImportCacheLock)
        {
            _cachedUrlImports[BuildCachedUrlImportKey(projectId, url)] = new CachedUrlImportPayload(json, DateTime.UtcNow);
        }
    }

    private void ClearCachedUrlDocument(string projectId, string url)
    {
        lock (_urlImportCacheLock)
        {
            _cachedUrlImports.Remove(BuildCachedUrlImportKey(projectId, url));
        }
    }

    private static string BuildCachedUrlImportKey(string projectId, string url)
    {
        return $"{projectId}::{url.Trim()}";
    }

    private sealed record CachedUrlImportPayload(string Json, DateTime CachedAt);
}
