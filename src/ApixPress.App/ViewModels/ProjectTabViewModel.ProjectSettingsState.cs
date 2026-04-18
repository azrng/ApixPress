using System.Linq;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
    public bool IsProjectSettingsOverviewSelected => SelectedProjectSettingsSection == ProjectSettingsSections.Overview;
    public bool IsProjectSettingsImportDataSelected => SelectedProjectSettingsSection == ProjectSettingsSections.ImportData;
    public bool ShowProjectSettingsOverviewSection => IsProjectSettingsSection && IsProjectSettingsOverviewSelected;
    public bool ShowProjectSettingsImportDataSection => IsProjectSettingsSection && IsProjectSettingsImportDataSelected;
    public bool IsImportFileMode => SelectedImportDataMode == ImportDataModes.File;
    public bool IsImportUrlMode => SelectedImportDataMode == ImportDataModes.Url;
    public bool CanEditImportData => !IsImportDataBusy;
    public bool ShowProjectImportDialogStatus => ShowImportStatusInfo || ShowImportStatusSuccess || ShowImportStatusError;
    public bool HasSelectedImportFile => !string.IsNullOrWhiteSpace(SelectedImportFilePath);
    public string SelectedImportFileName => HasSelectedImportFile ? Path.GetFileName(SelectedImportFilePath) : "尚未选择 Swagger 文件";
    public string SelectedImportFileSummary => HasSelectedImportFile
        ? SelectedImportFilePath
        : "请选择本地 Swagger/OpenAPI JSON 文件后再执行导入。";
    public bool HasImportedApiDocuments => ImportedApiDocuments.Count > 0;
    public bool ShowImportedApiDocumentsEmptyState => !IsImportDataBusy && !HasImportedApiDocuments;
    public bool ShowImportStatusInfo => ImportDataStatusState == ImportStatusStates.Info;
    public bool ShowImportStatusSuccess => ImportDataStatusState == ImportStatusStates.Success;
    public bool ShowImportStatusError => ImportDataStatusState == ImportStatusStates.Error;
    public string ImportedApiDocumentCountText => ImportedApiDocuments.Count.ToString();
    public string ProjectSettingsDescription => string.IsNullOrWhiteSpace(Project.Description)
        ? "当前项目还没有补充备注，可在这里继续维护环境与工作区说明。"
        : Project.Description;
    public string CurrentProjectSettingsTitle => IsProjectSettingsImportDataSelected ? "导入数据" : "基本设置";
    public string CurrentProjectSettingsSubtitle => IsProjectSettingsImportDataSelected
        ? "支持 Swagger 文件上传和 URL 导入，导入结果会持久化保存到当前项目。"
        : string.Empty;
    public bool HasPendingImportPreview => PendingImportPreview is not null;
    public string PendingImportOverwriteTitle => PendingImportPreview?.DocumentName ?? string.Empty;
    public string PendingImportOverwriteSummary
    {
        get
        {
            if (PendingImportPreview is null)
            {
                return string.Empty;
            }

            return PendingImportPreview.NewEndpointCount > 0
                ? $"本次将新增 {PendingImportPreview.NewEndpointCount} 个接口，并更新 {PendingImportPreview.ConflictCount} 个同路径接口，已保存用例会保留。"
                : $"本次导入将更新当前项目中 {PendingImportPreview.ConflictCount} 个同路径接口，已保存用例会保留。";
        }
    }

    public string PendingImportOverwriteDetailText
    {
        get
        {
            if (PendingImportPreview is null || PendingImportPreview.ConflictItems.Count == 0)
            {
                return string.Empty;
            }

            var displayedConflicts = PendingImportPreview.ConflictItems
                .Take(5)
                .Select(item => $"{item.Method} {item.Path} 现有：{item.ExistingDocumentName} / {item.ExistingEndpointName} -> 导入：{item.ImportedEndpointName}")
                .ToList();
            var lines = new List<string>
            {
                "同路径接口会更新为最新定义，当前已保存的用例不会因本次导入被自动删除。"
            };
            lines.AddRange(displayedConflicts);
            if (PendingImportPreview.ConflictItems.Count > displayedConflicts.Count)
            {
                lines.Add($"另有 {PendingImportPreview.ConflictItems.Count - displayedConflicts.Count} 个重复接口未展开。");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    public bool HasPendingWorkspaceDeleteTarget => PendingDeleteWorkspaceItem is not null;
    public string PendingWorkspaceDeleteTitle => PendingDeleteWorkspaceItem?.Title ?? string.Empty;
    public string PendingWorkspaceDeleteDescription
    {
        get
        {
            if (PendingDeleteWorkspaceItem is null)
            {
                return string.Empty;
            }

            var count = ProjectWorkspaceTreeBuilder.CollectDeletableSourceCases(PendingDeleteWorkspaceItem)
                .Select(item => item.Id)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            return count <= 1
                ? "删除后无法恢复，请确认当前已不再需要。"
                : $"该节点下共 {count} 项内容会被一起删除，删除后无法恢复。";
        }
    }
}
