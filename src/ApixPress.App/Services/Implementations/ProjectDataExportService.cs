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
    // 项目数据包大小上限，与 Swagger 导入的 20MB 保持一致
    private const long MaxProjectPackageFileBytes = 20 * 1024 * 1024;

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
    private readonly IProjectEnvironmentRepository? _projectEnvironmentRepository;
    private readonly IEnvironmentVariableRepository? _environmentVariableRepository;
    private readonly IRequestCaseDeliveryRepository? _requestCaseDeliveryRepository;
    private readonly IProjectExportSettingsRepository? _projectExportSettingsRepository;
    private readonly IRequestHistoryService? _requestHistoryService;
    private readonly IProjectDataHtmlRenderer? _htmlRenderer;
    private readonly IProjectDataMarkdownRenderer? _markdownRenderer;

    public ProjectDataExportService(
        IApiDocumentRepository apiDocumentRepository,
        IRequestCaseService requestCaseService)
        : this(apiDocumentRepository, requestCaseService, null, null, null)
    {
    }

    public ProjectDataExportService(
        IApiDocumentRepository apiDocumentRepository,
        IRequestCaseService requestCaseService,
        IProjectEnvironmentRepository? projectEnvironmentRepository,
        IEnvironmentVariableRepository? environmentVariableRepository,
        IRequestCaseDeliveryRepository? requestCaseDeliveryRepository)
        : this(
            apiDocumentRepository,
            requestCaseService,
            projectEnvironmentRepository,
            environmentVariableRepository,
            requestCaseDeliveryRepository,
            null,
            null,
            null,
            null)
    {
    }

    public ProjectDataExportService(
        IApiDocumentRepository apiDocumentRepository,
        IRequestCaseService requestCaseService,
        IProjectEnvironmentRepository? projectEnvironmentRepository,
        IEnvironmentVariableRepository? environmentVariableRepository,
        IRequestCaseDeliveryRepository? requestCaseDeliveryRepository,
        IProjectExportSettingsRepository? projectExportSettingsRepository,
        IRequestHistoryService? requestHistoryService,
        IProjectDataHtmlRenderer? htmlRenderer,
        IProjectDataMarkdownRenderer? markdownRenderer)
    {
        _apiDocumentRepository = apiDocumentRepository;
        _requestCaseService = requestCaseService;
        _projectEnvironmentRepository = projectEnvironmentRepository;
        _environmentVariableRepository = environmentVariableRepository;
        _requestCaseDeliveryRepository = requestCaseDeliveryRepository;
        _projectExportSettingsRepository = projectExportSettingsRepository;
        _requestHistoryService = requestHistoryService;
        _htmlRenderer = htmlRenderer;
        _markdownRenderer = markdownRenderer;
    }

    public async Task<IResultModel<ProjectDataExportResultDto>> ExportAsync(ProjectDataExportRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectId))
        {
            return ResultModel<ProjectDataExportResultDto>.Failure("导出失败：缺少项目标识。", "project_data_export_project_required");
        }

        var isSingleFileFormat = request.Format != ProjectDataExportFormat.Markdown;
        if (isSingleFileFormat && string.IsNullOrWhiteSpace(request.OutputFilePath))
        {
            return ResultModel<ProjectDataExportResultDto>.Failure("导出失败：未选择导出文件。", "project_data_export_file_required");
        }

        if (!isSingleFileFormat && string.IsNullOrWhiteSpace(request.OutputDirectoryPath))
        {
            return ResultModel<ProjectDataExportResultDto>.Failure("导出失败：未选择导出目录。", "project_data_export_directory_required");
        }

        try
        {
            return request.Format switch
            {
                ProjectDataExportFormat.Html => await ExportHtmlAsync(request, cancellationToken),
                ProjectDataExportFormat.Markdown => await ExportMarkdownAsync(request, cancellationToken),
                _ => await ExportBackupAsync(request, cancellationToken)
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<ProjectDataExportResultDto>.Failure("已取消导出项目数据。", "project_data_export_cancelled");
        }
        catch (Exception exception)
        {
            return ResultModel<ProjectDataExportResultDto>.Failure($"导出失败：{exception.Message}", "project_data_export_failed");
        }
    }

    public async Task<IResultModel<ProjectDataExportSettingsDto>> LoadExportSettingsAsync(string projectId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return ResultModel<ProjectDataExportSettingsDto>.Failure("读取导出设置失败：缺少项目标识。", "project_export_settings_project_required");
        }

        // 设置仓储在完整依赖注入下始终可用；缺省时返回默认值，不阻塞导出页
        if (_projectExportSettingsRepository is null)
        {
            return ResultModel<ProjectDataExportSettingsDto>.Success(new ProjectDataExportSettingsDto());
        }

        try
        {
            var entity = await _projectExportSettingsRepository.GetAsync(projectId, cancellationToken);
            var settings = entity is null
                ? new ProjectDataExportSettingsDto()
                : new ProjectDataExportSettingsDto
                {
                    Format = ProjectDataExportFormats.ToToken(ProjectDataExportFormats.FromToken(entity.Format)),
                    IncludeInterfaces = entity.IncludeInterfaces,
                    IncludeRequestCases = entity.IncludeRequestCases,
                    IncludeEnvironments = entity.IncludeEnvironments,
                    IncludeRequestHistory = entity.IncludeRequestHistory,
                    LastOutputDirectory = entity.LastOutputDirectory
                };
            return ResultModel<ProjectDataExportSettingsDto>.Success(settings);
        }
        catch (Exception exception)
        {
            return ResultModel<ProjectDataExportSettingsDto>.Failure($"读取导出设置失败：{exception.Message}", "project_export_settings_load_failed");
        }
    }

    public async Task<IResultModel<bool>> SaveExportSettingsAsync(string projectId, ProjectDataExportSettingsDto settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return ResultModel<bool>.Failure("保存导出设置失败：缺少项目标识。", "project_export_settings_project_required");
        }

        if (_projectExportSettingsRepository is null)
        {
            return ResultModel<bool>.Success(true);
        }

        try
        {
            await _projectExportSettingsRepository.UpsertAsync(new ProjectExportSettingsEntity
            {
                ProjectId = projectId,
                Format = ProjectDataExportFormats.ToToken(ProjectDataExportFormats.FromToken(settings.Format)),
                IncludeInterfaces = settings.IncludeInterfaces,
                IncludeRequestCases = settings.IncludeRequestCases,
                IncludeEnvironments = settings.IncludeEnvironments,
                IncludeRequestHistory = settings.IncludeRequestHistory,
                LastOutputDirectory = settings.LastOutputDirectory,
                UpdatedAt = DateTime.UtcNow
            }, cancellationToken);
            return ResultModel<bool>.Success(true);
        }
        catch (Exception exception)
        {
            return ResultModel<bool>.Failure($"保存导出设置失败：{exception.Message}", "project_export_settings_save_failed");
        }
    }

    private async Task<IResultModel<ProjectDataExportResultDto>> ExportBackupAsync(ProjectDataExportRequestDto request, CancellationToken cancellationToken)
    {
        var cases = await _requestCaseService.GetCaseDetailsAsync(request.ProjectId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // 孤儿规则：请求用例只在父接口同时导出时写入包内，避免导入端出现无归属用例
        var interfaces = request.IncludeInterfaces
            ? cases
                .Where(item => string.Equals(item.EntryType, "http-interface", StringComparison.OrdinalIgnoreCase))
                .Select(ToExportEntry)
                .OrderBy(item => item.FolderPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];
        var interfaceIds = interfaces.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var testCases = request.IncludeRequestCases
            ? cases
                .Where(item => string.Equals(item.EntryType, "http-case", StringComparison.OrdinalIgnoreCase)
                    && interfaceIds.Contains(item.ParentId))
                .Select(ToExportEntry)
                .OrderBy(item => item.ParentId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

        // 环境与历史仓储按需注入；缺失时按空范围降级导出，生产依赖注入下不会出现
        var environments = new List<ProjectDataExportEnvironmentDto>();
        if (request.IncludeEnvironments
            && _projectEnvironmentRepository is not null
            && _environmentVariableRepository is not null)
        {
            environments = await CollectEnvironmentsAsync(request.ProjectId, cancellationToken);
        }

        var history = new List<RequestHistoryItemDto>();
        if (request.IncludeRequestHistory && _requestHistoryService is not null)
        {
            history = (await _requestHistoryService.GetHistoryAsync(request.ProjectId, cancellationToken)).ToList();
        }

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
            TestCases = testCases,
            Environments = environments,
            History = history
        };

        await WriteFileAtomicallyAsync(request.OutputFilePath, async stream =>
        {
            await stream.WriteAsync(new byte[] { 0xEF, 0xBB, 0xBF }, cancellationToken);
            await JsonSerializer.SerializeAsync(stream, package, SerializerOptions, cancellationToken);
        });
        return ResultModel<ProjectDataExportResultDto>.Success(new ProjectDataExportResultDto
        {
            OutputPath = request.OutputFilePath,
            Format = ProjectDataExportFormat.Backup,
            InterfaceCount = interfaces.Count,
            TestCaseCount = testCases.Count,
            EnvironmentCount = environments.Count,
            VariableCount = environments.Sum(item => item.Variables.Count),
            HistoryCount = history.Count,
            FileCount = 1
        });
    }

    private async Task<IResultModel<ProjectDataExportResultDto>> ExportHtmlAsync(ProjectDataExportRequestDto request, CancellationToken cancellationToken)
    {
        if (_htmlRenderer is null)
        {
            return ResultModel<ProjectDataExportResultDto>.Failure("导出失败：HTML 渲染服务未初始化。", "project_data_export_renderer_missing");
        }

        var model = await BuildDocumentModelAsync(request, cancellationToken);
        var html = _htmlRenderer.Render(model);
        var bytes = Encoding.UTF8.GetBytes(html);
        await WriteFileAtomicallyAsync(request.OutputFilePath, async stream => await stream.WriteAsync(bytes, cancellationToken));
        return ResultModel<ProjectDataExportResultDto>.Success(new ProjectDataExportResultDto
        {
            OutputPath = request.OutputFilePath,
            Format = ProjectDataExportFormat.Html,
            InterfaceCount = model.Interfaces.Count,
            TestCaseCount = model.Interfaces.Sum(item => item.Cases.Count),
            FileCount = 1
        });
    }

    private async Task<IResultModel<ProjectDataExportResultDto>> ExportMarkdownAsync(ProjectDataExportRequestDto request, CancellationToken cancellationToken)
    {
        if (_markdownRenderer is null)
        {
            return ResultModel<ProjectDataExportResultDto>.Failure("导出失败：Markdown 渲染服务未初始化。", "project_data_export_renderer_missing");
        }

        var model = await BuildDocumentModelAsync(request, cancellationToken);
        var files = _markdownRenderer.Render(model);
        var outputDirectory = Path.Combine(request.OutputDirectoryPath, BuildMarkdownDirectoryName(request.ProjectName));

        if (Directory.Exists(outputDirectory) && Directory.EnumerateFileSystemEntries(outputDirectory).Any())
        {
            return ResultModel<ProjectDataExportResultDto>.Failure(
                $"导出失败：目标目录“{Path.GetFileName(outputDirectory)}”已存在且非空，请清空后重试或选择其他位置。",
                "project_data_export_directory_not_empty");
        }

        Directory.CreateDirectory(outputDirectory);
        try
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var filePath = Path.Combine(outputDirectory, file.RelativePath);
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(filePath, file.Content, Encoding.UTF8, cancellationToken);
            }
        }
        catch
        {
            // 半成品不留痕：写盘失败或取消时清掉本次创建的输出目录后交由外层包装错误
            try
            {
                Directory.Delete(outputDirectory, true);
            }
            catch
            {
                // 清理失败不掩盖原始异常
            }

            throw;
        }

        return ResultModel<ProjectDataExportResultDto>.Success(new ProjectDataExportResultDto
        {
            OutputPath = outputDirectory,
            Format = ProjectDataExportFormat.Markdown,
            InterfaceCount = model.Interfaces.Count,
            TestCaseCount = model.Interfaces.Sum(item => item.Cases.Count),
            FileCount = files.Count
        });
    }

    private async Task<ProjectDataDocumentModelDto> BuildDocumentModelAsync(ProjectDataExportRequestDto request, CancellationToken cancellationToken)
    {
        var cases = await _requestCaseService.GetCaseDetailsAsync(request.ProjectId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var interfaces = cases
            .Where(item => string.Equals(item.EntryType, "http-interface", StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.FolderPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.RequestSnapshot.Method, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var casesByParent = cases
            .Where(item => string.Equals(item.EntryType, "http-case", StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.ParentId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .ToList(), StringComparer.OrdinalIgnoreCase);

        return new ProjectDataDocumentModelDto
        {
            ProjectName = request.ProjectName,
            ProjectDescription = request.ProjectDescription,
            ExportedAt = DateTime.UtcNow,
            Interfaces = interfaces.Select(item => new ProjectDataDocumentInterfaceDto
            {
                FolderPath = item.FolderPath,
                Name = item.Name,
                Description = item.Description,
                Snapshot = item.RequestSnapshot,
                Cases = (casesByParent.GetValueOrDefault(item.Id) ?? [])
                    .Select(testCase => new ProjectDataDocumentCaseDto
                    {
                        Name = testCase.Name,
                        Description = testCase.Description,
                        Snapshot = testCase.RequestSnapshot
                    })
                    .ToList()
            }).ToList()
        };
    }

    private async Task<List<ProjectDataExportEnvironmentDto>> CollectEnvironmentsAsync(string projectId, CancellationToken cancellationToken)
    {
        var environments = await _projectEnvironmentRepository!.GetByProjectAsync(projectId, cancellationToken);
        var variablesByEnvironment = await _environmentVariableRepository!.GetByEnvironmentIdsAsync(
            environments.Select(item => item.Id),
            cancellationToken);
        return environments
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ProjectDataExportEnvironmentDto
            {
                Id = item.Id,
                Name = item.Name,
                BaseUrl = item.BaseUrl,
                IsActive = item.IsActive,
                SortOrder = item.SortOrder,
                Variables = (variablesByEnvironment.GetValueOrDefault(item.Id) ?? [])
                    .Select(variable => new ProjectDataExportVariableDto
                    {
                        Key = variable.Key,
                        Value = variable.Value,
                        IsEnabled = variable.IsEnabled
                    })
                    .OrderBy(variable => variable.Key, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .ToList();
    }

    private static string BuildMarkdownDirectoryName(string projectName)
    {
        var fallback = string.IsNullOrWhiteSpace(projectName) ? "project" : projectName.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = new string(fallback.Select(character => invalidChars.Contains(character) ? '-' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safeName) ? "project-docs" : $"{safeName}-docs";
    }

    private static async Task WriteFileAtomicallyAsync(string outputFilePath, Func<FileStream, Task> writeAsync)
    {
        var directoryPath = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var temporaryFilePath = $"{outputFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(
                temporaryFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
            {
                await writeAsync(stream);
            }

            File.Move(temporaryFilePath, outputFilePath, true);
        }
        catch
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }

            throw;
        }
    }

    public async Task<IResultModel<RequestCaseDeliveryOptionsDto>> GetRequestCaseDeliveryOptionsAsync(
        string projectId,
        CancellationToken cancellationToken)
    {
        if (!TryGetDeliveryDependencies(out var dependencyError))
        {
            return ResultModel<RequestCaseDeliveryOptionsDto>.Failure(dependencyError, "request_case_delivery_dependency_missing");
        }

        if (string.IsNullOrWhiteSpace(projectId))
        {
            return ResultModel<RequestCaseDeliveryOptionsDto>.Failure("请先选择项目。", "request_case_delivery_project_required");
        }

        try
        {
            var cases = await _requestCaseService.GetCaseDetailsAsync(projectId, cancellationToken);
            var environments = await _projectEnvironmentRepository!.GetByProjectAsync(projectId, cancellationToken);
            var variables = await _environmentVariableRepository!.GetByEnvironmentIdsAsync(
                environments.Select(item => item.Id),
                cancellationToken);
            var interfaces = cases
                .Where(IsHttpInterface)
                .OrderBy(item => item.FolderPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .Select(item => ToDeliveryCandidate(item, isSelected: false, isAutoIncluded: false))
                .ToList();
            var requestCases = cases
                .Where(IsHttpCase)
                .OrderBy(item => item.ParentId, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(item => item.UpdatedAt)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .Select(item => ToDeliveryCandidate(item, isSelected: false, isAutoIncluded: false))
                .ToList();

            return ResultModel<RequestCaseDeliveryOptionsDto>.Success(new RequestCaseDeliveryOptionsDto
            {
                Interfaces = interfaces,
                RequestCases = requestCases,
                EnvironmentCount = environments.Count,
                VariableCount = variables.Values.Sum(item => item.Count)
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<RequestCaseDeliveryOptionsDto>.Failure("已取消读取请求用例交付范围。", "request_case_delivery_options_cancelled");
        }
        catch (Exception exception)
        {
            return ResultModel<RequestCaseDeliveryOptionsDto>.Failure(
                $"读取请求用例交付范围失败：{exception.Message}",
                "request_case_delivery_options_failed");
        }
    }

    public async Task<IResultModel<RequestCaseDeliveryResultDto>> ExportRequestCaseDeliveryAsync(
        RequestCaseDeliveryRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetDeliveryDependencies(out var dependencyError))
        {
            return ResultModel<RequestCaseDeliveryResultDto>.Failure(dependencyError, "request_case_delivery_dependency_missing");
        }

        if (string.IsNullOrWhiteSpace(request.ProjectId))
        {
            return ResultModel<RequestCaseDeliveryResultDto>.Failure("交付失败：缺少项目标识。", "request_case_delivery_project_required");
        }

        if (string.IsNullOrWhiteSpace(request.OutputFilePath))
        {
            return ResultModel<RequestCaseDeliveryResultDto>.Failure("交付失败：未选择输出文件。", "request_case_delivery_file_required");
        }

        if (request.SelectedInterfaceIds.Count == 0 && request.SelectedRequestCaseIds.Count == 0)
        {
            return ResultModel<RequestCaseDeliveryResultDto>.Failure("请至少选择一个接口或请求用例。", "request_case_delivery_selection_required");
        }

        try
        {
            var cases = await _requestCaseService.GetCaseDetailsAsync(request.ProjectId, cancellationToken);
            var interfacesById = cases
                .Where(IsHttpInterface)
                .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
            var casesById = cases
                .Where(IsHttpCase)
                .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
            var selectedInterfaceIds = new HashSet<string>(request.SelectedInterfaceIds, StringComparer.OrdinalIgnoreCase);
            var missingParentCases = new List<string>();
            foreach (var caseId in request.SelectedRequestCaseIds)
            {
                if (!casesById.TryGetValue(caseId, out var requestCase)
                    || !interfacesById.ContainsKey(requestCase.ParentId))
                {
                    missingParentCases.Add(caseId);
                    continue;
                }

                selectedInterfaceIds.Add(requestCase.ParentId);
            }

            if (missingParentCases.Count > 0)
            {
                return ResultModel<RequestCaseDeliveryResultDto>.Failure(
                    $"交付失败：有 {missingParentCases.Count} 个请求用例缺少父接口。",
                    "request_case_delivery_orphan_case");
            }

            var selectedInterfaces = selectedInterfaceIds
                .Where(interfacesById.ContainsKey)
                .Select(id => interfacesById[id])
                .OrderBy(item => item.FolderPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var selectedCases = request.SelectedRequestCaseIds
                .Where(casesById.ContainsKey)
                .Select(id => casesById[id])
                .OrderBy(item => item.ParentId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (selectedInterfaces.Count == 0)
            {
                return ResultModel<RequestCaseDeliveryResultDto>.Failure(
                    "交付失败：未找到可交付的接口。",
                    "request_case_delivery_interface_not_found");
            }

            var environments = await _projectEnvironmentRepository!.GetByProjectAsync(request.ProjectId, cancellationToken);
            var variablesByEnvironment = await _environmentVariableRepository!.GetByEnvironmentIdsAsync(
                environments.Select(item => item.Id),
                cancellationToken);
            var package = new RequestCaseDeliveryPackageDto
            {
                ExportedAt = DateTime.UtcNow,
                Source = new RequestCaseDeliverySourceDto
                {
                    ProjectId = request.ProjectId,
                    ProjectName = request.ProjectName,
                    Product = "ApixPress"
                },
                Interfaces = selectedInterfaces.Select(ToDeliveryInterface).ToList(),
                RequestCases = selectedCases.Select(item => ToDeliveryCase(item)).ToList(),
                Environments = environments
                    .OrderBy(item => item.SortOrder)
                    .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(item => ToDeliveryEnvironment(item, variablesByEnvironment.GetValueOrDefault(item.Id) ?? []))
                    .ToList()
            };

            await WriteDeliveryPackageAsync(request.OutputFilePath, package, cancellationToken);
            return ResultModel<RequestCaseDeliveryResultDto>.Success(new RequestCaseDeliveryResultDto
            {
                FilePath = request.OutputFilePath,
                InterfaceCount = package.Interfaces.Count,
                RequestCaseCount = package.RequestCases.Count,
                EnvironmentCount = package.Environments.Count,
                VariableCount = package.Environments.Sum(item => item.Variables.Count)
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<RequestCaseDeliveryResultDto>.Failure("已取消请求用例交付。", "request_case_delivery_cancelled");
        }
        catch (Exception exception)
        {
            return ResultModel<RequestCaseDeliveryResultDto>.Failure(
                $"请求用例交付失败：{exception.Message}",
                "request_case_delivery_failed");
        }
    }

    public async Task<IResultModel<RequestCaseDeliveryPreviewDto>> PreviewRequestCaseDeliveryAsync(
        string projectId,
        string filePath,
        CancellationToken cancellationToken)
    {
        if (!TryGetDeliveryDependencies(out var dependencyError))
        {
            return ResultModel<RequestCaseDeliveryPreviewDto>.Failure(dependencyError, "request_case_delivery_dependency_missing");
        }

        try
        {
            var packageResult = await LoadDeliveryPackageAsync(filePath, cancellationToken);
            if (!packageResult.IsSuccess || packageResult.Package is null)
            {
                return ResultModel<RequestCaseDeliveryPreviewDto>.Failure(packageResult.Message, packageResult.ErrorCode);
            }

            var targetCases = await _requestCaseService.GetCaseDetailsAsync(projectId, cancellationToken);
            var targetEnvironments = await _projectEnvironmentRepository!.GetByProjectAsync(projectId, cancellationToken);
            var preview = BuildDeliveryPreview(projectId, filePath, packageResult.Package, targetCases, targetEnvironments);
            return ResultModel<RequestCaseDeliveryPreviewDto>.Success(preview);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<RequestCaseDeliveryPreviewDto>.Failure("已取消请求用例交付预览。", "request_case_delivery_preview_cancelled");
        }
        catch (Exception exception)
        {
            return ResultModel<RequestCaseDeliveryPreviewDto>.Failure(
                $"请求用例交付预览失败：{exception.Message}",
                "request_case_delivery_preview_failed");
        }
    }

    public async Task<IResultModel<RequestCaseDeliveryResultDto>> ImportRequestCaseDeliveryAsync(
        string projectId,
        string filePath,
        IReadOnlyList<RequestCaseDeliveryDecisionDto> decisions,
        CancellationToken cancellationToken)
    {
        if (!TryGetDeliveryDependencies(out var dependencyError))
        {
            return ResultModel<RequestCaseDeliveryResultDto>.Failure(dependencyError, "request_case_delivery_dependency_missing");
        }

        try
        {
            var packageResult = await LoadDeliveryPackageAsync(filePath, cancellationToken);
            if (!packageResult.IsSuccess || packageResult.Package is null)
            {
                return ResultModel<RequestCaseDeliveryResultDto>.Failure(packageResult.Message, packageResult.ErrorCode);
            }

            var targetCases = await _requestCaseService.GetCaseDetailsAsync(projectId, cancellationToken);
            var targetEnvironments = await _projectEnvironmentRepository!.GetByProjectAsync(projectId, cancellationToken);
            var targetEndpoints = await _apiDocumentRepository.GetEndpointsByProjectIdAsync(projectId, cancellationToken);
            var planResult = BuildDeliveryImportPlan(
                projectId,
                filePath,
                packageResult.RawJson,
                packageResult.Package,
                targetCases,
                targetEnvironments,
                targetEndpoints,
                decisions);
            if (!planResult.IsSuccess || planResult.Plan is null)
            {
                return ResultModel<RequestCaseDeliveryResultDto>.Failure(planResult.Message, planResult.ErrorCode);
            }

            await _requestCaseDeliveryRepository!.ImportAsync(planResult.Plan, cancellationToken);
            return ResultModel<RequestCaseDeliveryResultDto>.Success(planResult.Result!);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<RequestCaseDeliveryResultDto>.Failure("已取消请求用例交付导入，已回滚本次变更。", "request_case_delivery_import_cancelled");
        }
        catch (Exception exception)
        {
            return ResultModel<RequestCaseDeliveryResultDto>.Failure(
                $"请求用例交付导入失败，已回滚本次变更：{exception.Message}",
                "request_case_delivery_import_failed");
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

            var testCaseDtos = new List<RequestCaseDto>();
            foreach (var testCase in package.TestCases)
            {
                if (!packageInterfaceKeyById.TryGetValue(testCase.ParentId, out var parentEndpointKey)
                    || !interfaceIdByEndpointKey.TryGetValue(parentEndpointKey, out var parentInterfaceId))
                {
                    return ResultModel<ApiDocumentDto>.Failure(
                        $"项目数据包导入失败：用例“{testCase.Name}”未找到对应接口。",
                        "project_data_package_case_parent_missing");
                }

                testCaseDtos.Add(new RequestCaseDto
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
                });
            }

            if (testCaseDtos.Count > 0)
            {
                var batchResult = await _requestCaseService.SaveRangeAsync(testCaseDtos, cancellationToken);
                if (!batchResult.IsSuccess)
                {
                    return ResultModel<ApiDocumentDto>.Failure(
                        batchResult.Message ?? "项目数据包导入失败：用例批量保存出错。",
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

    private bool TryGetDeliveryDependencies(out string error)
    {
        if (_projectEnvironmentRepository is null
            || _environmentVariableRepository is null
            || _requestCaseDeliveryRepository is null)
        {
            error = "请求用例交付服务未完成初始化。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsHttpInterface(RequestCaseDto requestCase)
    {
        return string.Equals(requestCase.EntryType, "http-interface", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHttpCase(RequestCaseDto requestCase)
    {
        return string.Equals(requestCase.EntryType, "http-case", StringComparison.OrdinalIgnoreCase);
    }

    private static RequestCaseDeliveryCandidateDto ToDeliveryCandidate(
        RequestCaseDto requestCase,
        bool isSelected,
        bool isAutoIncluded)
    {
        return new RequestCaseDeliveryCandidateDto
        {
            Id = requestCase.Id,
            ParentId = requestCase.ParentId,
            EntryType = requestCase.EntryType,
            Name = requestCase.Name,
            Method = requestCase.RequestSnapshot.Method,
            Url = requestCase.RequestSnapshot.Url,
            FolderPath = requestCase.FolderPath,
            IsSelected = isSelected,
            IsAutoIncluded = isAutoIncluded
        };
    }

    private static RequestCaseDeliveryInterfaceDto ToDeliveryInterface(RequestCaseDto requestCase)
    {
        return new RequestCaseDeliveryInterfaceDto
        {
            SourceId = requestCase.Id,
            Name = requestCase.Name,
            GroupName = requestCase.GroupName,
            FolderPath = requestCase.FolderPath,
            Description = requestCase.Description,
            RequestSnapshot = CloneSnapshot(requestCase.RequestSnapshot)
        };
    }

    private static RequestCaseDeliveryCaseDto ToDeliveryCase(RequestCaseDto requestCase)
    {
        return new RequestCaseDeliveryCaseDto
        {
            SourceId = requestCase.Id,
            ParentInterfaceSourceId = requestCase.ParentId,
            Name = requestCase.Name,
            GroupName = requestCase.GroupName,
            FolderPath = requestCase.FolderPath,
            Tags = requestCase.Tags.ToList(),
            Description = requestCase.Description,
            RequestSnapshot = CloneSnapshot(requestCase.RequestSnapshot),
            UpdatedAt = requestCase.UpdatedAt
        };
    }

    private static RequestCaseDeliveryEnvironmentDto ToDeliveryEnvironment(
        ProjectEnvironmentEntity environment,
        IReadOnlyList<EnvironmentVariableEntity> variables)
    {
        return new RequestCaseDeliveryEnvironmentDto
        {
            SourceId = environment.Id,
            Name = environment.Name,
            BaseUrl = environment.BaseUrl,
            IsActive = environment.IsActive,
            SortOrder = environment.SortOrder,
            Variables = variables
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .Select(item => new RequestCaseDeliveryVariableDto
                {
                    SourceId = item.Id,
                    Key = item.Key,
                    Value = item.Value,
                    IsEnabled = item.IsEnabled
                })
                .ToList()
        };
    }

    private static RequestSnapshotDto CloneSnapshot(RequestSnapshotDto snapshot)
    {
        return new RequestSnapshotDto
        {
            EndpointId = snapshot.EndpointId,
            Name = snapshot.Name,
            Method = snapshot.Method,
            Url = snapshot.Url,
            Description = snapshot.Description,
            BodyMode = snapshot.BodyMode,
            BodyContent = snapshot.BodyContent,
            IgnoreSslErrors = snapshot.IgnoreSslErrors,
            QueryParameters = CloneKeyValues(snapshot.QueryParameters),
            PathParameters = CloneKeyValues(snapshot.PathParameters),
            Headers = CloneKeyValues(snapshot.Headers)
        };
    }

    private static async Task WriteDeliveryPackageAsync(
        string outputFilePath,
        RequestCaseDeliveryPackageDto package,
        CancellationToken cancellationToken)
    {
        await WriteFileAtomicallyAsync(outputFilePath, async stream =>
        {
            await stream.WriteAsync(new byte[] { 0xEF, 0xBB, 0xBF }, cancellationToken);
            await JsonSerializer.SerializeAsync(stream, package, SerializerOptions, cancellationToken);
        });
    }

    private static async Task<DeliveryPackageLoadResult> LoadDeliveryPackageAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return DeliveryPackageLoadResult.Failure("请选择要导入的请求用例交付包。", "request_case_delivery_file_required");
        }

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            return DeliveryPackageLoadResult.Failure("请求用例交付包不存在。", "request_case_delivery_file_not_found");
        }

        if (fileInfo.Length > MaxProjectPackageFileBytes)
        {
            return DeliveryPackageLoadResult.Failure(
                $"请求用例交付包文件过大（{fileInfo.Length / 1024.0 / 1024.0:F1} MB），上限为 {MaxProjectPackageFileBytes / 1024.0 / 1024.0:F0} MB。",
                "request_case_delivery_too_large");
        }

        var rawJson = await File.ReadAllTextAsync(filePath, cancellationToken);
        var package = await Task.Run(
            () => JsonSerializer.Deserialize<RequestCaseDeliveryPackageDto>(rawJson, ImportSerializerOptions),
            cancellationToken);
        if (package is null)
        {
            return DeliveryPackageLoadResult.Failure("请求用例交付包解析失败：文件内容为空或结构无效。", "request_case_delivery_invalid");
        }

        if (!string.Equals(package.FormatName, RequestCaseDeliveryPackageDto.Format, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(package.SchemaVersion, RequestCaseDeliveryPackageDto.CurrentSchemaVersion, StringComparison.OrdinalIgnoreCase))
        {
            return DeliveryPackageLoadResult.Failure(
                $"请求用例交付包版本不受支持：{package.SchemaVersion}",
                "request_case_delivery_schema_not_supported");
        }

        if (string.IsNullOrWhiteSpace(package.Source.ProjectId))
        {
            return DeliveryPackageLoadResult.Failure("请求用例交付包缺少来源项目标识。", "request_case_delivery_source_missing");
        }

        return DeliveryPackageLoadResult.Success(rawJson, package);
    }

    private static RequestCaseDeliveryPreviewDto BuildDeliveryPreview(
        string projectId,
        string filePath,
        RequestCaseDeliveryPackageDto package,
        IReadOnlyList<RequestCaseDto> targetCases,
        IReadOnlyList<ProjectEnvironmentEntity> targetEnvironments)
    {
        var targetInterfaces = targetCases.Where(IsHttpInterface).ToList();
        var targetRequestCases = targetCases.Where(IsHttpCase).ToList();
        var targetInterfaceBySource = targetInterfaces
            .Where(item => string.Equals(item.SourceProjectId, package.Source.ProjectId, StringComparison.OrdinalIgnoreCase))
            .Where(item => !string.IsNullOrWhiteSpace(item.SourceId))
            .ToDictionary(item => item.SourceId, StringComparer.OrdinalIgnoreCase);
        var targetEnvironmentBySource = targetEnvironments
            .Where(item => string.Equals(item.SourceProjectId, package.Source.ProjectId, StringComparison.OrdinalIgnoreCase))
            .Where(item => !string.IsNullOrWhiteSpace(item.SourceId))
            .ToDictionary(item => item.SourceId, StringComparer.OrdinalIgnoreCase);
        var targetInterfaceByBusinessKey = targetInterfaces
            .GroupBy(item => BuildDeliveryInterfaceBusinessKey(item.RequestSnapshot.Method, item.RequestSnapshot.Url), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var targetEnvironmentByName = targetEnvironments
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var targetCaseBySource = targetRequestCases
            .Where(item => string.Equals(item.SourceProjectId, package.Source.ProjectId, StringComparison.OrdinalIgnoreCase))
            .Where(item => !string.IsNullOrWhiteSpace(item.SourceId))
            .ToDictionary(item => item.SourceId, StringComparer.OrdinalIgnoreCase);
        var targetCaseByBusinessKey = targetRequestCases
            .GroupBy(item => $"{item.ParentId}\u0000{item.Name}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var items = new List<RequestCaseDeliveryPreviewItemDto>();

        foreach (var item in package.Interfaces.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (targetInterfaceBySource.TryGetValue(item.SourceId, out var existingBySource))
            {
                items.Add(new RequestCaseDeliveryPreviewItemDto
                {
                    Kind = "interface",
                    SourceId = item.SourceId,
                    Name = item.Name,
                    Status = "更新",
                    DefaultAction = RequestCaseDeliveryDecisionActions.Update,
                    ExistingId = existingBySource.Id
                });
                continue;
            }

            var businessKey = BuildDeliveryInterfaceBusinessKey(item.RequestSnapshot.Method, item.RequestSnapshot.Url);
            if (targetInterfaceByBusinessKey.TryGetValue(businessKey, out var existingByBusiness))
            {
                items.Add(new RequestCaseDeliveryPreviewItemDto
                {
                    Kind = "interface",
                    SourceId = item.SourceId,
                    Name = item.Name,
                    Status = "冲突",
                    DefaultAction = RequestCaseDeliveryDecisionActions.Skip,
                    ExistingId = existingByBusiness.Id,
                    Detail = $"已存在同方法路径接口：{item.RequestSnapshot.Method} {item.RequestSnapshot.Url}"
                });
                continue;
            }

            items.Add(new RequestCaseDeliveryPreviewItemDto
            {
                Kind = "interface",
                SourceId = item.SourceId,
                Name = item.Name,
                Status = "新增",
                DefaultAction = RequestCaseDeliveryDecisionActions.Import
            });
        }

        var interfaceTargetBySource = package.Interfaces.ToDictionary(
            item => item.SourceId,
            // 业务键冲突的默认策略是跳过；预览不能把被跳过的接口伪装成可用父节点，
            // 否则子用例会显示为可导入，用户确认后才发现整棵节点被跳过。
            item => targetInterfaceBySource.GetValueOrDefault(item.SourceId)?.Id
                    ?? (targetInterfaceByBusinessKey.ContainsKey(BuildDeliveryInterfaceBusinessKey(
                        item.RequestSnapshot.Method,
                        item.RequestSnapshot.Url))
                        ? string.Empty
                        : $"preview-new:{item.SourceId}"),
            StringComparer.OrdinalIgnoreCase);
        foreach (var item in package.RequestCases.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (!interfaceTargetBySource.TryGetValue(item.ParentInterfaceSourceId, out var targetParentId)
                || string.IsNullOrWhiteSpace(targetParentId))
            {
                items.Add(new RequestCaseDeliveryPreviewItemDto
                {
                    Kind = "request-case",
                    SourceId = item.SourceId,
                    Name = item.Name,
                    Status = "孤儿",
                    DefaultAction = RequestCaseDeliveryDecisionActions.Skip,
                    Detail = "未找到可用的父接口。"
                });
                continue;
            }

            if (targetCaseBySource.TryGetValue(item.SourceId, out var existingBySource))
            {
                items.Add(new RequestCaseDeliveryPreviewItemDto
                {
                    Kind = "request-case",
                    SourceId = item.SourceId,
                    Name = item.Name,
                    Status = "更新",
                    DefaultAction = RequestCaseDeliveryDecisionActions.Update,
                    ExistingId = existingBySource.Id
                });
                continue;
            }

            if (targetCaseByBusinessKey.TryGetValue($"{targetParentId}\u0000{item.Name}", out var existingByBusiness))
            {
                items.Add(new RequestCaseDeliveryPreviewItemDto
                {
                    Kind = "request-case",
                    SourceId = item.SourceId,
                    Name = item.Name,
                    Status = "冲突",
                    DefaultAction = RequestCaseDeliveryDecisionActions.Skip,
                    ExistingId = existingByBusiness.Id,
                    Detail = "父接口下已存在同名请求用例。"
                });
                continue;
            }

            items.Add(new RequestCaseDeliveryPreviewItemDto
            {
                Kind = "request-case",
                SourceId = item.SourceId,
                Name = item.Name,
                Status = "新增",
                DefaultAction = RequestCaseDeliveryDecisionActions.Import
            });
        }

        foreach (var item in package.Environments.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (targetEnvironmentBySource.TryGetValue(item.SourceId, out var existingBySource))
            {
                items.Add(new RequestCaseDeliveryPreviewItemDto
                {
                    Kind = "environment",
                    SourceId = item.SourceId,
                    Name = item.Name,
                    Status = "更新",
                    DefaultAction = RequestCaseDeliveryDecisionActions.Update,
                    ExistingId = existingBySource.Id
                });
                continue;
            }

            if (targetEnvironmentByName.TryGetValue(item.Name, out var existingByName))
            {
                items.Add(new RequestCaseDeliveryPreviewItemDto
                {
                    Kind = "environment",
                    SourceId = item.SourceId,
                    Name = item.Name,
                    Status = "冲突",
                    DefaultAction = RequestCaseDeliveryDecisionActions.Skip,
                    ExistingId = existingByName.Id,
                    Detail = "目标项目已有同名环境。"
                });
                continue;
            }

            items.Add(new RequestCaseDeliveryPreviewItemDto
            {
                Kind = "environment",
                SourceId = item.SourceId,
                Name = item.Name,
                Status = "新增",
                DefaultAction = RequestCaseDeliveryDecisionActions.Import
            });
        }

        return new RequestCaseDeliveryPreviewDto
        {
            FilePath = filePath,
            SourceProjectId = package.Source.ProjectId,
            SourceProjectName = package.Source.ProjectName,
            SchemaVersion = package.SchemaVersion,
            InterfaceCount = package.Interfaces.Count,
            RequestCaseCount = package.RequestCases.Count,
            EnvironmentCount = package.Environments.Count,
            VariableCount = package.Environments.Sum(item => item.Variables.Count),
            NewCount = items.Count(item => item.DefaultAction == RequestCaseDeliveryDecisionActions.Import),
            UpdateCount = items.Count(item => item.DefaultAction == RequestCaseDeliveryDecisionActions.Update),
            SkipCount = items.Count(item => item.DefaultAction == RequestCaseDeliveryDecisionActions.Skip && item.Status != "孤儿"),
            DuplicateCount = items.Count(item => item.DefaultAction == RequestCaseDeliveryDecisionActions.Duplicate),
            OrphanCount = items.Count(item => item.Status == "孤儿"),
            Items = items
        };
    }

    private static string BuildDeliveryInterfaceBusinessKey(string method, string url)
    {
        return $"{NormalizeMethod(method)}\u0000{NormalizePath(url)}";
    }

    private static string BuildDeliveryRequestCaseBusinessKey(
        string groupName,
        string folderPath,
        string parentId,
        string name)
    {
        return $"{groupName}\u0000{folderPath}\u0000{parentId}\u0000{name}";
    }

    private static string ResolveUniqueRequestCaseName(
        string baseName,
        string groupName,
        string folderPath,
        string parentId,
        ISet<string> usedNames,
        bool forceCopySuffix = false)
    {
        var normalizedBaseName = string.IsNullOrWhiteSpace(baseName) ? "未命名" : baseName.Trim();
        var candidate = forceCopySuffix ? $"{normalizedBaseName}_copy" : normalizedBaseName;
        var suffix = 2;
        while (!usedNames.Add(BuildDeliveryRequestCaseBusinessKey(groupName, folderPath, parentId, candidate)))
        {
            candidate = $"{normalizedBaseName}_copy{suffix++}";
        }

        return candidate;
    }

    private static DeliveryImportPlanResult BuildDeliveryImportPlan(
        string projectId,
        string filePath,
        string rawJson,
        RequestCaseDeliveryPackageDto package,
        IReadOnlyList<RequestCaseDto> targetCases,
        IReadOnlyList<ProjectEnvironmentEntity> targetEnvironments,
        IReadOnlyList<ApiProjectEndpointEntity> targetEndpoints,
        IReadOnlyList<RequestCaseDeliveryDecisionDto> decisions)
    {
        var targetInterfaces = targetCases.Where(IsHttpInterface).ToList();
        var targetRequestCases = targetCases.Where(IsHttpCase).ToList();
        var targetInterfaceBySource = targetInterfaces
            .Where(item => string.Equals(item.SourceProjectId, package.Source.ProjectId, StringComparison.OrdinalIgnoreCase))
            .Where(item => !string.IsNullOrWhiteSpace(item.SourceId))
            .ToDictionary(item => item.SourceId, StringComparer.OrdinalIgnoreCase);
        var targetCaseBySource = targetRequestCases
            .Where(item => string.Equals(item.SourceProjectId, package.Source.ProjectId, StringComparison.OrdinalIgnoreCase))
            .Where(item => !string.IsNullOrWhiteSpace(item.SourceId))
            .ToDictionary(item => item.SourceId, StringComparer.OrdinalIgnoreCase);
        var targetEnvironmentBySource = targetEnvironments
            .Where(item => string.Equals(item.SourceProjectId, package.Source.ProjectId, StringComparison.OrdinalIgnoreCase))
            .Where(item => !string.IsNullOrWhiteSpace(item.SourceId))
            .ToDictionary(item => item.SourceId, StringComparer.OrdinalIgnoreCase);
        var targetInterfaceByBusinessKey = targetInterfaces
            .GroupBy(item => BuildDeliveryInterfaceBusinessKey(item.RequestSnapshot.Method, item.RequestSnapshot.Url), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var targetCaseByBusinessKey = targetRequestCases
            .GroupBy(item => $"{item.ParentId}\u0000{item.Name}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var targetEnvironmentByName = targetEnvironments
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var decisionLookup = decisions
            .Where(item => !string.IsNullOrWhiteSpace(item.Kind) && !string.IsNullOrWhiteSpace(item.SourceId))
            .GroupBy(item => BuildDecisionKey(item.Kind, item.SourceId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Action, StringComparer.OrdinalIgnoreCase);
        var interfaceTargetIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var interfaceEndpointKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var interfaceEntities = new List<RequestCaseEntity>();
        var caseEntities = new List<RequestCaseEntity>();
        var environmentEntities = new List<ProjectEnvironmentEntity>();
        var variableEntities = new List<EnvironmentVariableEntity>();
        var endpoints = new List<ApiEndpointEntity>();
        var parameters = new List<RequestParameterEntity>();
        var endpointIdsToDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var documentIdsToDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var environmentIdsToClearVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedInterfaceNames = targetInterfaces
            .Select(item => BuildDeliveryRequestCaseBusinessKey(item.GroupName, item.FolderPath, item.ParentId, item.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedCaseNames = targetRequestCases
            .Select(item => BuildDeliveryRequestCaseBusinessKey(item.GroupName, item.FolderPath, item.ParentId, item.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var targetEnvironmentNames = targetEnvironments
            .Select(item => item.Name)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasTargetActiveEnvironment = targetEnvironments.Any(item => item.IsActive);
        var plannedActiveEnvironment = false;
        var addedCount = 0;
        var updatedCount = 0;
        var skippedCount = 0;
        var duplicatedCount = 0;
        var documentId = Guid.NewGuid().ToString("N");

        foreach (var sourceInterface in package.Interfaces)
        {
            var existingBySource = targetInterfaceBySource.GetValueOrDefault(sourceInterface.SourceId);
            var existingByBusiness = targetInterfaceByBusinessKey.GetValueOrDefault(
                BuildDeliveryInterfaceBusinessKey(sourceInterface.RequestSnapshot.Method, sourceInterface.RequestSnapshot.Url));
            var defaultAction = existingBySource is not null
                ? RequestCaseDeliveryDecisionActions.Update
                : existingByBusiness is not null
                    ? RequestCaseDeliveryDecisionActions.Skip
                    : RequestCaseDeliveryDecisionActions.Import;
            var action = ResolveDecision(decisionLookup, "interface", sourceInterface.SourceId, defaultAction);
            if (action == RequestCaseDeliveryDecisionActions.Skip)
            {
                skippedCount++;
                continue;
            }

            var isDuplicate = action == RequestCaseDeliveryDecisionActions.Duplicate;
            var targetInterface = action == RequestCaseDeliveryDecisionActions.Update
                ? existingBySource ?? existingByBusiness
                : null;
            var targetInterfaceId = targetInterface?.Id ?? Guid.NewGuid().ToString("N");
            // 只有覆盖更新才替换已有 endpoint；导入和另存必须保留目标项目原接口。
            var endpointMethod = targetInterface?.RequestSnapshot.Method
                                 ?? existingByBusiness?.RequestSnapshot.Method
                                 ?? sourceInterface.RequestSnapshot.Method;
            var endpointPath = targetInterface?.RequestSnapshot.Url
                               ?? existingByBusiness?.RequestSnapshot.Url
                               ?? sourceInterface.RequestSnapshot.Url;
            var endpointForSource = action == RequestCaseDeliveryDecisionActions.Update
                ? targetEndpoints.FirstOrDefault(item =>
                    string.Equals(BuildDeliveryInterfaceBusinessKey(item.Method, item.Path),
                        BuildDeliveryInterfaceBusinessKey(endpointMethod, endpointPath),
                        StringComparison.OrdinalIgnoreCase))
                : null;
            var endpointId = endpointForSource?.Id ?? Guid.NewGuid().ToString("N");
            if (endpointForSource is not null)
            {
                endpointIdsToDelete.Add(endpointForSource.Id);
                if (!string.IsNullOrWhiteSpace(endpointForSource.DocumentId))
                {
                    documentIdsToDelete.Add(endpointForSource.DocumentId);
                }
            }

            var endpointKey = BuildDeliveryEndpointKey(package.Source.ProjectId, sourceInterface.SourceId);
            var interfaceGroupName = string.IsNullOrWhiteSpace(sourceInterface.GroupName) ? "接口" : sourceInterface.GroupName;
            if (targetInterface is not null)
            {
                usedInterfaceNames.Remove(BuildDeliveryRequestCaseBusinessKey(
                    targetInterface.GroupName,
                    targetInterface.FolderPath,
                    targetInterface.ParentId,
                    targetInterface.Name));
            }
            var interfaceName = ResolveUniqueRequestCaseName(
                sourceInterface.Name,
                interfaceGroupName,
                sourceInterface.FolderPath,
                endpointKey,
                usedInterfaceNames,
                isDuplicate);
            interfaceTargetIds[sourceInterface.SourceId] = targetInterfaceId;
            interfaceEndpointKeys[sourceInterface.SourceId] = endpointKey;
            endpoints.Add(new ApiEndpointEntity
            {
                Id = endpointId,
                DocumentId = documentId,
                GroupName = NormalizeGroupName(sourceInterface.FolderPath, sourceInterface.GroupName),
                Name = interfaceName,
                Method = NormalizeMethod(sourceInterface.RequestSnapshot.Method),
                Path = NormalizePath(sourceInterface.RequestSnapshot.Url),
                Description = sourceInterface.Description,
                RequestBodyMode = sourceInterface.RequestSnapshot.BodyMode,
                RequestBodyTemplate = sourceInterface.RequestSnapshot.BodyContent
            });
            parameters.AddRange(BuildDeliveryParameters(endpointId, sourceInterface.RequestSnapshot));
            interfaceEntities.Add(new RequestCaseEntity
            {
                Id = targetInterfaceId,
                ProjectId = projectId,
                EntryType = "http-interface",
                Name = interfaceName,
                GroupName = interfaceGroupName,
                FolderPath = sourceInterface.FolderPath,
                ParentId = endpointKey,
                TagsJson = "[]",
                Description = sourceInterface.Description,
                RequestSnapshotJson = JsonSerializer.Serialize(new RequestSnapshotDto
                {
                    EndpointId = endpointKey,
                    Name = interfaceName,
                    Method = NormalizeMethod(sourceInterface.RequestSnapshot.Method),
                    Url = NormalizePath(sourceInterface.RequestSnapshot.Url),
                    Description = sourceInterface.RequestSnapshot.Description,
                    BodyMode = sourceInterface.RequestSnapshot.BodyMode,
                    BodyContent = sourceInterface.RequestSnapshot.BodyContent,
                    IgnoreSslErrors = sourceInterface.RequestSnapshot.IgnoreSslErrors,
                    QueryParameters = CloneKeyValues(sourceInterface.RequestSnapshot.QueryParameters),
                    PathParameters = CloneKeyValues(sourceInterface.RequestSnapshot.PathParameters),
                    Headers = CloneKeyValues(sourceInterface.RequestSnapshot.Headers)
                }, SerializerOptions),
                SourceProjectId = isDuplicate ? string.Empty : package.Source.ProjectId,
                SourceId = isDuplicate ? string.Empty : sourceInterface.SourceId,
                UpdatedAt = DateTime.UtcNow
            });

            if (isDuplicate)
            {
                duplicatedCount++;
            }
            else if (targetInterface is null)
            {
                addedCount++;
            }
            else
            {
                updatedCount++;
            }
        }

        foreach (var sourceCase in package.RequestCases)
        {
            var defaultAction = targetCaseBySource.ContainsKey(sourceCase.SourceId)
                ? RequestCaseDeliveryDecisionActions.Update
                : RequestCaseDeliveryDecisionActions.Import;
            interfaceTargetIds.TryGetValue(sourceCase.ParentInterfaceSourceId, out var targetParentId);
            if (string.IsNullOrWhiteSpace(targetParentId))
            {
                var orphanAction = ResolveDecision(decisionLookup, "request-case", sourceCase.SourceId, RequestCaseDeliveryDecisionActions.Skip);
                if (orphanAction == RequestCaseDeliveryDecisionActions.Skip)
                {
                    skippedCount++;
                    continue;
                }

                return DeliveryImportPlanResult.Failure(
                    $"请求用例“{sourceCase.Name}”未找到可用的父接口。",
                    "request_case_delivery_case_parent_missing");
            }

            var existingBySource = targetCaseBySource.GetValueOrDefault(sourceCase.SourceId);
            var existingByBusiness = targetCaseByBusinessKey.GetValueOrDefault($"{targetParentId}\u0000{sourceCase.Name}");
            if (existingBySource is null && existingByBusiness is not null)
            {
                defaultAction = RequestCaseDeliveryDecisionActions.Skip;
            }

            var action = ResolveDecision(decisionLookup, "request-case", sourceCase.SourceId, defaultAction);
            if (action == RequestCaseDeliveryDecisionActions.Skip)
            {
                skippedCount++;
                continue;
            }

            var isDuplicate = action == RequestCaseDeliveryDecisionActions.Duplicate;
            var targetCase = action == RequestCaseDeliveryDecisionActions.Update
                ? existingBySource ?? existingByBusiness
                : null;
            var parentEndpointKey = interfaceEndpointKeys[sourceCase.ParentInterfaceSourceId];
            var snapshot = CloneSnapshot(sourceCase.RequestSnapshot);
            snapshot = new RequestSnapshotDto
            {
                EndpointId = parentEndpointKey,
                Name = snapshot.Name,
                Method = snapshot.Method,
                Url = snapshot.Url,
                Description = snapshot.Description,
                BodyMode = snapshot.BodyMode,
                BodyContent = snapshot.BodyContent,
                IgnoreSslErrors = snapshot.IgnoreSslErrors,
                QueryParameters = snapshot.QueryParameters,
                PathParameters = snapshot.PathParameters,
                Headers = snapshot.Headers
            };
            var caseGroupName = string.IsNullOrWhiteSpace(sourceCase.GroupName) ? "用例" : sourceCase.GroupName;
            if (targetCase is not null)
            {
                usedCaseNames.Remove(BuildDeliveryRequestCaseBusinessKey(
                    targetCase.GroupName,
                    targetCase.FolderPath,
                    targetCase.ParentId,
                    targetCase.Name));
            }
            var caseName = ResolveUniqueRequestCaseName(
                sourceCase.Name,
                caseGroupName,
                sourceCase.FolderPath,
                targetParentId,
                usedCaseNames,
                isDuplicate);
            caseEntities.Add(new RequestCaseEntity
            {
                Id = targetCase?.Id ?? Guid.NewGuid().ToString("N"),
                ProjectId = projectId,
                EntryType = "http-case",
                Name = caseName,
                GroupName = caseGroupName,
                FolderPath = sourceCase.FolderPath,
                ParentId = targetParentId,
                TagsJson = JsonSerializer.Serialize(sourceCase.Tags, SerializerOptions),
                Description = sourceCase.Description,
                RequestSnapshotJson = JsonSerializer.Serialize(snapshot, SerializerOptions),
                SourceProjectId = isDuplicate ? string.Empty : package.Source.ProjectId,
                SourceId = isDuplicate ? string.Empty : sourceCase.SourceId,
                UpdatedAt = sourceCase.UpdatedAt == default ? DateTime.UtcNow : sourceCase.UpdatedAt
            });
            if (isDuplicate)
            {
                duplicatedCount++;
            }
            else if (targetCase is null)
            {
                addedCount++;
            }
            else
            {
                updatedCount++;
            }
        }

        foreach (var sourceEnvironment in package.Environments)
        {
            var existingBySource = targetEnvironmentBySource.GetValueOrDefault(sourceEnvironment.SourceId);
            var existingByName = targetEnvironmentByName.GetValueOrDefault(sourceEnvironment.Name);
            var defaultAction = existingBySource is not null
                ? RequestCaseDeliveryDecisionActions.Update
                : existingByName is not null
                    ? RequestCaseDeliveryDecisionActions.Skip
                    : RequestCaseDeliveryDecisionActions.Import;
            var action = ResolveDecision(decisionLookup, "environment", sourceEnvironment.SourceId, defaultAction);
            if (action == RequestCaseDeliveryDecisionActions.Skip)
            {
                skippedCount++;
                continue;
            }

            var isDuplicate = action == RequestCaseDeliveryDecisionActions.Duplicate;
            var targetEnvironment = action == RequestCaseDeliveryDecisionActions.Update
                ? existingBySource ?? existingByName
                : null;
            var environmentId = targetEnvironment?.Id ?? Guid.NewGuid().ToString("N");
            var environmentName = isDuplicate
                ? ResolveUniqueName(sourceEnvironment.Name, targetEnvironmentNames, forceCopySuffix: true)
                : sourceEnvironment.Name;
            targetEnvironmentNames.Add(environmentName);
            // 目标已有活动环境时保持目标状态；只有目标没有活动环境时才采用包内活动环境。
            var isActive = targetEnvironment?.IsActive == true
                           || (sourceEnvironment.IsActive && !hasTargetActiveEnvironment && !plannedActiveEnvironment);
            if (isActive)
            {
                plannedActiveEnvironment = true;
            }

            environmentEntities.Add(new ProjectEnvironmentEntity
            {
                Id = environmentId,
                ProjectId = projectId,
                Name = environmentName,
                BaseUrl = sourceEnvironment.BaseUrl.TrimEnd('/'),
                IsActive = isActive,
                SortOrder = sourceEnvironment.SortOrder,
                SourceProjectId = isDuplicate ? string.Empty : package.Source.ProjectId,
                SourceId = isDuplicate ? string.Empty : sourceEnvironment.SourceId,
                CreatedAt = targetEnvironment?.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            if (targetEnvironment is not null)
            {
                environmentIdsToClearVariables.Add(environmentId);
            }

            foreach (var variable in sourceEnvironment.Variables)
            {
                variableEntities.Add(new EnvironmentVariableEntity
                {
                    Id = Guid.NewGuid().ToString("N"),
                    EnvironmentId = environmentId,
                    EnvironmentName = environmentName,
                    Key = variable.Key,
                    Value = variable.Value,
                    IsEnabled = variable.IsEnabled
                });
            }

            if (isDuplicate)
            {
                duplicatedCount++;
            }
            else if (targetEnvironment is null)
            {
                addedCount++;
            }
            else
            {
                updatedCount++;
            }
        }

        var document = endpoints.Count == 0
            ? null
            : new ApiDocumentEntity
            {
                Id = documentId,
                ProjectId = projectId,
                Name = string.IsNullOrWhiteSpace(package.Source.ProjectName) ? Path.GetFileNameWithoutExtension(filePath) : package.Source.ProjectName,
                SourceType = "REQUEST_CASE_DELIVERY",
                SourceValue = filePath,
                BaseUrl = string.Empty,
                RawJson = rawJson,
                ImportedAt = DateTime.UtcNow
            };
        return DeliveryImportPlanResult.Success(
            new RequestCaseDeliveryImportPlan
            {
                ProjectId = projectId,
                Document = document,
                Endpoints = endpoints,
                Parameters = parameters,
                RequestCases = interfaceEntities.Concat(caseEntities).ToList(),
                Environments = environmentEntities,
                Variables = variableEntities,
                EndpointIdsToDelete = endpointIdsToDelete.ToList(),
                DocumentIdsToDelete = documentIdsToDelete.ToList(),
                EnvironmentIdsToClearVariables = environmentIdsToClearVariables.ToList()
            },
            new RequestCaseDeliveryResultDto
            {
                FilePath = filePath,
                InterfaceCount = package.Interfaces.Count,
                RequestCaseCount = package.RequestCases.Count,
                EnvironmentCount = package.Environments.Count,
                VariableCount = package.Environments.Sum(item => item.Variables.Count),
                AddedCount = addedCount,
                UpdatedCount = updatedCount,
                SkippedCount = skippedCount,
                DuplicatedCount = duplicatedCount
            });
    }

    private static IEnumerable<RequestParameterEntity> BuildDeliveryParameters(string endpointId, RequestSnapshotDto snapshot)
    {
        return snapshot.QueryParameters
            .Select(item => new { Item = item, Type = RequestParameterKind.Query })
            .Concat(snapshot.PathParameters.Select(item => new { Item = item, Type = RequestParameterKind.Path }))
            .Concat(snapshot.Headers.Select(item => new { Item = item, Type = RequestParameterKind.Header }))
            .Select(item => new RequestParameterEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                EndpointId = endpointId,
                ParameterType = item.Type.ToString(),
                Name = item.Item.Name,
                DefaultValue = item.Item.Value,
                Description = string.Empty,
                Required = false
            });
    }

    private static string BuildDeliveryEndpointKey(string sourceProjectId, string sourceId)
    {
        return $"delivery-import:{sourceProjectId}:{sourceId}";
    }

    private static string BuildDecisionKey(string kind, string sourceId)
    {
        return $"{kind}\u0000{sourceId}";
    }

    private static string ResolveDecision(
        IReadOnlyDictionary<string, string> decisions,
        string kind,
        string sourceId,
        string defaultAction)
    {
        if (!decisions.TryGetValue(BuildDecisionKey(kind, sourceId), out var action))
        {
            return defaultAction;
        }

        return action switch
        {
            RequestCaseDeliveryDecisionActions.Import => RequestCaseDeliveryDecisionActions.Import,
            RequestCaseDeliveryDecisionActions.Update => RequestCaseDeliveryDecisionActions.Update,
            RequestCaseDeliveryDecisionActions.Skip => RequestCaseDeliveryDecisionActions.Skip,
            RequestCaseDeliveryDecisionActions.Duplicate => RequestCaseDeliveryDecisionActions.Duplicate,
            _ => defaultAction
        };
    }

    private static string ResolveUniqueName(string name, ISet<string> existingNames, bool forceCopySuffix = false)
    {
        var baseName = string.IsNullOrWhiteSpace(name) ? "未命名" : name.Trim();
        if (!forceCopySuffix && !existingNames.Contains(baseName))
        {
            return baseName;
        }

        var candidate = $"{baseName}_copy";
        var suffix = 2;
        while (existingNames.Contains(candidate))
        {
            candidate = $"{baseName}_copy{suffix++}";
        }

        return candidate;
    }

    private static string NormalizeGroupName(string folderPath, string groupName)
    {
        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            return folderPath.Trim();
        }

        return string.IsNullOrWhiteSpace(groupName) ? string.Empty : groupName.Trim();
    }

    private sealed record DeliveryPackageLoadResult(
        bool IsSuccess,
        string Message,
        string ErrorCode,
        string RawJson,
        RequestCaseDeliveryPackageDto? Package)
    {
        public static DeliveryPackageLoadResult Success(string rawJson, RequestCaseDeliveryPackageDto package)
        {
            return new DeliveryPackageLoadResult(true, string.Empty, string.Empty, rawJson, package);
        }

        public static DeliveryPackageLoadResult Failure(string message, string errorCode)
        {
            return new DeliveryPackageLoadResult(false, message, errorCode, string.Empty, null);
        }
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
        var existingEndpointByKey = existingEndpoints
            .GroupBy(endpoint => BuildConflictKey(endpoint.Method, endpoint.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var conflicts = importedEndpoints
            .Select(item => new
            {
                Imported = item,
                Existing = existingEndpointByKey.GetValueOrDefault(BuildConflictKey(item.Method, item.Path))
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

    private static string BuildConflictKey(string method, string path)
    {
        return $"{method}\u0000{path}";
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

        // 未分组接口不再兜底到"默认模块"，导入后直接落在接口树根目录
        return string.Empty;
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

        // 与 Swagger 导入一致，限制数据包大小，防止误选超大文件撑爆内存
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Exists && fileInfo.Length > MaxProjectPackageFileBytes)
        {
            return PackageLoadResult.Failure(
                $"项目数据包文件过大（{fileInfo.Length / 1024.0 / 1024.0:F1} MB），上限为 {MaxProjectPackageFileBytes / 1024.0 / 1024.0:F0} MB。",
                "project_data_package_too_large");
        }

        var rawJson = await File.ReadAllTextAsync(filePath, cancellationToken);
        var package = await Task.Run(() => JsonSerializer.Deserialize<ProjectDataExportPackageDto>(rawJson, ImportSerializerOptions), cancellationToken);
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

    private sealed record DeliveryImportPlanResult(
        bool IsSuccess,
        string Message,
        string ErrorCode,
        RequestCaseDeliveryImportPlan? Plan,
        RequestCaseDeliveryResultDto? Result)
    {
        public static DeliveryImportPlanResult Success(
            RequestCaseDeliveryImportPlan plan,
            RequestCaseDeliveryResultDto result)
        {
            return new DeliveryImportPlanResult(true, string.Empty, string.Empty, plan, result);
        }

        public static DeliveryImportPlanResult Failure(string message, string errorCode)
        {
            return new DeliveryImportPlanResult(false, message, errorCode, null, null);
        }
    }

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
