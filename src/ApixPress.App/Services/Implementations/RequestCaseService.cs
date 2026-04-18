using Azrng.Core.Json;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using ApixPress.App.Services.Interfaces;
using Azrng.Core;
using Azrng.Core.DependencyInjection;
using Azrng.Core.Results;
using Microsoft.Data.Sqlite;

namespace ApixPress.App.Services.Implementations;

public sealed class RequestCaseService : IRequestCaseService, ITransientDependency
{
    private const string ImportedEndpointKeyPrefix = "swagger-import:";
    private const string PreservedInterfaceParentIdPrefix = "preserved-interface:";

    private readonly IRequestCaseRepository _requestCaseRepository;
    private readonly IJsonSerializer _serializer;

    public RequestCaseService(IRequestCaseRepository requestCaseRepository, IJsonSerializer serializer)
    {
        _requestCaseRepository = requestCaseRepository;
        _serializer = serializer;
    }

    public async Task<IReadOnlyList<RequestCaseDto>> GetCasesAsync(string projectId, CancellationToken cancellationToken)
    {
        var cases = await _requestCaseRepository.GetCasesAsync(projectId, cancellationToken);
        return cases.Select(ToDto).ToList();
    }

    public async Task<IResultModel<RequestCaseDto>> SaveAsync(RequestCaseDto requestCase, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestCase.ProjectId))
        {
            return ResultModel<RequestCaseDto>.Failure("请先选择项目后再保存用例。", "request_case_project_required");
        }

        if (string.IsNullOrWhiteSpace(requestCase.Name))
        {
            return ResultModel<RequestCaseDto>.Failure("请输入用例名称。", "request_case_name_required");
        }

        var entity = ToEntity(requestCase);
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = Guid.NewGuid().ToString("N");
        }

        if (entity.UpdatedAt == default)
        {
            entity.UpdatedAt = DateTime.UtcNow;
        }

        try
        {
            await _requestCaseRepository.UpsertAsync(entity, cancellationToken);
            return ResultModel<RequestCaseDto>.Success(ToDto(entity));
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            return ResultModel<RequestCaseDto>.Failure("保存失败：当前目录下已存在同名接口或用例，请调整名称后重试。", "request_case_unique_conflict");
        }
        catch (Exception exception)
        {
            return ResultModel<RequestCaseDto>.Failure($"保存失败：{exception.Message}", "request_case_save_failed");
        }
    }

    public async Task SyncImportedHttpInterfacesAsync(string projectId, IReadOnlyList<ApiEndpointDto> endpoints, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return;
        }

        var existingCases = await GetCasesAsync(projectId, cancellationToken);
        var importedInterfaces = existingCases
            .Where(item => string.Equals(item.EntryType, "http-interface", StringComparison.OrdinalIgnoreCase))
            .Where(IsImportedInterface)
            .ToDictionary(item => item.RequestSnapshot.EndpointId, StringComparer.OrdinalIgnoreCase);
        var importedInterfaceIds = importedInterfaces.Values
            .Select(item => item.Id)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var importedCases = existingCases
            .Where(item => string.Equals(item.EntryType, "http-case", StringComparison.OrdinalIgnoreCase))
            .Where(item => importedInterfaceIds.Contains(item.ParentId))
            .ToList();
        var importedCasesByInterfaceId = importedCases
            .GroupBy(item => item.ParentId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var normalizedEndpoints = endpoints
            .GroupBy(BuildImportedEndpointKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();
        var targetKeys = normalizedEndpoints
            .Select(BuildImportedEndpointKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var removedInterface in importedInterfaces.Values.Where(item => !targetKeys.Contains(item.RequestSnapshot.EndpointId)).ToList())
        {
            if (importedCasesByInterfaceId.ContainsKey(removedInterface.Id))
            {
                await PreserveImportedInterfaceAsync(removedInterface, cancellationToken);
                continue;
            }

            await DeleteAsync(projectId, removedInterface.Id, cancellationToken);
        }

        foreach (var endpoint in normalizedEndpoints)
        {
            var endpointKey = BuildImportedEndpointKey(endpoint);
            importedInterfaces.TryGetValue(endpointKey, out var existingInterface);

            var snapshot = BuildImportedSnapshot(endpoint, endpointKey);
            var saveResult = await SaveAsync(new RequestCaseDto
            {
                Id = existingInterface?.Id ?? string.Empty,
                ProjectId = projectId,
                EntryType = "http-interface",
                Name = endpoint.Name,
                GroupName = "接口",
                FolderPath = NormalizeFolderPath(endpoint.GroupName),
                ParentId = endpointKey,
                Description = endpoint.Description,
                RequestSnapshot = snapshot,
                UpdatedAt = DateTime.UtcNow
            }, cancellationToken);

            if (!saveResult.IsSuccess)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(saveResult.Message)
                    ? "导入接口同步失败。"
                    : saveResult.Message);
            }
        }
    }

    public async Task<IResultModel<RequestCaseDto>> DuplicateAsync(string projectId, string id, CancellationToken cancellationToken)
    {
        var source = await _requestCaseRepository.GetByIdAsync(projectId, id, cancellationToken);
        if (source is null)
        {
            return ResultModel<RequestCaseDto>.Failure("未找到待复制的用例。", "request_case_not_found");
        }

        var duplicate = ToDto(source);
        var duplicateEntity = ToEntity(new RequestCaseDto
        {
            Id = Guid.NewGuid().ToString("N"),
            ProjectId = duplicate.ProjectId,
            EntryType = duplicate.EntryType,
            Name = $"{duplicate.Name} 副本",
            GroupName = duplicate.GroupName,
            FolderPath = duplicate.FolderPath,
            ParentId = duplicate.ParentId,
            Tags = duplicate.Tags,
            Description = duplicate.Description,
            RequestSnapshot = duplicate.RequestSnapshot,
            UpdatedAt = DateTime.UtcNow
        });

        await _requestCaseRepository.UpsertAsync(duplicateEntity, cancellationToken);
        return ResultModel<RequestCaseDto>.Success(ToDto(duplicateEntity));
    }

    public async Task<IResultModel<bool>> DeleteAsync(string projectId, string id, CancellationToken cancellationToken)
    {
        await _requestCaseRepository.DeleteAsync(projectId, id, cancellationToken);
        return ResultModel<bool>.Success(true);
    }

    private static bool IsImportedInterface(RequestCaseDto requestCase)
    {
        return requestCase.RequestSnapshot.EndpointId.StartsWith(ImportedEndpointKeyPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildImportedEndpointKey(ApiEndpointDto endpoint)
    {
        var method = string.IsNullOrWhiteSpace(endpoint.Method) ? "GET" : endpoint.Method.Trim().ToUpperInvariant();
        var path = string.IsNullOrWhiteSpace(endpoint.Path) ? "/" : endpoint.Path.Trim();
        return $"{ImportedEndpointKeyPrefix}{method} {path}";
    }

    private static string BuildImportedEndpointKey(string interfaceId, IReadOnlyDictionary<string, RequestCaseDto> importedInterfaces)
    {
        var requestCase = importedInterfaces.Values.FirstOrDefault(item => string.Equals(item.Id, interfaceId, StringComparison.OrdinalIgnoreCase));
        return requestCase?.RequestSnapshot.EndpointId ?? string.Empty;
    }

    private async Task PreserveImportedInterfaceAsync(RequestCaseDto requestCase, CancellationToken cancellationToken)
    {
        var saveResult = await SaveAsync(new RequestCaseDto
        {
            Id = requestCase.Id,
            ProjectId = requestCase.ProjectId,
            EntryType = requestCase.EntryType,
            Name = requestCase.Name,
            GroupName = requestCase.GroupName,
            FolderPath = requestCase.FolderPath,
            ParentId = BuildPreservedInterfaceParentId(requestCase.Id),
            Tags = requestCase.Tags.ToList(),
            Description = requestCase.Description,
            RequestSnapshot = BuildDetachedSnapshot(requestCase.RequestSnapshot),
            UpdatedAt = DateTime.UtcNow
        }, cancellationToken);

        if (!saveResult.IsSuccess)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(saveResult.Message)
                ? "保留已保存用例关联的接口失败。"
                : saveResult.Message);
        }
    }

    private static string BuildPreservedInterfaceParentId(string requestCaseId)
    {
        return $"{PreservedInterfaceParentIdPrefix}{requestCaseId}";
    }

    private static RequestSnapshotDto BuildDetachedSnapshot(RequestSnapshotDto snapshot)
    {
        return new RequestSnapshotDto
        {
            EndpointId = string.Empty,
            Name = snapshot.Name,
            Method = snapshot.Method,
            Url = snapshot.Url,
            Description = snapshot.Description,
            BodyMode = snapshot.BodyMode,
            BodyContent = snapshot.BodyContent,
            IgnoreSslErrors = snapshot.IgnoreSslErrors,
            QueryParameters = snapshot.QueryParameters
                .Select(item => new RequestKeyValueDto
                {
                    Name = item.Name,
                    Value = item.Value
                })
                .ToList(),
            PathParameters = snapshot.PathParameters
                .Select(item => new RequestKeyValueDto
                {
                    Name = item.Name,
                    Value = item.Value
                })
                .ToList(),
            Headers = snapshot.Headers
                .Select(item => new RequestKeyValueDto
                {
                    Name = item.Name,
                    Value = item.Value
                })
                .ToList()
        };
    }

    private static RequestSnapshotDto BuildImportedSnapshot(ApiEndpointDto endpoint, string endpointKey)
    {
        return new RequestSnapshotDto
        {
            EndpointId = endpointKey,
            Name = endpoint.Name,
            Method = string.IsNullOrWhiteSpace(endpoint.Method) ? "GET" : endpoint.Method.Trim().ToUpperInvariant(),
            Url = endpoint.Path,
            Description = endpoint.Description,
            BodyMode = string.IsNullOrWhiteSpace(endpoint.RequestBodyTemplate) ? BodyModes.None : BodyModes.RawJson,
            BodyContent = endpoint.RequestBodyTemplate,
            Headers = endpoint.Parameters
                .Where(item => item.ParameterType == RequestParameterKind.Header)
                .Select(ToKeyValue)
                .ToList(),
            QueryParameters = endpoint.Parameters
                .Where(item => item.ParameterType == RequestParameterKind.Query)
                .Select(ToKeyValue)
                .ToList(),
            PathParameters = endpoint.Parameters
                .Where(item => item.ParameterType == RequestParameterKind.Path)
                .Select(ToKeyValue)
                .ToList()
        };
    }

    private static string NormalizeFolderPath(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return "默认模块";
        }

        return string.Join('/',
            folderPath.Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static RequestKeyValueDto ToKeyValue(RequestParameterDto parameter)
    {
        return new RequestKeyValueDto
        {
            Name = parameter.Name,
            Value = parameter.DefaultValue
        };
    }

    private RequestCaseDto ToDto(RequestCaseEntity entity)
    {
        return new RequestCaseDto
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            EntryType = entity.EntryType,
            Name = entity.Name,
            GroupName = entity.GroupName,
            FolderPath = entity.FolderPath,
            ParentId = entity.ParentId,
            Tags = _serializer.ToObject<List<string>>(entity.TagsJson) ?? [],
            Description = entity.Description,
            RequestSnapshot = _serializer.ToObject<RequestSnapshotDto>(entity.RequestSnapshotJson) ?? new RequestSnapshotDto(),
            UpdatedAt = entity.UpdatedAt
        };
    }

    private RequestCaseEntity ToEntity(RequestCaseDto dto)
    {
        return new RequestCaseEntity
        {
            Id = dto.Id,
            ProjectId = dto.ProjectId,
            EntryType = dto.EntryType,
            Name = dto.Name,
            GroupName = dto.GroupName,
            FolderPath = dto.FolderPath,
            ParentId = dto.ParentId,
            TagsJson = _serializer.ToJson(dto.Tags),
            Description = dto.Description,
            RequestSnapshotJson = _serializer.ToJson(dto.RequestSnapshot),
            UpdatedAt = dto.UpdatedAt
        };
    }
}
