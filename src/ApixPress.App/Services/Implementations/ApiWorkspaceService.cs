using Azrng.Core.DependencyInjection;
using Azrng.Core.Exceptions;
using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.Results;
using System.Net;

namespace ApixPress.App.Services.Implementations;

public sealed class ApiWorkspaceService : IApiWorkspaceService, ITransientDependency
{
    private readonly IApiDocumentRepository _apiDocumentRepository;
    private readonly Func<string, string, string, ParsedDocumentGraph> _parseOpenApiDocument;
    private readonly OpenApiImportSourceReader _sourceReader;
    private readonly OpenApiPreparedImportCache _preparedImportCache;

    public ApiWorkspaceService(IApiDocumentRepository apiDocumentRepository)
        : this(apiDocumentRepository, new OpenApiImportSourceReader(), OpenApiJsonParser.Parse)
    {
    }

    public ApiWorkspaceService(
        IApiDocumentRepository apiDocumentRepository,
        Func<Uri, CancellationToken, Task<string>> downloadOpenApiDocumentAsync)
        : this(
            apiDocumentRepository,
            new OpenApiImportSourceReader(downloadOpenApiDocumentAsync, File.ReadAllTextAsync),
            OpenApiJsonParser.Parse)
    {
    }

    public ApiWorkspaceService(
        IApiDocumentRepository apiDocumentRepository,
        Func<Uri, CancellationToken, Task<string>> downloadOpenApiDocumentAsync,
        Func<string, CancellationToken, Task<string>> readOpenApiFileAsync,
        Func<string, string, string, ParsedDocumentGraph> parseOpenApiDocument)
        : this(
            apiDocumentRepository,
            new OpenApiImportSourceReader(downloadOpenApiDocumentAsync, readOpenApiFileAsync),
            parseOpenApiDocument)
    {
    }

    private ApiWorkspaceService(
        IApiDocumentRepository apiDocumentRepository,
        OpenApiImportSourceReader sourceReader,
        Func<string, string, string, ParsedDocumentGraph> parseOpenApiDocument)
    {
        _apiDocumentRepository = apiDocumentRepository;
        _sourceReader = sourceReader;
        _parseOpenApiDocument = parseOpenApiDocument;
        _preparedImportCache = new OpenApiPreparedImportCache();
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

            if (_preparedImportCache.TryTake(projectId, "URL", url, out var preparedGraph, out _))
            {
                return await ImportPreparedGraphAsync(projectId, preparedGraph, cancellationToken);
            }

            var json = await _sourceReader.ReadFromUrlAsync(targetUri, cancellationToken);
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
            _preparedImportCache.Clear(projectId, "URL", url);
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

            if (_preparedImportCache.TryGet(projectId, "URL", url, out _, out var preparedPreview))
            {
                return ResultModel<ApiImportPreviewDto>.Success(preparedPreview);
            }

            var json = await _sourceReader.ReadFromUrlAsync(targetUri, cancellationToken);
            var previewResult = await PreviewImportCoreAsync(projectId, "URL", url, json, cancellationToken);
            if (!previewResult.IsSuccess)
            {
                _preparedImportCache.Clear(projectId, "URL", url);
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
            if (_preparedImportCache.TryTake(projectId, "FILE", filePath, out var preparedGraph, out _))
            {
                return await ImportPreparedGraphAsync(projectId, preparedGraph, cancellationToken);
            }

            var json = await _sourceReader.ReadFromFileAsync(filePath, cancellationToken);
            return await ImportCoreAsync(projectId, "FILE", filePath, json, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<ApiDocumentDto>.Failure("已取消本地文件导入。", "swagger_import_cancelled");
        }
        catch (OpenApiImportSourceException exception)
        {
            return ResultModel<ApiDocumentDto>.Failure(exception.Message, exception.ErrorCode);
        }
        catch (Exception exception)
        {
            return ResultModel<ApiDocumentDto>.Failure($"本地文件导入失败：{exception.Message}", "swagger_import_file_failed");
        }
        finally
        {
            _preparedImportCache.Clear(projectId, "FILE", filePath);
        }
    }

    public async Task<IResultModel<ApiImportPreviewDto>> PreviewImportFromFileAsync(string projectId, string filePath, CancellationToken cancellationToken)
    {
        try
        {
            if (_preparedImportCache.TryGet(projectId, "FILE", filePath, out _, out var preparedPreview))
            {
                return ResultModel<ApiImportPreviewDto>.Success(preparedPreview);
            }

            var json = await _sourceReader.ReadFromFileAsync(filePath, cancellationToken);
            return await PreviewImportCoreAsync(projectId, "FILE", filePath, json, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<ApiImportPreviewDto>.Failure("已取消导入预检查。", "swagger_import_preview_cancelled");
        }
        catch (OpenApiImportSourceException exception)
        {
            return ResultModel<ApiImportPreviewDto>.Failure(exception.Message, exception.ErrorCode);
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
            .Select(OpenApiImportPreviewBuilder.BuildImportedEndpointKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var endpointIds = (await _apiDocumentRepository.GetEndpointsByProjectIdAsync(projectId, cancellationToken))
            .Where(endpoint => requestCaseKeys.Contains(OpenApiImportPreviewBuilder.BuildImportedEndpointKey(endpoint.Method, endpoint.Path)))
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
            var preview = OpenApiImportPreviewBuilder.Build(graph, existingEndpoints);
            _preparedImportCache.Cache(projectId, sourceType, sourceValue, graph, preview);
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
            RequestBodyMode = string.IsNullOrWhiteSpace(endpoint.RequestBodyMode) ? BodyModes.None : endpoint.RequestBodyMode,
            RequestBodyTemplate = endpoint.RequestBodyTemplate,
            Parameters = parameterLookup.TryGetValue(endpoint.Id, out var endpointParameters)
                ? endpointParameters
                : []
        }).ToList();
    }

    private static string BuildUrlImportHttpFailureMessage(string prefix, HttpRequestException exception)
    {
        if (exception.StatusCode is HttpStatusCode statusCode)
        {
            return $"{prefix}：远程服务返回 {(int)statusCode} {statusCode}。";
        }

        return $"{prefix}：{exception.Message}";
    }
}
