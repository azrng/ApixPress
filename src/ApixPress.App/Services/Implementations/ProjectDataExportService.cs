using System.Text;
using System.Text.Json;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.DependencyInjection;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Implementations;

public sealed class ProjectDataExportService : IProjectDataExportService, ITransientDependency
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly IRequestCaseService _requestCaseService;

    public ProjectDataExportService(IRequestCaseService requestCaseService)
    {
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
            await File.WriteAllTextAsync(request.OutputFilePath, json, new UTF8Encoding(false), cancellationToken);
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
}
