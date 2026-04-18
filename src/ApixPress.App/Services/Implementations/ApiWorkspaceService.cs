using Azrng.Core.DependencyInjection;
using Azrng.Core.Exceptions;
using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.Results;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace ApixPress.App.Services.Implementations;

public sealed class ApiWorkspaceService : IApiWorkspaceService, ITransientDependency
{
    private const long MaxSwaggerFileSizeBytes = 20 * 1024 * 1024;
    private static readonly TimeSpan SwaggerUrlDownloadTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PreparedImportCacheLifetime = TimeSpan.FromMinutes(10);
    private static readonly HttpClient SharedHttpClient = CreateSwaggerImportHttpClient();

    private readonly IApiDocumentRepository _apiDocumentRepository;
    private readonly Func<Uri, CancellationToken, Task<string>> _downloadOpenApiDocumentAsync;
    private readonly Func<string, CancellationToken, Task<string>> _readOpenApiFileAsync;
    private readonly Func<string, string, string, ParsedDocumentGraph> _parseOpenApiDocument;
    private readonly Dictionary<string, PreparedImportPayload> _preparedImports = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _preparedImportCacheLock = new();

    public ApiWorkspaceService(IApiDocumentRepository apiDocumentRepository)
        : this(apiDocumentRepository, DownloadOpenApiDocumentAsync, ReadOpenApiFileAsync, OpenApiJsonParser.Parse)
    {
    }

    public ApiWorkspaceService(
        IApiDocumentRepository apiDocumentRepository,
        Func<Uri, CancellationToken, Task<string>> downloadOpenApiDocumentAsync)
        : this(apiDocumentRepository, downloadOpenApiDocumentAsync, ReadOpenApiFileAsync, OpenApiJsonParser.Parse)
    {
    }

    public ApiWorkspaceService(
        IApiDocumentRepository apiDocumentRepository,
        Func<Uri, CancellationToken, Task<string>> downloadOpenApiDocumentAsync,
        Func<string, CancellationToken, Task<string>> readOpenApiFileAsync,
        Func<string, string, string, ParsedDocumentGraph> parseOpenApiDocument)
    {
        _apiDocumentRepository = apiDocumentRepository;
        _downloadOpenApiDocumentAsync = downloadOpenApiDocumentAsync;
        _readOpenApiFileAsync = readOpenApiFileAsync;
        _parseOpenApiDocument = parseOpenApiDocument;
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
        return MapEndpoints(endpoints, parameters);
    }

