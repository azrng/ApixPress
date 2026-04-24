using FakeAppNotificationService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeAppNotificationService;
using FakeEnvironmentVariableService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeEnvironmentVariableService;
using FakeFilePickerService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeFilePickerService;
using FakeRequestCaseService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeRequestCaseService;
using FakeRequestExecutionService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeRequestExecutionService;
using FakeRequestHistoryService = ApixPress.App.Tests.ViewModels.ViewModelSharedTestDoubles.FakeRequestHistoryService;
using Avalonia.Controls;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels;
using Azrng.Core.Results;

namespace ApixPress.App.Tests.ViewModels;

public sealed partial class MainWindowViewModelTests
{
    private static MainWindowViewModel CreateViewModel(
        FakeProjectWorkspaceService? projectWorkspaceService = null,
        FakeAppShellSettingsService? shellSettingsService = null,
        FakeApplicationUpdateService? applicationUpdateService = null,
        IRequestCaseService? requestCaseService = null,
        IRequestHistoryService? requestHistoryService = null,
        IEnvironmentVariableService? environmentVariableService = null,
        IApiWorkspaceService? apiWorkspaceService = null)
    {
        return new MainWindowViewModel(
            new FakeRequestExecutionService(),
            requestCaseService ?? new FakeRequestCaseService(),
            requestHistoryService ?? new FakeRequestHistoryService(),
            environmentVariableService ?? new FakeEnvironmentVariableService(),
            projectWorkspaceService ?? new FakeProjectWorkspaceService(),
            shellSettingsService ?? new FakeAppShellSettingsService(),
            applicationUpdateService ?? new FakeApplicationUpdateService(),
            apiWorkspaceService ?? new FakeApiWorkspaceService(),
            new FakeFilePickerService(),
            new FakeAppNotificationService(),
            new FakeWindowHostService());
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

        public Task<IReadOnlyList<ApiEndpointDto>> GetProjectEndpointsAsync(string projectId, CancellationToken cancellationToken)
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

    private sealed class FakeApplicationUpdateService : IApplicationUpdateService
    {
        public string ChannelName { get; set; } = "GitHub Releases";

        public bool IsConfigured { get; set; } = true;

        public AppUpdateCheckResultDto CheckResult { get; set; } = new()
        {
            CurrentVersion = "1.0.0.0",
            LatestVersion = "1.0.0.0",
            HasUpdate = false
        };

        public string StartMessage { get; set; } = string.Empty;

        public bool StartSucceeded { get; set; } = true;

        public int CheckCalls { get; private set; }

        public int StartCalls { get; private set; }

        public Task<IResultModel<AppUpdateCheckResultDto>> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken)
        {
            CheckCalls++;
            return Task.FromResult<IResultModel<AppUpdateCheckResultDto>>(ResultModel<AppUpdateCheckResultDto>.Success(CheckResult));
        }

        public Task<IResultModel<bool>> StartUpdateAsync(AppUpdateCheckResultDto updateInfo, CancellationToken cancellationToken)
        {
            StartCalls++;
            return Task.FromResult<IResultModel<bool>>(StartSucceeded
                ? ResultModel<bool>.Success(true)
                : ResultModel<bool>.Failure(StartMessage));
        }
    }

    private sealed class FakeWindowHostService : IWindowHostService
    {
        public Window? MainWindow { get; set; }
    }

}
