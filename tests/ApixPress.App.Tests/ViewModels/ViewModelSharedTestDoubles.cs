using System.Collections.Generic;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.Results;

namespace ApixPress.App.Tests.ViewModels;

public static class ViewModelSharedTestDoubles
{
    public sealed class FakeEnvironmentVariableService : IEnvironmentVariableService
    {
        public Task<IReadOnlyList<ProjectEnvironmentDto>> GetEnvironmentsAsync(string projectId, CancellationToken cancellationToken)
        {
            IReadOnlyList<ProjectEnvironmentDto> environments =
            [
                new ProjectEnvironmentDto
                {
                    Id = "env-1",
                    ProjectId = projectId,
                    Name = "开发",
                    BaseUrl = "https://api.demo.local",
                    IsActive = true,
                    SortOrder = 1
                }
            ];
            return Task.FromResult(environments);
        }

        public Task<IResultModel<ProjectEnvironmentDto>> SaveEnvironmentAsync(ProjectEnvironmentDto environment, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<ProjectEnvironmentDto>>(ResultModel<ProjectEnvironmentDto>.Success(environment));
        }

        public Task<IResultModel<ProjectEnvironmentDto>> SetActiveEnvironmentAsync(string projectId, string environmentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<ProjectEnvironmentDto>>(ResultModel<ProjectEnvironmentDto>.Success(new ProjectEnvironmentDto
            {
                Id = environmentId,
                ProjectId = projectId,
                Name = "开发",
                BaseUrl = "https://api.demo.local",
                IsActive = true,
                SortOrder = 1
            }));
        }

        public Task<IResultModel<bool>> DeleteEnvironmentAsync(string projectId, string environmentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<bool>>(ResultModel<bool>.Success(true));
        }

        public Task<IReadOnlyList<EnvironmentVariableDto>> GetVariablesAsync(string environmentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<EnvironmentVariableDto>>([]);
        }

        public Task<IResultModel<EnvironmentVariableDto>> SaveVariableAsync(EnvironmentVariableDto variable, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<EnvironmentVariableDto>>(ResultModel<EnvironmentVariableDto>.Success(variable));
        }

        public Task<IResultModel<bool>> DeleteVariableAsync(string id, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<bool>>(ResultModel<bool>.Success(true));
        }

        public Task<IReadOnlyDictionary<string, string>> GetActiveDictionaryAsync(string environmentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        }
    }

    public sealed class FakeRequestCaseService : IRequestCaseService
    {
        private const string ImportedEndpointKeyPrefix = "swagger-import:";

        public List<RequestCaseDto> Cases { get; } = [];

