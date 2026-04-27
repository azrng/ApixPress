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
    public const string ImportDataTitle = "导入数据";
    public const string ImportDescription = "这里可以导入 Swagger 文档。";
    public const string ImportSubtitle = "支持 Swagger 文件上传和 URL 导入，导入结果会持久化保存到当前项目。";
    public const string ImportCardTitle = "导入 API 数据";
    public const string ImportCardDescription = "请选择要导入的数据源格式";
    public const string ImportFormatName = "OpenAPI / Swagger";
    public const string ImportFormatDescription = "支持导入 OpenAPI 3.x 与 Swagger 2.0 的 JSON 或 YAML 文档";
    public const string ExportDescription = "这里可以导出当前项目中的接口与测试用例。";
    public const string ExportSubtitle = "将当前项目中的接口和测试用例导出为可复用的项目数据包。";
    public const string ExportCardTitle = "导出项目数据";
    public const string ExportCardDescription = "导出当前项目中的接口与测试用例";
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
}

internal static class ImportTexts
{
    public const string DialogTitle = "导入数据";
    public const string DataSourceFormatLabel = "数据源格式";
    public const string DataSourceFormatName = "OpenAPI/Swagger";
    public const string DialogNotice = "支持导入 OpenAPI 3.0、3.1 或 Swagger 2.0 数据格式的 JSON 或 YAML 文件。";
    public const string FileModeTitle = "上传文件";
    public const string UrlModeTitle = "URL 导入";
    public const string PickFileButton = "选择文件";
    public const string StartImportButton = "开始导入";
    public const string UrlDescription = "输入可直接访问的 OpenAPI / Swagger 文档地址。";
    public const string UrlWatermark = "https://example.com/swagger/v1/swagger.json";
    public const string ImportUrlButton = "导入 URL";
    public const string BusyOverlayDescription = "导入期间会暂时锁定当前对话框，完成后会自动展示结果。";
    public const string BusyProcessing = "正在处理 Swagger 导入...";
    public const string ExportBusyProcessing = "正在导出当前项目数据...";
    public const string BusyRefreshResult = "正在刷新导入结果...";
    public const string DefaultStatus = "可导入 Swagger 文档，或导出当前项目中的接口与测试用例数据包。";
    public const string EmptyStateStatus = "当前项目还没有导入 Swagger 数据，可先从文件或 URL 开始导入。";
    public const string EmptyRefreshStatus = "已刷新导入数据，当前项目还没有 Swagger 文档。";
    public const string OpenDialogStatus = "请选择 OpenAPI / Swagger 导入方式。";
    public const string CloseDialogStatus = "已返回导入数据页面。";
    public const string PickFileCancelledStatus = "未选择文件，当前保持原有导入配置。";
    public const string ExportCancelledStatus = "已取消导出项目数据。";
    public const string MissingFileStatus = "请先选择要导入的 Swagger/OpenAPI JSON 文件。";
    public const string MissingFileShellStatus = "请先选择要导入的 Swagger 文件。";
    public const string MissingUrlStatus = "请输入 Swagger/OpenAPI 文档 URL。";
    public const string MissingUrlShellStatus = "请输入 Swagger 文档 URL。";
    public const string PreviewFailureFallback = "Swagger 导入预检查失败，请检查文档格式后重试。";
    public const string ImportFailureFallback = "Swagger 导入失败，请检查文档格式后重试。";
    public const string UnexpectedFailureFallback = "Swagger 导入失败，应用已阻止异常继续扩散。";
    public const string RefreshAfterImportFailure = "导入结果已写入，但刷新接口列表时发生错误。";
    public const string ExportFailureFallback = "项目数据导出失败，请检查导出路径后重试。";
    public const string OverwriteCancelled = "已取消本次覆盖导入。";
    public const string OverwritePendingShellStatus = "检测到同路径接口，等待确认是否更新接口定义。";
    public const string OverwriteDetailPrefix = "同路径接口会更新为最新定义，当前已保存的用例不会因本次导入被自动删除。";
    public const string SourceTypeUrl = "URL 导入";
    public const string SourceTypeFile = "文件上传";
    public const string BaseUrlFallback = "未解析出 BaseUrl";
    public const string UnselectedFileName = "尚未选择 Swagger 文件";
    public const string UnselectedFileSummary = "请选择本地 Swagger/OpenAPI JSON 文件后再执行导入。";
    public const string PreviewLocalBusyText = "正在校验本地 Swagger 文件...";
    public const string ImportLocalBusyText = "正在导入本地 Swagger 文件...";
    public const string PreviewUrlBusyText = "正在获取并校验 Swagger URL...";
    public const string ImportUrlBusyText = "正在导入 Swagger URL...";

    public static string FormatSelectedFileStatus(string fileName)
    {
        return $"已选择 Swagger 文件：{fileName}";
    }

    public static string FormatFileImportSuccess(string documentName)
    {
        return $"Swagger 文件导入成功：{documentName}";
    }

    public static string FormatUrlImportSuccess(string documentName)
    {
        return $"Swagger URL 导入成功：{documentName}";
    }

    public static string FormatExportSuccess(int interfaceCount, int testCaseCount, string fileName)
    {
        return $"项目数据导出成功：{interfaceCount} 个接口、{testCaseCount} 个测试用例，已保存到 {fileName}";
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