    public async Task<IReadOnlyList<ApiEndpointDto>> GetProjectEndpointsAsync(string projectId, CancellationToken cancellationToken)
    {
        var endpoints = await _apiDocumentRepository.GetEndpointDetailsByProjectIdAsync(projectId, cancellationToken);
        var parameters = await _apiDocumentRepository.GetParametersByEndpointIdsAsync(endpoints.Select(item => item.Id), cancellationToken);
        return MapEndpoints(endpoints, parameters);
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

            if (TryTakePreparedImport(projectId, "URL", url, out var preparedImport))
            {
                return await ImportPreparedGraphAsync(projectId, preparedImport.Graph, cancellationToken);
            }

            var json = await _downloadOpenApiDocumentAsync(targetUri, cancellationToken);
            return await ImportCoreAsync(projectId, "URL", url, json, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<ApiDocumentDto>.Failure("已取消 URL 导入。", "swagger_import_cancelled");
        }
        catch (OperationCanceledException)
        {
            return ResultModel<ApiDocumentDto>.Failure("获取 Swagger URL 超时，请稍后重试，或先下载为本地文件再导入。", "swagger_import_timeout");
        }
        catch (HttpRequestException exception)
        {
            return ResultModel<ApiDocumentDto>.Failure(BuildUrlImportHttpFailureMessage("URL 导入失败", exception), "swagger_import_http_failed");
        }
        catch (Exception exception)
        {
            return ResultModel<ApiDocumentDto>.Failure($"URL 导入失败：{exception.Message}");
        }
        finally
        {
            ClearPreparedImport(projectId, "URL", url);
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

            if (TryGetPreparedImport(projectId, "URL", url, out var preparedImport))
            {
                return ResultModel<ApiImportPreviewDto>.Success(preparedImport.Preview);
            }

            var json = await _downloadOpenApiDocumentAsync(targetUri, cancellationToken);
            var previewResult = await PreviewImportCoreAsync(projectId, "URL", url, json, cancellationToken);
            if (!previewResult.IsSuccess)
            {
                ClearPreparedImport(projectId, "URL", url);
            }

            return previewResult;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<ApiImportPreviewDto>.Failure("已取消导入预检查。", "swagger_import_preview_cancelled");
        }
        catch (OperationCanceledException)
        {
            return ResultModel<ApiImportPreviewDto>.Failure("获取 Swagger URL 超时，请稍后重试，或先下载为本地文件再导入。", "swagger_import_timeout");
        }
        catch (HttpRequestException exception)
        {
            return ResultModel<ApiImportPreviewDto>.Failure(BuildUrlImportHttpFailureMessage("URL 导入预检查失败", exception), "swagger_import_http_failed");
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

            if (TryTakePreparedImport(projectId, "FILE", filePath, out var preparedImport))
            {
                return await ImportPreparedGraphAsync(projectId, preparedImport.Graph, cancellationToken);
            }

            var json = await _readOpenApiFileAsync(filePath, cancellationToken);
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
        finally
        {
            ClearPreparedImport(projectId, "FILE", filePath);
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

            if (TryGetPreparedImport(projectId, "FILE", filePath, out var preparedImport))
            {
                return ResultModel<ApiImportPreviewDto>.Success(preparedImport.Preview);
            }

            var json = await _readOpenApiFileAsync(filePath, cancellationToken);
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
            var graph = _parseOpenApiDocument(json, sourceType, sourceValue);
            return await ImportPreparedGraphAsync(projectId, graph, cancellationToken);
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
            var graph = _parseOpenApiDocument(json, sourceType, sourceValue);
            var existingEndpoints = await _apiDocumentRepository.GetEndpointsByProjectIdAsync(projectId, cancellationToken);
            var preview = BuildImportPreview(graph, existingEndpoints);
            CachePreparedImport(projectId, sourceType, sourceValue, graph, preview);
            return ResultModel<ApiImportPreviewDto>.Success(preview);
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

    private async Task<IResultModel<ApiDocumentDto>> ImportPreparedGraphAsync(
        string projectId,
        ParsedDocumentGraph graph,
        CancellationToken cancellationToken)
    {
        graph.Document.ProjectId = projectId;
        await _apiDocumentRepository.SaveDocumentGraphAsync(graph.Document, graph.Endpoints, graph.Parameters, cancellationToken);
        return ResultModel<ApiDocumentDto>.Success(ToDocumentDto(graph.Document));
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

    private static IReadOnlyList<ApiEndpointDto> MapEndpoints(
        IReadOnlyList<ApiEndpointEntity> endpoints,
        IReadOnlyList<RequestParameterEntity> parameters)
    {
        var parameterLookup = parameters
            .GroupBy(item => item.EndpointId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(ToParameterDto).ToList(), StringComparer.OrdinalIgnoreCase);

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
                ? endpointParameters
                : []
        }).ToList();
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

    private bool TryGetPreparedImport(string projectId, string sourceType, string sourceValue, out PreparedImportPayload payload)
    {
        lock (_preparedImportCacheLock)
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

            return true;
        }
    }

    private bool TryTakePreparedImport(string projectId, string sourceType, string sourceValue, out PreparedImportPayload payload)
    {
        lock (_preparedImportCacheLock)
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

            _preparedImports.Remove(cacheKey);
            return true;
        }
    }

    private void CachePreparedImport(
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

    private void ClearPreparedImport(string projectId, string sourceType, string sourceValue)
    {
        lock (_preparedImportCacheLock)
        {
            _preparedImports.Remove(BuildPreparedImportKey(projectId, sourceType, sourceValue));
        }
    }

    private static string BuildPreparedImportKey(string projectId, string sourceType, string sourceValue)
    {
        return $"{projectId}::{sourceType.Trim().ToUpperInvariant()}::{sourceValue.Trim()}";
    }

    private static Task<string> ReadOpenApiFileAsync(string filePath, CancellationToken cancellationToken)
    {
        return File.ReadAllTextAsync(filePath, cancellationToken);
    }

    private static async Task<string> DownloadOpenApiDocumentAsync(Uri targetUri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, targetUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/yaml"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/yaml"));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(SwaggerUrlDownloadTimeout);

        using var response = await SharedHttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeoutCts.Token);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"远程服务返回 {(int)response.StatusCode} {response.ReasonPhrase}".Trim(),
                null,
                response.StatusCode);
        }

        if (response.Content.Headers.ContentLength is > MaxSwaggerFileSizeBytes)
        {
            throw new InvalidOperationException("Swagger 文档超过 20MB 限制。");
        }

        var document = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        if (Encoding.UTF8.GetByteCount(document) > MaxSwaggerFileSizeBytes)
        {
            throw new InvalidOperationException("Swagger 文档超过 20MB 限制。");
        }

        return document;
    }

    private static HttpClient CreateSwaggerImportHttpClient()
    {
        var httpClient = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ApixPress/1.0");
        return httpClient;
    }

    private static string BuildUrlImportHttpFailureMessage(string prefix, HttpRequestException exception)
    {
        if (exception.StatusCode is HttpStatusCode statusCode)
        {
            return $"{prefix}：远程服务返回 {(int)statusCode} {statusCode}。";
        }

        return $"{prefix}：{exception.Message}";
    }

    private sealed record PreparedImportPayload(ParsedDocumentGraph Graph, ApiImportPreviewDto Preview, DateTime CachedAt);
}