        public Task<IReadOnlyList<RequestCaseDto>> GetCasesAsync(string projectId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<RequestCaseDto>>(Cases
                .Where(item => string.Equals(item.ProjectId, projectId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.EntryType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.FolderPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }

        public Task<IResultModel<RequestCaseDto>> SaveAsync(RequestCaseDto requestCase, CancellationToken cancellationToken)
        {
            var saved = new RequestCaseDto
            {
                Id = requestCase.Id,
                ProjectId = requestCase.ProjectId,
                EntryType = requestCase.EntryType,
                Name = requestCase.Name,
                GroupName = requestCase.GroupName,
                FolderPath = requestCase.FolderPath,
                ParentId = requestCase.ParentId,
                Tags = requestCase.Tags.ToList(),
                Description = requestCase.Description,
                RequestSnapshot = new RequestSnapshotDto
                {
                    EndpointId = requestCase.RequestSnapshot.EndpointId,
                    Name = requestCase.RequestSnapshot.Name,
                    Method = requestCase.RequestSnapshot.Method,
                    Url = requestCase.RequestSnapshot.Url,
                    Description = requestCase.RequestSnapshot.Description,
                    BodyMode = requestCase.RequestSnapshot.BodyMode,
                    BodyContent = requestCase.RequestSnapshot.BodyContent,
                    IgnoreSslErrors = requestCase.RequestSnapshot.IgnoreSslErrors,
                    QueryParameters = requestCase.RequestSnapshot.QueryParameters.ToList(),
                    PathParameters = requestCase.RequestSnapshot.PathParameters.ToList(),
                    Headers = requestCase.RequestSnapshot.Headers.ToList()
                },
                UpdatedAt = requestCase.UpdatedAt
            };

            if (string.IsNullOrWhiteSpace(saved.Id))
            {
                saved = new RequestCaseDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ProjectId = saved.ProjectId,
                    EntryType = saved.EntryType,
                    Name = saved.Name,
                    GroupName = saved.GroupName,
                    FolderPath = saved.FolderPath,
                    ParentId = saved.ParentId,
                    Tags = saved.Tags.ToList(),
                    Description = saved.Description,
                    RequestSnapshot = saved.RequestSnapshot,
                    UpdatedAt = saved.UpdatedAt
                };
            }

            Cases.RemoveAll(item => string.Equals(item.Id, saved.Id, StringComparison.OrdinalIgnoreCase));
            Cases.Add(saved);
            return Task.FromResult<IResultModel<RequestCaseDto>>(ResultModel<RequestCaseDto>.Success(saved));
        }

        public async Task SyncImportedHttpInterfacesAsync(string projectId, IReadOnlyList<ApiEndpointDto> endpoints, CancellationToken cancellationToken)
        {
            var existingImported = Cases
                .Where(item => string.Equals(item.ProjectId, projectId, StringComparison.OrdinalIgnoreCase))
                .Where(item => string.Equals(item.EntryType, "http-interface", StringComparison.OrdinalIgnoreCase))
                .Where(item => item.RequestSnapshot.EndpointId.StartsWith(ImportedEndpointKeyPrefix, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(item => item.RequestSnapshot.EndpointId, StringComparer.OrdinalIgnoreCase);
            var targetKeys = endpoints
                .Select(BuildImportedEndpointKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Cases.RemoveAll(item =>
                string.Equals(item.ProjectId, projectId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.EntryType, "http-interface", StringComparison.OrdinalIgnoreCase)
                && item.RequestSnapshot.EndpointId.StartsWith(ImportedEndpointKeyPrefix, StringComparison.OrdinalIgnoreCase)
                && !targetKeys.Contains(item.RequestSnapshot.EndpointId));

            foreach (var endpoint in endpoints)
            {
                var key = BuildImportedEndpointKey(endpoint);
                existingImported.TryGetValue(key, out var existing);
                await SaveAsync(new RequestCaseDto
                {
                    Id = existing?.Id ?? string.Empty,
                    ProjectId = projectId,
                    EntryType = "http-interface",
                    Name = endpoint.Name,
                    GroupName = "接口",
                    FolderPath = endpoint.GroupName,
                    Description = endpoint.Description,
                    RequestSnapshot = new RequestSnapshotDto
                    {
                        EndpointId = key,
                        Name = endpoint.Name,
                        Method = endpoint.Method,
                        Url = endpoint.Path,
                        Description = endpoint.Description
                    },
                    UpdatedAt = DateTime.UtcNow
                }, cancellationToken);
            }
        }

        public Task<IResultModel<RequestCaseDto>> DuplicateAsync(string projectId, string id, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<RequestCaseDto>>(ResultModel<RequestCaseDto>.Failure("未实现"));
        }

        public Task<IResultModel<bool>> DeleteAsync(string projectId, string id, CancellationToken cancellationToken)
        {
            Cases.RemoveAll(item => string.Equals(item.ProjectId, projectId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult<IResultModel<bool>>(ResultModel<bool>.Success(true));
        }

        private static string BuildImportedEndpointKey(ApiEndpointDto endpoint)
        {
            return $"{ImportedEndpointKeyPrefix}{endpoint.Method.ToUpperInvariant()} {endpoint.Path}";
        }
    }

    public sealed class FakeRequestExecutionService : IRequestExecutionService
    {
        public Task<IResultModel<ResponseSnapshotDto>> SendAsync(RequestSnapshotDto request, ProjectEnvironmentDto environment, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<ResponseSnapshotDto>>(ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto()));
        }
    }

    public sealed class FakeRequestHistoryService : IRequestHistoryService
    {
        public Task<IReadOnlyList<RequestHistoryItemDto>> GetHistoryAsync(string projectId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<RequestHistoryItemDto>>([]);
        }

        public Task<IResultModel<RequestHistoryItemDto>> AddAsync(string projectId, RequestSnapshotDto request, ResponseSnapshotDto? response, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<RequestHistoryItemDto>>(ResultModel<RequestHistoryItemDto>.Success(new RequestHistoryItemDto()));
        }

        public Task<IResultModel<bool>> ClearAsync(string projectId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<bool>>(ResultModel<bool>.Success(true));
        }

        public Task<IResultModel<bool>> DeleteAsync(string projectId, string id, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<bool>>(ResultModel<bool>.Success(true));
        }
    }

    public sealed class FakeFilePickerService : IFilePickerService
    {
        public Task<string?> PickSwaggerJsonFileAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(null);
        }
    }
}
