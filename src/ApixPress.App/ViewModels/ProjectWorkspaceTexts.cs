using ApixPress.App.Models.DTOs;

namespace ApixPress.App.ViewModels;

internal static class ProjectSettingsTexts
{
    public const string SidebarTitle = "项目设置";
    public const string SidebarDescription = "在这里管理项目设置、环境以及项目数据的导入导出入口。";
    public const string OverviewGroupTitle = "基本设置";
    public const string ImportGroupTitle = "数据管理";
    public const string ExportDataTitle = "导出数据";
    public const string OverviewTitle = "基本设置";
    public const string OverviewDescription = "这里可以查看当前项目的基本设置。";
    public const string OverviewCardTitle = "基本信息";
    public const string OverviewCardDescription = "在这里维护当前项目名称、项目 ID 和项目简介。";
    public const string EmptyDescription = "当前项目还没有补充备注，可在这里继续维护环境与工作区说明。";
    public const string DangerCardTitle = "危险操作";
    public const string DangerCardDescription = "这些操作会直接影响当前项目数据，请确认已导出或备份后再继续。";
    public const string DangerOperationStatus = "危险操作需要二次确认后才会执行。";
    public const string ProjectIdCopiedStatus = "项目 ID 已复制到剪贴板。";
    public const string ProjectIdCopyFailureStatus = "复制项目 ID 失败，请手动选择复制。";
    public const string ClearProjectDataTitle = "清空项目数据";
    public const string ClearProjectDataDescription = "删除当前项目的接口、用例、导入文档、环境变量和请求历史，保留项目名称与简介，并重建一个空的开发环境。";
    public const string ClearProjectDataAction = "清空项目数据";
    public const string ClearProjectDataPendingStatus = "等待确认清空当前项目数据。";
    public const string ClearProjectDataCancelledStatus = "已取消清空项目数据。";
    public const string ClearingProjectDataStatus = "正在清空当前项目数据...";
    public const string ClearProjectDataFailureFallback = "清空项目数据失败，请稍后重试。";
    public const string DeleteProjectTitle = "删除项目";
    public const string DeleteProjectDescription = "删除当前项目及其全部接口、用例、导入文档、环境配置和请求历史；删除后会关闭该项目标签。";
    public const string DeleteProjectAction = "删除项目";
    public const string DeleteProjectPendingStatus = "等待确认删除当前项目。";
    public const string DeleteProjectCancelledStatus = "已取消删除项目。";
    public const string DeletingProjectStatus = "正在删除当前项目...";
    public const string DeleteProjectFailureFallback = "删除项目失败，请稍后重试。";
    public const string ImportDataTitle = "导入数据";
    public const string ImportDescription = "这里可以导入 OpenAPI、Swagger、Postman 文档或 ApixPress 项目数据包。";
    public const string ImportSubtitle = "支持 OpenAPI / Swagger / Postman JSON 文件上传、URL 导入，以及导入 `.apixpkg.json` 项目数据包。";
    public const string ImportCardTitle = "导入 API 数据";
    public const string ImportCardDescription = "选择 JSON 文件或输入 URL，格式自动识别";
    public const string ImportFormatName = "OpenAPI / Swagger / Postman";
    public const string ImportFormatDescription = "自动识别 OpenAPI、Swagger 与 Postman Collection JSON 文档";
    public const string ImportPackageCardTitle = "导入项目数据包";
    public const string ImportPackageCardDescription = "导入 ApixPress 导出的接口与请求用例数据";
    public const string ImportPackageFormatName = "ApixPress 项目数据包";
    public const string ImportPackageFormatDescription = "支持导入 `.apixpkg.json` 文件，并恢复接口与请求用例";
    public const string ExportDescription = "这里可以导出当前项目中的接口与请求用例。";
    public const string ExportSubtitle = "将当前项目中的接口和请求用例导出为可复用的项目数据包。";
    public const string ExportCardTitle = "导出数据";
    public const string ExportCardDescription = "支持导出 ApixPress 备份用于迁移备份，或导出 HTML / Markdown 接口文档用于分享与归档。";
    public const string ExportFormatName = "ApixPress 项目数据包";
    public const string ExportFormatDescription = "导出为可读的 UTF-8 JSON 数据包，扩展名为 .apixpkg.json";

