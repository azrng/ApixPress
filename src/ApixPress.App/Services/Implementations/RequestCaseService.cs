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
        return cases.Select(ToSummaryDto).ToList();
    }

    public async Task<IReadOnlyList<RequestCaseDto>> GetCaseDetailsAsync(string projectId, CancellationToken cancellationToken)
    {
        var cases = await _requestCaseRepository.GetCasesAsync(projectId, cancellationToken);
        return cases.Select(ToDto).ToList();
    }

    public async Task<RequestCaseDto?> GetDetailAsync(string projectId, string id, CancellationToken cancellationToken)
    {
        var entity = await _requestCaseRepository.GetByIdAsync(projectId, id, cancellationToken);
        return entity is null ? null : ToDto(entity);
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

    public async Task<ImportedHttpInterfaceSyncResultDto> SyncImportedHttpInterfacesAsync(string projectId, IReadOnlyList<ApiEndpointDto> endpoints, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return new ImportedHttpInterfaceSyncResultDto();
        }

        var existingCases = await _requestCaseRepository.GetCasesAsync(projectId, cancellationToken);
        var importedInterfaces = existingCases
            .Where(item => string.Equals(item.EntryType, "http-interface", StringComparison.OrdinalIgnoreCase))
            .Select(item => new ImportedInterfaceRecord(item, DeserializeRequestSnapshot(item.RequestSnapshotJson)))
            .Where(item => IsImportedInterface(item.Snapshot))
            .ToDictionary(item => item.Snapshot.EndpointId, StringComparer.OrdinalIgnoreCase);
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
        var deletedInterfaceIds = new List<string>();
        var entitiesToUpsert = new List<RequestCaseEntity>();

        foreach (var removedInterface in importedInterfaces.Values.Where(item => !targetKeys.Contains(item.Snapshot.EndpointId)))
        {
            if (importedCasesByInterfaceId.ContainsKey(removedInterface.Id))
            {
                entitiesToUpsert.Add(BuildPreservedImportedInterfaceEntity(removedInterface));
                continue;
            }

            deletedInterfaceIds.Add(removedInterface.Id);
        }

        try
        {
            if (deletedInterfaceIds.Count > 0)
            {
                await _requestCaseRepository.DeleteRangeAsync(projectId, deletedInterfaceIds, cancellationToken);
            }

            foreach (var endpoint in normalizedEndpoints)
            {
                var endpointKey = BuildImportedEndpointKey(endpoint);
                importedInterfaces.TryGetValue(endpointKey, out var existingInterface);

                entitiesToUpsert.Add(BuildImportedInterfaceEntity(projectId, endpoint, endpointKey, existingInterface?.Id));
            }

            if (entitiesToUpsert.Count > 0)
            {
                await _requestCaseRepository.UpsertRangeAsync(entitiesToUpsert, cancellationToken);
            }

            return new ImportedHttpInterfaceSyncResultDto
            {
                UpsertedCases = entitiesToUpsert.Select(ToDto).ToList(),
                DeletedCaseIds = deletedInterfaceIds
            };
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException("导入接口同步失败：当前目录下已存在同名接口或用例，请调整名称后重试。", exception);
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

    public async Task DeleteRangeAsync(string projectId, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        await _requestCaseRepository.DeleteRangeAsync(projectId, ids, cancellationToken);
    }

    private static bool IsImportedInterface(RequestSnapshotDto requestSnapshot)
    {
        return requestSnapshot.EndpointId.StartsWith(ImportedEndpointKeyPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildImportedEndpointKey(ApiEndpointDto endpoint)
    {
        var method = string.IsNullOrWhiteSpace(endpoint.Method) ? "GET" : endpoint.Method.Trim().ToUpperInvariant();
        var path = string.IsNullOrWhiteSpace(endpoint.Path) ? "/" : endpoint.Path.Trim();
        return $"{ImportedEndpointKeyPrefix}{method} {path}";
    }

    private RequestCaseEntity BuildPreservedImportedInterfaceEntity(ImportedInterfaceRecord requestCase)
    {
        return new RequestCaseEntity
        {
            Id = requestCase.Id,
            ProjectId = requestCase.ProjectId,
            EntryType = requestCase.EntryType,
            Name = requestCase.Name,
            GroupName = requestCase.GroupName,
            FolderPath = requestCase.FolderPath,
            ParentId = BuildPreservedInterfaceParentId(requestCase.Id),
            TagsJson = requestCase.TagsJson,
            Description = requestCase.Description,
            RequestSnapshotJson = _serializer.ToJson(BuildDetachedSnapshot(requestCase.Snapshot)),
            UpdatedAt = DateTime.UtcNow
        };
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
            BodyMode = ResolveImportedBodyMode(endpoint),
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

    private static string ResolveImportedBodyMode(ApiEndpointDto endpoint)
    {
        if (!string.IsNullOrWhiteSpace(endpoint.RequestBodyMode) && endpoint.RequestBodyMode != BodyModes.None)
        {
            return endpoint.RequestBodyMode;
        }

        return string.IsNullOrWhiteSpace(endpoint.RequestBodyTemplate)
            ? BodyModes.None
            : BodyModes.RawJson;
    }

    private RequestCaseEntity BuildImportedInterfaceEntity(string projectId, ApiEndpointDto endpoint, string endpointKey, string? id)
    {
        return new RequestCaseEntity
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id,
            ProjectId = projectId,
            EntryType = "http-interface",
            Name = endpoint.Name,
            GroupName = "接口",
            FolderPath = NormalizeFolderPath(endpoint.GroupName),
            ParentId = endpointKey,
            TagsJson = "[]",
            Description = endpoint.Description,
            RequestSnapshotJson = _serializer.ToJson(BuildImportedSnapshot(endpoint, endpointKey)),
            UpdatedAt = DateTime.UtcNow
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
            HasLoadedDetail = true,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private RequestCaseDto ToSummaryDto(RequestCaseEntity entity)
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
            RequestSnapshot = new RequestSnapshotDto
            {
                EndpointId = entity.EndpointId,
                Method = entity.Method,
                Url = entity.Url
            },
            HasLoadedDetail = false,
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

    private RequestSnapshotDto DeserializeRequestSnapshot(string requestSnapshotJson)
    {
        return _serializer.ToObject<RequestSnapshotDto>(requestSnapshotJson) ?? new RequestSnapshotDto();
    }

    private sealed class ImportedInterfaceRecord
    {
        public ImportedInterfaceRecord(RequestCaseEntity entity, RequestSnapshotDto snapshot)
        {
            Entity = entity;
            Snapshot = snapshot;
        }

        public RequestCaseEntity Entity { get; }

        public RequestSnapshotDto Snapshot { get; }

        public string Id => Entity.Id;

        public string ProjectId => Entity.ProjectId;

        public string EntryType => Entity.EntryType;

        public string Name => Entity.Name;

        public string GroupName => Entity.GroupName;

        public string FolderPath => Entity.FolderPath;

        public string TagsJson => Entity.TagsJson;

        public string Description => Entity.Description;
    }
}
