using ApixPress.App.Models.DTOs;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Interfaces;

public interface IProjectDataExportService
{
    Task<IResultModel<ProjectDataExportResultDto>> ExportAsync(ProjectDataExportRequestDto request, CancellationToken cancellationToken);

    /// <summary>读取项目级导出偏好（格式、备份四项范围与最近输出目录）；无记录时返回默认值。</summary>
    Task<IResultModel<ProjectDataExportSettingsDto>> LoadExportSettingsAsync(string projectId, CancellationToken cancellationToken);

    /// <summary>保存项目级导出偏好；不保存 token、密码等敏感数据。</summary>
    Task<IResultModel<bool>> SaveExportSettingsAsync(string projectId, ProjectDataExportSettingsDto settings, CancellationToken cancellationToken);

    /// <summary>按接口和请求用例选择生成独立交付包。</summary>
    Task<IResultModel<RequestCaseDeliveryResultDto>> ExportRequestCaseDeliveryAsync(
        RequestCaseDeliveryRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>读取当前项目的请求用例交付选择项和环境统计。</summary>
    Task<IResultModel<RequestCaseDeliveryOptionsDto>> GetRequestCaseDeliveryOptionsAsync(
        string projectId,
        CancellationToken cancellationToken);

    /// <summary>预览请求用例交付包，不产生目标项目写入。</summary>
    Task<IResultModel<RequestCaseDeliveryPreviewDto>> PreviewRequestCaseDeliveryAsync(
        string projectId,
        string filePath,
        CancellationToken cancellationToken);

    /// <summary>按预览决策在单一事务内导入请求用例交付包。</summary>
    Task<IResultModel<RequestCaseDeliveryResultDto>> ImportRequestCaseDeliveryAsync(
        string projectId,
        string filePath,
        IReadOnlyList<RequestCaseDeliveryDecisionDto> decisions,
        CancellationToken cancellationToken);

    Task<IResultModel<ApiImportPreviewDto>> PreviewImportPackageAsync(string projectId, string filePath, CancellationToken cancellationToken);

    Task<IResultModel<ApiDocumentDto>> ImportPackageAsync(string projectId, string filePath, CancellationToken cancellationToken);
}
