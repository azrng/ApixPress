using ApixPress.App.Models.DTOs;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Interfaces;

public interface IProjectDataExportService
{
    Task<IResultModel<ProjectDataExportResultDto>> ExportAsync(ProjectDataExportRequestDto request, CancellationToken cancellationToken);
}
