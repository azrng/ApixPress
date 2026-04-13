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

    private readonly IApiDocumentRepository _apiDocumentRepository;

    public ApiWorkspaceService(IApiDocumentRepository apiDocumentRepository)
    {
        _apiDocumentRepository = apiDocumentRepository;
    }

    public async Task<IReadOnlyList<ApiDocumentDto>> GetDocumentsAsync(string projectId, CancellationToken cancellationToken)
    {
        var documents = await _apiDocumentRepository.GetDocumentsAsync(projectId, cancellationToken);
        return documents
            .OrderByDescending(item => item.ImportedAt)
            .Take(1)
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

            using var httpClient = new HttpClient();
            var json = await httpClient.GetStringAsync(targetUri, cancellationToken);
            return await ImportCoreAsync(projectId, "URL", url, json, cancellationToken);
        }
        catch (Exception exception)
        {
            return ResultModel<ApiDocumentDto>.Failure($"URL 导入失败：{exception.Message}");
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
        catch (Exception exception)
        {
            return ResultModel<ApiDocumentDto>.Failure($"本地文件导入失败：{exception.Message}", "swagger_import_file_failed");
        }
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
        catch (Exception exception)
        {
            return ResultModel<ApiDocumentDto>.Failure($"导入失败：{exception.Message}", "swagger_import_failed");
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
}
