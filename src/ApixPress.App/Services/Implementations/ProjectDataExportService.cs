using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.DependencyInjection;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Implementations;

public sealed class ProjectDataExportService : IProjectDataExportService, ITransientDependency
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions ImportSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IApiDocumentRepository _apiDocumentRepository;
    private readonly IRequestCaseService _requestCaseService;

    public ProjectDataExportService(
        IApiDocumentRepository apiDocumentRepository,
        IRequestCaseService requestCaseService)
    {
        _apiDocumentRepository = apiDocumentRepository;
        _requestCaseService = requestCaseService;
    }

    public async Task<IResultModel<ProjectDataExportResultDto>> ExportAsync(ProjectDataExportRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectId))
        {
            return ResultModel<ProjectDataExportResultDto>.Failure("导出失败：缺少项目标识。", "project_data_export_project_required");
        }

        if (string.IsNullOrWhiteSpace(request.OutputFilePath))
        {
            return ResultModel<ProjectDataExportResultDto>.Failure("导出失败：未选择导出文件。", "project_data_export_file_required");
        }

        try
        {
            var cases = await _requestCaseService.GetCaseDetailsAsync(request.ProjectId, cancellationToken);
            var interfaces = cases
                .Where(item => string.Equals(item.EntryType, "http-interface", StringComparison.OrdinalIgnoreCase))
                .Select(ToExportEntry)
                .ToList();
            var testCases = cases
                .Where(item => string.Equals(item.EntryType, "http-case", StringComparison.OrdinalIgnoreCase))
                .Select(ToExportEntry)
                .ToList();
            var package = new ProjectDataExportPackageDto
            {
                ExportedAt = DateTime.UtcNow,
                Project = new ProjectDataExportProjectDto
                {
                    Id = request.ProjectId,
                    Name = request.ProjectName,
                    Description = request.ProjectDescription
                },
                Interfaces = interfaces,
                TestCases = testCases
            };
            var directoryPath = Path.GetDirectoryName(request.OutputFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var json = JsonSerializer.Serialize(package, SerializerOptions);
            await File.WriteAllTextAsync(request.OutputFilePath, json, new UTF8Encoding(true), cancellationToken);
            return ResultModel<ProjectDataExportResultDto>.Success(new ProjectDataExportResultDto
            {
                FilePath = request.OutputFilePath,
                InterfaceCount = interfaces.Count,
                TestCaseCount = testCases.Count
            });
        }
        catch (Exception exception)
        {
            return ResultModel<ProjectDataExportResultDto>.Failure($"导出失败：{exception.Message}", "project_data_export_failed");
        }
    }

    public async Task<IResultModel<ApiImportPreviewDto>> PreviewImportPackageAsync(string projectId, string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var packageResult = await LoadPackageAsync(filePath, cancellationToken);
            if (!packageResult.IsSuccess || packageResult.Package is null)
            {
                return ResultModel<ApiImportPreviewDto>.Failure(packageResult.Message, packageResult.ErrorCode);
            }

            var existingEndpoints = await _apiDocumentRepository.GetEndpointsByProjectIdAsync(projectId, cancellationToken);
            var preview = BuildPreview(projectId, filePath, packageResult.Package, existingEndpoints);
            return ResultModel<ApiImportPreviewDto>.Success(preview);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<ApiImportPreviewDto>.Failure("已取消项目数据包导入预检查。", "project_data_package_preview_cancelled");
        }
        catch (Exception exception)
        {
            return ResultModel<ApiImportPreviewDto>.Failure($"项目数据包导入预检查失败：{exception.Message}", "project_data_package_preview_failed");
        }
    }

    public async Task<IResultModel<ApiDocumentDto>> ImportPackageAsync(string projectId, string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var packageResult = await LoadPackageAsync(filePath, cancellationToken);
            if (!packageResult.IsSuccess || packageResult.Package is null)
            {
                return ResultModel<ApiDocumentDto>.Failure(packageResult.Message, packageResult.ErrorCode);
            }

            var package = packageResult.Package;
            var importTime = DateTime.UtcNow;
            var documentId = Guid.NewGuid().ToString("N");
            var document = new ApiDocumentEntity
            {
                Id = documentId,
                ProjectId = projectId,
                Name = ResolveDocumentName(package, filePath),
                SourceType = "APIXPKG",
                SourceValue = filePath,
                BaseUrl = string.Empty,
                RawJson = packageResult.RawJson,
                ImportedAt = importTime
            };
            var endpointMaps = package.Interfaces
                .Select(item => BuildEndpointMap(documentId, item))
                .ToList();

            await _apiDocumentRepository.SaveDocumentGraphAsync(
                document,
                endpointMaps.Select(item => item.Endpoint).ToList(),
                endpointMaps.SelectMany(item => item.Parameters).ToList(),
                cancellationToken);

            var allProjectEndpoints = await LoadProjectEndpointsAsync(projectId, cancellationToken);
            var syncResult = await _requestCaseService.SyncImportedHttpInterfacesAsync(projectId, allProjectEndpoints, cancellationToken);
            var interfaceIdByEndpointKey = syncResult.UpsertedCases
                .Where(item => string.Equals(item.EntryType, "http-interface", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    item => item.RequestSnapshot.EndpointId,
                    item => item.Id,
                    StringComparer.OrdinalIgnoreCase);
            var packageInterfaceKeyById = endpointMaps.ToDictionary(
                item => item.Source.Id,
                item => BuildImportedEndpointKey(item.Endpoint.Method, item.Endpoint.Path),
                StringComparer.OrdinalIgnoreCase);

            foreach (var testCase in package.TestCases)
            {
                if (!packageInterfaceKeyById.TryGetValue(testCase.ParentId, out var parentEndpointKey)
                    || !interfaceIdByEndpointKey.TryGetValue(parentEndpointKey, out var parentInterfaceId))
                {
                    return ResultModel<ApiDocumentDto>.Failure(
                        $"项目数据包导入失败：用例“{testCase.Name}”未找到对应接口。",
                        "project_data_package_case_parent_missing");
                }

                var saveResult = await _requestCaseService.SaveAsync(new RequestCaseDto
                {
                    Id = testCase.Id,
                    ProjectId = projectId,
                    EntryType = "http-case",
                    Name = testCase.Name,
                    GroupName = string.IsNullOrWhiteSpace(testCase.GroupName) ? "用例" : testCase.GroupName,
                    FolderPath = testCase.FolderPath,
                    ParentId = parentInterfaceId,
                    Tags = testCase.Tags.ToList(),
                    Description = testCase.Description,
                    RequestSnapshot = new RequestSnapshotDto
                    {
                        EndpointId = parentEndpointKey,
                        Name = testCase.RequestSnapshot.Name,
                        Method = testCase.RequestSnapshot.Method,
                        Url = testCase.RequestSnapshot.Url,
                        Description = testCase.RequestSnapshot.Description,
                        BodyMode = testCase.RequestSnapshot.BodyMode,
                        BodyContent = testCase.RequestSnapshot.BodyContent,
                        IgnoreSslErrors = testCase.RequestSnapshot.IgnoreSslErrors,
                        QueryParameters = CloneKeyValues(testCase.RequestSnapshot.QueryParameters),
                        PathParameters = CloneKeyValues(testCase.RequestSnapshot.PathParameters),
                        Headers = CloneKeyValues(testCase.RequestSnapshot.Headers)
                    },
                    UpdatedAt = testCase.UpdatedAt == default ? importTime : testCase.UpdatedAt
                }, cancellationToken);

                if (!saveResult.IsSuccess)
                {
                    return ResultModel<ApiDocumentDto>.Failure(
                        string.IsNullOrWhiteSpace(saveResult.Message)
                            ? $"项目数据包导入失败：保存用例“{testCase.Name}”时发生错误。"
                            : saveResult.Message,
                        "project_data_package_case_save_failed");
                }
            }

            return ResultModel<ApiDocumentDto>.Success(new ApiDocumentDto
            {
                Id = document.Id,
                ProjectId = document.ProjectId,
                Name = document.Name,
                SourceType = document.SourceType,
                SourceValue = document.SourceValue,
                BaseUrl = document.BaseUrl,
                ImportedAt = document.ImportedAt
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<ApiDocumentDto>.Failure("已取消项目数据包导入。", "project_data_package_import_cancelled");
        }
        catch (Exception exception)
        {
            return ResultModel<ApiDocumentDto>.Failure($"项目数据包导入失败：{exception.Message}", "project_data_package_import_failed");
        }
    }

    private static ProjectDataExportEntryDto ToExportEntry(RequestCaseDto requestCase)
    {
        return new ProjectDataExportEntryDto
        {
            Id = requestCase.Id,
            Name = requestCase.Name,
            GroupName = requestCase.GroupName,
            FolderPath = requestCase.FolderPath,
            ParentId = requestCase.ParentId,
            Tags = requestCase.Tags.ToList(),
            Description = requestCase.Description,
            RequestSnapshot = requestCase.RequestSnapshot,
            UpdatedAt = requestCase.UpdatedAt
        };
    }

    private async Task<IReadOnlyList<ApiEndpointDto>> LoadProjectEndpointsAsync(string projectId, CancellationToken cancellationToken)
    {
        var endpoints = await _apiDocumentRepository.GetEndpointDetailsByProjectIdAsync(projectId, cancellationToken);
        var parameters = await _apiDocumentRepository.GetParametersByEndpointIdsAsync(endpoints.Select(item => item.Id), cancellationToken);
        var parameterLookup = parameters
            .GroupBy(item => item.EndpointId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        return endpoints.Select(endpoint => new ApiEndpointDto
        {
            Id = endpoint.Id,
            DocumentId = endpoint.DocumentId,
            GroupName = endpoint.GroupName,
            Name = endpoint.Name,
            Method = endpoint.Method,
            Path = endpoint.Path,
            Description = endpoint.Description,
            RequestBodyMode = endpoint.RequestBodyMode,
            RequestBodyTemplate = endpoint.RequestBodyTemplate,
            Parameters = parameterLookup.TryGetValue(endpoint.Id, out var endpointParameters)
                ? endpointParameters.Select(parameter => new RequestParameterDto
                {
                    Id = parameter.Id,
                    EndpointId = parameter.EndpointId,
                    ParameterType = Enum.TryParse<RequestParameterKind>(parameter.ParameterType, out var parameterType)
                        ? parameterType
                        : RequestParameterKind.Query,
                    Name = parameter.Name,
                    DefaultValue = parameter.DefaultValue,
                    Description = parameter.Description,
                    Required = parameter.Required
                }).ToList()
                : []
        }).ToList();
    }

    private static ApiImportPreviewDto BuildPreview(
        string projectId,
        string filePath,
        ProjectDataExportPackageDto package,
        IReadOnlyList<ApiProjectEndpointEntity> existingEndpoints)
    {
        var importedEndpoints = package.Interfaces
            .Select(item => new
            {
                Entry = item,
                Method = NormalizeMethod(item.RequestSnapshot.Method),
                Path = NormalizePath(item.RequestSnapshot.Url)
            })
            .ToList();
        var conflicts = importedEndpoints
            .Select(item => new
            {
                Imported = item,
                Existing = existingEndpoints.FirstOrDefault(endpoint =>
                    string.Equals(endpoint.Method, item.Method, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(endpoint.Path, item.Path, StringComparison.OrdinalIgnoreCase))
            })
            .Where(item => item.Existing is not null)
            .Select(item => new ApiImportConflictDto
            {
                ExistingDocumentId = item.Existing!.DocumentId,
                ExistingDocumentName = item.Existing.DocumentName,
                ExistingEndpointId = item.Existing.Id,
                ExistingEndpointName = item.Existing.Name,
                ImportedEndpointName = item.Imported.Entry.Name,
                Method = item.Imported.Method,
                Path = item.Imported.Path
            })
            .ToList();

        return new ApiImportPreviewDto
        {
            DocumentName = ResolveDocumentName(package, filePath),
            SourceType = "APIXPKG",
            SourceValue = filePath,
            TotalEndpointCount = importedEndpoints.Count,
            NewEndpointCount = importedEndpoints.Count - conflicts.Count,
            ConflictCount = conflicts.Count,
            ConflictItems = conflicts
        };
    }

    private static EndpointMap BuildEndpointMap(string documentId, ProjectDataExportEntryDto source)
    {
        var endpointId = Guid.NewGuid().ToString("N");
        var endpoint = new ApiEndpointEntity
        {
            Id = endpointId,
            DocumentId = documentId,
            GroupName = NormalizeGroupName(source),
            Name = source.Name,
            Method = NormalizeMethod(source.RequestSnapshot.Method),
            Path = NormalizePath(source.RequestSnapshot.Url),
            Description = source.Description,
            RequestBodyMode = source.RequestSnapshot.BodyMode,
            RequestBodyTemplate = source.RequestSnapshot.BodyContent
        };
        var parameters = new List<RequestParameterEntity>();
        parameters.AddRange(BuildParameterEntities(endpointId, RequestParameterKind.Query, source.RequestSnapshot.QueryParameters));
        parameters.AddRange(BuildParameterEntities(endpointId, RequestParameterKind.Path, source.RequestSnapshot.PathParameters));
        parameters.AddRange(BuildParameterEntities(endpointId, RequestParameterKind.Header, source.RequestSnapshot.Headers));
        return new EndpointMap(source, endpoint, parameters);
    }

    private static IEnumerable<RequestParameterEntity> BuildParameterEntities(
        string endpointId,
        RequestParameterKind parameterType,
        IReadOnlyList<RequestKeyValueDto> items)
    {
        return items.Select(item => new RequestParameterEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            EndpointId = endpointId,
            ParameterType = parameterType.ToString(),
            Name = item.Name,
            DefaultValue = item.Value,
            Description = string.Empty,
            Required = false
        });
    }

    private static List<RequestKeyValueDto> CloneKeyValues(IReadOnlyList<RequestKeyValueDto> items)
    {
        return items.Select(item => new RequestKeyValueDto
        {
            Name = item.Name,
            Value = item.Value,
            IsEnabled = item.IsEnabled
        }).ToList();
    }

    private static string NormalizeGroupName(ProjectDataExportEntryDto source)
    {
        if (!string.IsNullOrWhiteSpace(source.FolderPath))
        {
            return source.FolderPath.Trim();
        }

        if (!string.IsNullOrWhiteSpace(source.GroupName) && !string.Equals(source.GroupName, "接口", StringComparison.OrdinalIgnoreCase))
        {
            return source.GroupName.Trim();
        }

        return "默认模块";
    }

    private static string NormalizeMethod(string method)
    {
        return string.IsNullOrWhiteSpace(method) ? "GET" : method.Trim().ToUpperInvariant();
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
    }

    private static string BuildImportedEndpointKey(string method, string path)
    {
        return $"swagger-import:{NormalizeMethod(method)} {NormalizePath(path)}";
    }

    private static string ResolveDocumentName(ProjectDataExportPackageDto package, string filePath)
    {
        if (!string.IsNullOrWhiteSpace(package.Project.Name))
        {
            return package.Project.Name.Trim();
        }

        return Path.GetFileNameWithoutExtension(filePath);
    }

    private static async Task<PackageLoadResult> LoadPackageAsync(string filePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return PackageLoadResult.Failure("请选择要导入的项目数据包文件。", "project_data_package_file_required");
        }

        var rawJson = await File.ReadAllTextAsync(filePath, cancellationToken);
        var package = JsonSerializer.Deserialize<ProjectDataExportPackageDto>(rawJson, ImportSerializerOptions);
        if (package is null)
        {
            return PackageLoadResult.Failure("项目数据包解析失败：文件内容为空或结构无效。", "project_data_package_invalid");
        }

        if (!string.Equals(package.SchemaVersion, ProjectDataExportPackageDto.CurrentSchemaVersion, StringComparison.OrdinalIgnoreCase))
        {
            return PackageLoadResult.Failure($"项目数据包版本不受支持：{package.SchemaVersion}", "project_data_package_schema_not_supported");
        }

        return PackageLoadResult.Success(rawJson, package);
    }

    private sealed record EndpointMap(
        ProjectDataExportEntryDto Source,
        ApiEndpointEntity Endpoint,
        List<RequestParameterEntity> Parameters);

    private sealed record PackageLoadResult(
        bool IsSuccess,
        string Message,
        string ErrorCode,
        string RawJson,
        ProjectDataExportPackageDto? Package)
    {
        public static PackageLoadResult Success(string rawJson, ProjectDataExportPackageDto package)
        {
            return new PackageLoadResult(true, string.Empty, string.Empty, rawJson, package);
        }

        public static PackageLoadResult Failure(string message, string errorCode)
        {
            return new PackageLoadResult(false, message, errorCode, string.Empty, null);
        }
    }
}
