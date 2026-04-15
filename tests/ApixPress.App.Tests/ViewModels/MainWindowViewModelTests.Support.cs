using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels;
using Azrng.Core.Results;

namespace ApixPress.App.Tests.ViewModels;

public sealed partial class MainWindowViewModelTests
{
    private static MainWindowViewModel CreateViewModel(
        FakeProjectWorkspaceService? projectWorkspaceService = null,
        FakeAppShellSettingsService? shellSettingsService = null)
    {
        return new MainWindowViewModel(
            new FakeRequestExecutionService(),
            new FakeRequestCaseService(),
            new FakeRequestHistoryService(),
            new FakeEnvironmentVariableService(),
            projectWorkspaceService ?? new FakeProjectWorkspaceService(),
            shellSettingsService ?? new FakeAppShellSettingsService(),
            new FakeApiWorkspaceService(),
            new FakeFilePickerService());
    }

    private sealed class FakeProjectWorkspaceService : IProjectWorkspaceService
    {
        private readonly List<ProjectWorkspaceDto> _projects = [];

        public void SeedProjects(IEnumerable<(string Id, string Name, string Description, bool IsDefault)> projects)
        {
            _projects.Clear();
            _projects.AddRange(projects.Select(project => new ProjectWorkspaceDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                IsDefault = project.IsDefault
            }));
        }

        public void RemoveProject(string projectId)
        {
            _projects.RemoveAll(item => string.Equals(item.Id, projectId, StringComparison.OrdinalIgnoreCase));
        }