    public static string FormatCurrentEnvironmentSummary(string environmentLabel)
    {
        return $"当前环境：{environmentLabel}";
    }

    public static string FormatImportedApiDocumentSummary(int count)
    {
        return $"已导入：{count} 份文档";
    }

    public static string FormatClearProjectDataSuccess(string projectName)
    {
        return $"项目 {projectName} 的数据已清空。";
    }

    public static string FormatDeleteProjectSuccess(string projectName)
    {
        return $"项目 {projectName} 已删除。";
    }
}

internal static class ImportTexts
{
    public const string DialogTitle = "导入数据";
    public const string DataSourceFormatLabel = "数据源格式";
    public const string DataSourceFormatName = "OpenAPI / Swagger / Postman";
    public const string DialogNotice = "自动识别 OpenAPI、Swagger 与 Postman Collection JSON 文档。";
    public const string FileModeTitle = "上传文件";
    public const string UrlModeTitle = "URL 导入";
    public const string PickFileButton = "选择文件";
    public const string StartImportButton = "开始导入";
    public const string UrlDescription = "输入可直接访问的 OpenAPI / Swagger 文档地址。";
    public const string UrlWatermark = "https://example.com/swagger/v1/swagger.json";
    public const string ImportUrlButton = "导入 URL";
    public const string BusyOverlayDescription = "导入期间会暂时锁定当前对话框，完成后会自动展示结果。";
    public const string BusyProcessing = "正在处理 Swagger 导入...";
    public const string PackageBusyProcessing = "正在处理项目数据包导入...";
    public const string ExportBusyProcessing = "正在导出当前项目数据...";
    public const string ExportBackupButtonText = "导出项目数据";
    public const string ExportHtmlButtonText = "导出 HTML";
    public const string ExportMarkdownButtonText = "导出 Markdown";
    public const string BusyRefreshResult = "正在刷新导入结果...";
    public const string DefaultStatus = "可导入 OpenAPI、Swagger、Postman 文档或 `.apixpkg.json` 项目数据包。";
    public const string EmptyStateStatus = "当前项目还没有导入 API 文档，可先从文件或 URL 开始导入。";
    public const string EmptyRefreshStatus = "已刷新导入数据，当前项目还没有 API 文档。";
    public const string OpenDialogStatus = "请选择 OpenAPI、Swagger 或 Postman 导入方式。";
    public const string CloseDialogStatus = "已返回导入数据页面。";
    public const string PickFileCancelledStatus = "未选择文件，当前保持原有导入配置。";
    public const string PickPackageCancelledStatus = "未选择项目数据包文件。";
    public const string ExportCancelledStatus = "已取消导出项目数据。";
    public const string MissingFileStatus = "请先选择要导入的 OpenAPI、Swagger 或 Postman JSON 文件。";
    public const string MissingFileShellStatus = "请先选择要导入的 API 文档文件。";
    public const string MissingUrlStatus = "请输入 Swagger/OpenAPI 文档 URL。";
    public const string MissingUrlShellStatus = "请输入 Swagger 文档 URL。";
    public const string PreviewFailureFallback = "Swagger 导入预检查失败，请检查文档格式后重试。";
    public const string ImportFailureFallback = "Swagger 导入失败，请检查文档格式后重试。";
    public const string UnexpectedFailureFallback = "Swagger 导入失败，应用已阻止异常继续扩散。";
    public const string RefreshAfterImportFailure = "导入结果已写入，但刷新接口列表时发生错误。";
    public const string PackagePreviewFailureFallback = "项目数据包导入预检查失败，请检查文件格式后重试。";
    public const string PackageImportFailureFallback = "项目数据包导入失败，请检查文件格式后重试。";
    public const string PackageUnexpectedFailureFallback = "项目数据包导入失败，应用已阻止异常继续扩散。";
    public const string ExportFailureFallback = "项目数据导出失败，请检查导出路径后重试。";
    public const string OverwriteCancelled = "已取消本次覆盖导入。";
    public const string OverwritePendingShellStatus = "检测到同路径接口，等待确认是否更新接口定义。";
    public const string PackageOverwritePendingShellStatus = "检测到同路径接口，等待确认是否导入项目数据包中的最新定义。";
    public const string OverwriteDetailPrefix = "同路径接口会更新为最新定义，当前已保存的用例不会因本次导入被自动删除。";
    public const string SourceTypeUrl = "URL 导入";
    public const string SourceTypeFile = "文件上传";
    public const string SourceTypePostman = "Postman 导入";
    public const string SourceTypeProjectPackage = "项目数据包";
    public const string BaseUrlFallback = "未解析出 BaseUrl";
    public const string BaseUrlSyncedSuffix = "；已将文档 BaseUrl 同步到当前环境";
    public const string UnselectedFileName = "尚未选择 API 文档文件";
    public const string UnselectedFileSummary = "请选择本地 OpenAPI、Swagger 或 Postman JSON 文件后再执行导入。";
    public const string PreviewLocalBusyText = "正在识别并校验本地 API 文档...";
    public const string ImportLocalBusyText = "正在导入本地 API 文档...";
    public const string PreviewUrlBusyText = "正在获取并校验 Swagger URL...";
    public const string ImportUrlBusyText = "正在导入 Swagger URL...";
    public const string PreviewPackageBusyText = "正在校验项目数据包...";
    public const string ImportPackageBusyText = "正在导入项目数据包...";

