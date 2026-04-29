using System.Collections.Generic;
using Avalonia.Controls.Notifications;
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

        public Task<IResultModel<IReadOnlyList<EnvironmentVariableDto>>> SaveVariablesAsync(
            ProjectEnvironmentDto environment,
            IReadOnlyList<EnvironmentVariableDto> variables,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<IReadOnlyList<EnvironmentVariableDto>>>(
                ResultModel<IReadOnlyList<EnvironmentVariableDto>>.Success(variables));
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
        public TaskCompletionSource<bool>? DetailLoadGate { get; set; }
        public int GetCasesCallCount { get; private set; }
        public int GetDetailCallCount { get; private set; }

        public Task<IReadOnlyList<RequestCaseDto>> GetCasesAsync(string projectId, CancellationToken cancellationToken)
        {
            GetCasesCallCount++;
            return Task.FromResult<IReadOnlyList<RequestCaseDto>>(Cases
                .Where(item => string.Equals(item.ProjectId, projectId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.EntryType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.FolderPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(item => new RequestCaseDto
                {
                    Id = item.Id,
                    ProjectId = item.ProjectId,
                    EntryType = item.EntryType,
                    Name = item.Name,
                    GroupName = item.GroupName,
                    FolderPath = item.FolderPath,
                    ParentId = item.ParentId,
                    Tags = item.Tags.ToList(),
                    Description = item.Description,
                    RequestSnapshot = new RequestSnapshotDto
                    {
                        EndpointId = item.RequestSnapshot.EndpointId,
                        Method = item.RequestSnapshot.Method,
                        Url = item.RequestSnapshot.Url
                    },
                    HasLoadedDetail = false,
                    UpdatedAt = item.UpdatedAt
                })
                .ToList());
        }

        public Task<IReadOnlyList<RequestCaseDto>> GetCaseDetailsAsync(string projectId, CancellationToken cancellationToken)
        {
            IReadOnlyList<RequestCaseDto> cases = Cases
                .Where(item => string.Equals(item.ProjectId, projectId, StringComparison.OrdinalIgnoreCase))
                .Select(CloneCase)
                .ToList();
            return Task.FromResult(cases);
        }

        public async Task<RequestCaseDto?> GetDetailAsync(string projectId, string id, CancellationToken cancellationToken)
        {
            GetDetailCallCount++;
            if (DetailLoadGate is not null)
            {
                await DetailLoadGate.Task.WaitAsync(cancellationToken);
            }

            return Cases.FirstOrDefault(item =>
                string.Equals(item.ProjectId, projectId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
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
                HasLoadedDetail = true,
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
                    HasLoadedDetail = true,
                    UpdatedAt = saved.UpdatedAt
                };
            }

            Cases.RemoveAll(item => string.Equals(item.Id, saved.Id, StringComparison.OrdinalIgnoreCase));
            Cases.Add(saved);
            return Task.FromResult<IResultModel<RequestCaseDto>>(ResultModel<RequestCaseDto>.Success(saved));
        }

        public async Task<ImportedHttpInterfaceSyncResultDto> SyncImportedHttpInterfacesAsync(string projectId, IReadOnlyList<ApiEndpointDto> endpoints, CancellationToken cancellationToken)
        {
            var existingImported = Cases
                .Where(item => string.Equals(item.ProjectId, projectId, StringComparison.OrdinalIgnoreCase))
                .Where(item => string.Equals(item.EntryType, "http-interface", StringComparison.OrdinalIgnoreCase))
                .Where(item => item.RequestSnapshot.EndpointId.StartsWith(ImportedEndpointKeyPrefix, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(item => item.RequestSnapshot.EndpointId, StringComparer.OrdinalIgnoreCase);
            var targetKeys = endpoints
                .Select(BuildImportedEndpointKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var deletedIds = Cases
                .Where(item =>
                string.Equals(item.ProjectId, projectId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.EntryType, "http-interface", StringComparison.OrdinalIgnoreCase)
                && item.RequestSnapshot.EndpointId.StartsWith(ImportedEndpointKeyPrefix, StringComparison.OrdinalIgnoreCase)
                && !targetKeys.Contains(item.RequestSnapshot.EndpointId))
                .Select(item => item.Id)
                .ToList();
            var deletedIdLookup = deletedIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            Cases.RemoveAll(item => deletedIdLookup.Contains(item.Id));

            var upsertedCases = new List<RequestCaseDto>();

            foreach (var endpoint in endpoints)
            {
                var key = BuildImportedEndpointKey(endpoint);
                existingImported.TryGetValue(key, out var existing);
                var saveResult = await SaveAsync(new RequestCaseDto
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

                if (saveResult.Data is not null)
                {
                    upsertedCases.Add(saveResult.Data);
                }
            }

            return new ImportedHttpInterfaceSyncResultDto
            {
                UpsertedCases = upsertedCases,
                DeletedCaseIds = deletedIds
            };
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

        public Task DeleteRangeAsync(string projectId, IReadOnlyList<string> ids, CancellationToken cancellationToken)
        {
            var targetIds = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
            Cases.RemoveAll(item =>
                string.Equals(item.ProjectId, projectId, StringComparison.OrdinalIgnoreCase)
                && targetIds.Contains(item.Id));
            return Task.CompletedTask;
        }

        private static string BuildImportedEndpointKey(ApiEndpointDto endpoint)
        {
            return $"{ImportedEndpointKeyPrefix}{endpoint.Method.ToUpperInvariant()} {endpoint.Path}";
        }

        private static RequestCaseDto CloneCase(RequestCaseDto requestCase)
        {
            return new RequestCaseDto
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
                    QueryParameters = requestCase.RequestSnapshot.QueryParameters
                        .Select(item => new RequestKeyValueDto
                        {
                            Name = item.Name,
                            Value = item.Value,
                            IsEnabled = item.IsEnabled
                        })
                        .ToList(),
                    PathParameters = requestCase.RequestSnapshot.PathParameters
                        .Select(item => new RequestKeyValueDto
                        {
                            Name = item.Name,
                            Value = item.Value,
                            IsEnabled = item.IsEnabled
                        })
                        .ToList(),
                    Headers = requestCase.RequestSnapshot.Headers
                        .Select(item => new RequestKeyValueDto
                        {
                            Name = item.Name,
                            Value = item.Value,
                            IsEnabled = item.IsEnabled
                        })
                        .ToList()
                },
                HasLoadedDetail = requestCase.HasLoadedDetail,
                UpdatedAt = requestCase.UpdatedAt
            };
        }
    }

    public sealed class FakeRequestExecutionService : IRequestExecutionService
    {
        public int SendCallCount { get; private set; }

        public RequestSnapshotDto? LastRequest { get; private set; }

        public TaskCompletionSource<IResultModel<ResponseSnapshotDto>>? PendingSendResult { get; set; }

        public Task<IResultModel<ResponseSnapshotDto>> SendAsync(RequestSnapshotDto request, ProjectEnvironmentDto environment, CancellationToken cancellationToken)
        {
            SendCallCount++;
            LastRequest = request;
            if (PendingSendResult is not null)
            {
                return PendingSendResult.Task.WaitAsync(cancellationToken);
            }

            return Task.FromResult<IResultModel<ResponseSnapshotDto>>(ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto()));
        }
    }

    public sealed class FakeRequestHistoryService : IRequestHistoryService
    {
        public List<RequestHistoryItemDto> Items { get; } = [];
        public int GetHistoryCallCount { get; private set; }
        public int GetDetailCallCount { get; private set; }

        public Task<IReadOnlyList<RequestHistoryItemDto>> GetHistoryAsync(string projectId, CancellationToken cancellationToken)
        {
            GetHistoryCallCount++;
            return Task.FromResult<IReadOnlyList<RequestHistoryItemDto>>(Items.Select(item => new RequestHistoryItemDto
            {
                Id = item.Id,
                Timestamp = item.Timestamp,
                HasResponse = item.HasResponse,
                StatusCode = item.StatusCode,
                DurationMs = item.DurationMs,
                SizeBytes = item.SizeBytes,
                RequestSnapshot = item.RequestSnapshot,
                ResponseSnapshot = null
            }).ToList());
        }

        public Task<RequestHistoryItemDto?> GetDetailAsync(string projectId, string id, CancellationToken cancellationToken)
        {
            GetDetailCallCount++;
            return Task.FromResult<RequestHistoryItemDto?>(Items.FirstOrDefault(item =>
                string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<IResultModel<RequestHistoryItemDto>> AddAsync(string projectId, RequestSnapshotDto request, ResponseSnapshotDto? response, CancellationToken cancellationToken)
        {
            var item = new RequestHistoryItemDto
            {
                Id = Guid.NewGuid().ToString("N"),
                Timestamp = DateTime.UtcNow,
                HasResponse = response is not null,
                StatusCode = response?.StatusCode,
                DurationMs = response?.DurationMs ?? 0,
                SizeBytes = response?.SizeBytes ?? 0,
                RequestSnapshot = request,
                ResponseSnapshot = response
            };
            Items.Insert(0, item);
            return Task.FromResult<IResultModel<RequestHistoryItemDto>>(ResultModel<RequestHistoryItemDto>.Success(item));
        }

        public Task<IResultModel<bool>> ClearAsync(string projectId, CancellationToken cancellationToken)
        {
            Items.Clear();
            return Task.FromResult<IResultModel<bool>>(ResultModel<bool>.Success(true));
        }

        public Task<IResultModel<bool>> ClearAllAsync(CancellationToken cancellationToken)
        {
            Items.Clear();
            return Task.FromResult<IResultModel<bool>>(ResultModel<bool>.Success(true));
        }

        public Task<IResultModel<bool>> DeleteAsync(string projectId, string id, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<bool>>(ResultModel<bool>.Success(true));
        }
    }

    public sealed class FakeSystemDataService : ISystemDataService
    {
        public int ClearAllCallCount { get; private set; }

        public int ClearProjectCallCount { get; private set; }

        public string LastClearedProjectId { get; private set; } = string.Empty;

        public bool ShouldSucceed { get; set; } = true;

        public Task<IResultModel<bool>> ClearProjectAsync(string projectId, CancellationToken cancellationToken)
        {
            ClearProjectCallCount++;
            LastClearedProjectId = projectId;
            return Task.FromResult<IResultModel<bool>>(ShouldSucceed
                ? ResultModel<bool>.Success(true)
                : ResultModel<bool>.Failure("清空失败"));
        }

        public Task<IResultModel<bool>> ClearAllAsync(CancellationToken cancellationToken)
        {
            ClearAllCallCount++;
            return Task.FromResult<IResultModel<bool>>(ShouldSucceed
                ? ResultModel<bool>.Success(true)
                : ResultModel<bool>.Failure("清空失败"));
        }
    }

    public sealed class FakeProjectWorkspaceService : IProjectWorkspaceService
    {
        public int DeleteCallCount { get; private set; }

        public string LastDeletedProjectId { get; private set; } = string.Empty;

        public bool DeleteShouldSucceed { get; set; } = true;

        public Task<IReadOnlyList<ProjectWorkspaceDto>> GetProjectsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ProjectWorkspaceDto>>([]);
        }

        public Task<ProjectWorkspaceDto?> GetStartupProjectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<ProjectWorkspaceDto?>(null);
        }

        public Task<IResultModel<ProjectWorkspaceDto>> SaveAsync(ProjectWorkspaceDto project, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<ProjectWorkspaceDto>>(ResultModel<ProjectWorkspaceDto>.Success(project));
        }

        public Task<IResultModel<ProjectWorkspaceDto>> SetDefaultAsync(string projectId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<ProjectWorkspaceDto>>(ResultModel<ProjectWorkspaceDto>.Failure("未实现"));
        }

        public Task<IResultModel<bool>> DeleteAsync(string projectId, CancellationToken cancellationToken)
        {
            DeleteCallCount++;
            LastDeletedProjectId = projectId;
            return Task.FromResult<IResultModel<bool>>(DeleteShouldSucceed
                ? ResultModel<bool>.Success(true)
                : ResultModel<bool>.Failure("删除失败"));
        }
    }

    public sealed class FakeApplicationRestartService : IApplicationRestartService
    {
        public int RestartCallCount { get; private set; }

        public bool ShouldSucceed { get; set; } = true;

        public Task<IResultModel<bool>> RestartAsync(CancellationToken cancellationToken)
        {
            RestartCallCount++;
            return Task.FromResult<IResultModel<bool>>(ShouldSucceed
                ? ResultModel<bool>.Success(true)
                : ResultModel<bool>.Failure("重启失败"));
        }
    }

    public sealed class FakeFilePickerService : IFilePickerService
    {
        public string? PickProjectDataPackageFileResult { get; set; }
        public string? SaveProjectDataExportFileResult { get; set; }

        public Task<string?> PickSwaggerJsonFileAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> PickProjectDataPackageFileAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(PickProjectDataPackageFileResult);
        }

        public Task<string?> SaveProjectDataExportFileAsync(string suggestedFileName, CancellationToken cancellationToken)
        {
            return Task.FromResult(SaveProjectDataExportFileResult);
        }

        public Task<string?> PickStorageDirectoryAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(null);
        }
    }

    public sealed class FakeProjectDataExportService : IProjectDataExportService
    {
        public ProjectDataExportRequestDto? LastRequest { get; private set; }

        public IResultModel<ProjectDataExportResultDto> Result { get; set; } = ResultModel<ProjectDataExportResultDto>.Success(new ProjectDataExportResultDto
        {
            FilePath = @"C:\temp\project-data.apixpkg.json",
            InterfaceCount = 0,
            TestCaseCount = 0
        });

        public Task<IResultModel<ProjectDataExportResultDto>> ExportAsync(ProjectDataExportRequestDto request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }

        public IResultModel<ApiImportPreviewDto> PreviewImportResult { get; set; } = ResultModel<ApiImportPreviewDto>.Success(new ApiImportPreviewDto
        {
            DocumentName = "导入项目",
            SourceType = "APIXPKG",
            SourceValue = @"C:\temp\project-data.apixpkg.json",
            TotalEndpointCount = 1,
            NewEndpointCount = 1,
            ConflictCount = 0
        });

        public IResultModel<ApiDocumentDto> ImportResult { get; set; } = ResultModel<ApiDocumentDto>.Success(new ApiDocumentDto
        {
            Id = "doc-1",
            ProjectId = "project-1",
            Name = "导入项目",
            SourceType = "APIXPKG",
            SourceValue = @"C:\temp\project-data.apixpkg.json",
            ImportedAt = DateTime.UtcNow
        });

        public string? LastImportProjectId { get; private set; }

        public string? LastImportFilePath { get; private set; }

        public Task<IResultModel<ApiImportPreviewDto>> PreviewImportPackageAsync(string projectId, string filePath, CancellationToken cancellationToken)
        {
            LastImportProjectId = projectId;
            LastImportFilePath = filePath;
            return Task.FromResult(PreviewImportResult);
        }

        public Task<IResultModel<ApiDocumentDto>> ImportPackageAsync(string projectId, string filePath, CancellationToken cancellationToken)
        {
            LastImportProjectId = projectId;
            LastImportFilePath = filePath;
            return Task.FromResult(ImportResult);
        }
    }

    public sealed class FakeAppNotificationService : IAppNotificationService
    {
        public List<(string Title, string Content, NotificationType Type)> Notifications { get; } = [];

        public void Show(string title, string content, NotificationType type = NotificationType.Information, TimeSpan? expiration = null)
        {
            Notifications.Add((title, content, type));
        }

        public void ShowSuccess(string title, string content, TimeSpan? expiration = null)
        {
            Show(title, content, NotificationType.Success, expiration);
        }

        public void ShowError(string title, string content, TimeSpan? expiration = null)
        {
            Show(title, content, NotificationType.Error, expiration);
        }
    }
}