        public Task<IReadOnlyList<ProjectWorkspaceDto>> GetProjectsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ProjectWorkspaceDto>>(_projects
                .Select(project => CloneProject(project))
                .ToList());
        }

        public Task<ProjectWorkspaceDto?> GetStartupProjectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<ProjectWorkspaceDto?>(_projects.FirstOrDefault(item => item.IsDefault) is { } project
                ? CloneProject(project)
                : null);
        }

        public Task<IResultModel<ProjectWorkspaceDto>> SaveAsync(ProjectWorkspaceDto project, CancellationToken cancellationToken)
        {
            var saved = new ProjectWorkspaceDto
            {
                Id = string.IsNullOrWhiteSpace(project.Id) ? Guid.NewGuid().ToString("N") : project.Id,
                Name = project.Name,
                Description = project.Description,
                IsDefault = project.IsDefault,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt
            };

            _projects.RemoveAll(item => string.Equals(item.Id, saved.Id, StringComparison.OrdinalIgnoreCase));
            _projects.Add(saved);
            return Task.FromResult<IResultModel<ProjectWorkspaceDto>>(ResultModel<ProjectWorkspaceDto>.Success(CloneProject(saved)));
        }

        public Task<IResultModel<ProjectWorkspaceDto>> SetDefaultAsync(string projectId, CancellationToken cancellationToken)
        {
            var updatedProjects = _projects
                .Select(project => new ProjectWorkspaceDto
                {
                    Id = project.Id,
                    Name = project.Name,
                    Description = project.Description,
                    IsDefault = string.Equals(project.Id, projectId, StringComparison.OrdinalIgnoreCase),
                    CreatedAt = project.CreatedAt,
                    UpdatedAt = project.UpdatedAt
                })
                .ToList();
            _projects.Clear();
            _projects.AddRange(updatedProjects);
            var selected = _projects.FirstOrDefault(item => item.IsDefault);

            return Task.FromResult<IResultModel<ProjectWorkspaceDto>>(selected is null
                ? ResultModel<ProjectWorkspaceDto>.Failure("项目不存在")
                : ResultModel<ProjectWorkspaceDto>.Success(CloneProject(selected)));
        }

        public Task<IResultModel<bool>> DeleteAsync(string projectId, CancellationToken cancellationToken)
        {
            RemoveProject(projectId);
            return Task.FromResult<IResultModel<bool>>(ResultModel<bool>.Success(true));
        }

        private static ProjectWorkspaceDto CloneProject(ProjectWorkspaceDto project)
        {
            return new ProjectWorkspaceDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                IsDefault = project.IsDefault,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt
            };
        }
    }

    private sealed class FakeAppShellSettingsService : IAppShellSettingsService
    {
        public AppShellSettingsDto CurrentSettings { get; set; } = new();

        public Task<IResultModel<AppShellSettingsDto>> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<AppShellSettingsDto>>(ResultModel<AppShellSettingsDto>.Success(CloneSettings(CurrentSettings)));
        }

        public Task<IResultModel<AppShellSettingsDto>> SaveAsync(AppShellSettingsDto settings, CancellationToken cancellationToken)
        {
            CurrentSettings = CloneSettings(settings);
            return Task.FromResult<IResultModel<AppShellSettingsDto>>(ResultModel<AppShellSettingsDto>.Success(CloneSettings(CurrentSettings)));
        }

        private static AppShellSettingsDto CloneSettings(AppShellSettingsDto settings)
        {
            return new AppShellSettingsDto
            {
                RequestTimeoutMilliseconds = settings.RequestTimeoutMilliseconds,
                ValidateSslCertificate = settings.ValidateSslCertificate,
                AutoFollowRedirects = settings.AutoFollowRedirects,
                SendNoCacheHeader = settings.SendNoCacheHeader,
                EnableVerboseLogging = settings.EnableVerboseLogging,
                EnableUpdateReminder = settings.EnableUpdateReminder
            };
        }
    }

    private sealed class FakeEnvironmentVariableService : IEnvironmentVariableService
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

    private sealed class FakeRequestCaseService : IRequestCaseService
    {
        public Task<IReadOnlyList<RequestCaseDto>> GetCasesAsync(string projectId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<RequestCaseDto>>([]);
        }

        public Task<IResultModel<RequestCaseDto>> SaveAsync(RequestCaseDto requestCase, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<RequestCaseDto>>(ResultModel<RequestCaseDto>.Success(requestCase));
        }

        public Task SyncImportedHttpInterfacesAsync(string projectId, IReadOnlyList<ApiEndpointDto> endpoints, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<IResultModel<RequestCaseDto>> DuplicateAsync(string projectId, string id, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<RequestCaseDto>>(ResultModel<RequestCaseDto>.Failure("未实现"));
        }

        public Task<IResultModel<bool>> DeleteAsync(string projectId, string id, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<bool>>(ResultModel<bool>.Success(true));
        }
    }

    private sealed class FakeRequestExecutionService : IRequestExecutionService
    {
        public Task<IResultModel<ResponseSnapshotDto>> SendAsync(RequestSnapshotDto request, ProjectEnvironmentDto environment, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<ResponseSnapshotDto>>(ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto()));
        }
    }

    private sealed class FakeRequestHistoryService : IRequestHistoryService
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

    private sealed class FakeApiWorkspaceService : IApiWorkspaceService
    {
        public Task<IReadOnlyList<ApiDocumentDto>> GetDocumentsAsync(string projectId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ApiDocumentDto>>([]);
        }

        public Task<IReadOnlyList<ApiEndpointDto>> GetEndpointsAsync(string documentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ApiEndpointDto>>([]);
        }

        public Task<ApiDocumentDto?> GetDocumentAsync(string projectId, string documentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<ApiDocumentDto?>(null);
        }

        public Task<IResultModel<ApiImportPreviewDto>> PreviewImportFromUrlAsync(string projectId, string url, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<ApiImportPreviewDto>>(ResultModel<ApiImportPreviewDto>.Failure("未实现"));
        }

        public Task<IResultModel<ApiImportPreviewDto>> PreviewImportFromFileAsync(string projectId, string filePath, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<ApiImportPreviewDto>>(ResultModel<ApiImportPreviewDto>.Failure("未实现"));
        }

        public Task<IResultModel<ApiDocumentDto>> ImportFromUrlAsync(string projectId, string url, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<ApiDocumentDto>>(ResultModel<ApiDocumentDto>.Failure("未实现"));
        }

        public Task<IResultModel<ApiDocumentDto>> ImportFromFileAsync(string projectId, string filePath, CancellationToken cancellationToken)
        {
            return Task.FromResult<IResultModel<ApiDocumentDto>>(ResultModel<ApiDocumentDto>.Failure("未实现"));
        }

        public Task DeleteImportedHttpInterfacesAsync(string projectId, IReadOnlyList<RequestCaseDto> requestCases, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFilePickerService : IFilePickerService
    {
        public Task<string?> PickSwaggerJsonFileAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(null);
        }
    }
}