    public static string FormatSelectedFileStatus(string fileName)
    {
        return $"已选择 API 文档文件：{fileName}";
    }

    public static string FormatFileImportSuccess(string documentName)
    {
        return $"API 文档文件导入成功：{documentName}";
    }

    public static string FormatUrlImportSuccess(string documentName)
    {
        return $"Swagger URL 导入成功：{documentName}";
    }

    public static string FormatPackageImportSuccess(string documentName)
    {
        return $"项目数据包导入成功：{documentName}";
    }

    public static string FormatExportSuccess(ProjectDataExportResultDto result)
    {
        var targetName = Path.GetFileName(result.OutputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(targetName))
        {
            targetName = result.OutputPath;
        }

        return result.Format switch
        {
            ProjectDataExportFormat.Html => $"HTML 文档导出成功：{result.InterfaceCount} 个接口、{result.TestCaseCount} 个请求用例示例，已保存到 {targetName}",
            ProjectDataExportFormat.Markdown => $"Markdown 文档导出成功：{result.InterfaceCount} 个接口、{result.FileCount} 个文件，已保存到目录 {targetName}",
            _ => $"项目数据导出成功：{result.InterfaceCount} 个接口、{result.TestCaseCount} 个请求用例、{result.EnvironmentCount} 个环境、{result.HistoryCount} 条请求历史，已保存到 {targetName}"
        };
    }

    public static string FormatRefreshImportedDocumentsSuccess(int count)
    {
        return $"已刷新已导入数据，共 {count} 份文档。";
    }

    public static string FormatOverwriteDetectedStatus(int conflictCount)
    {
        return $"检测到 {conflictCount} 个同路径接口，确认后将更新接口定义，已保存用例会保留。";
    }

    public static string FormatPendingOverwriteSummary(int newEndpointCount, int conflictCount)
    {
        return newEndpointCount > 0
            ? $"本次将新增 {newEndpointCount} 个接口，并更新 {conflictCount} 个同路径接口，已保存用例会保留。"
            : $"本次导入将更新当前项目中 {conflictCount} 个同路径接口，已保存用例会保留。";
    }

    public static string FormatAdditionalConflictItems(int remainingCount)
    {
        return $"另有 {remainingCount} 个重复接口未展开。";
    }
}
