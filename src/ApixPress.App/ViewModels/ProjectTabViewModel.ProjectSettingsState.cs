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
    public string SelectedImportFileName => HasSelectedImportFile ? Path.GetFileName(SelectedImportFilePath) : ImportTexts.UnselectedFileName;
    public string SelectedImportFileSummary => HasSelectedImportFile
        ? SelectedImportFilePath
        : ImportTexts.UnselectedFileSummary;
    public bool HasImportedApiDocuments => ImportedApiDocuments.Count > 0;
    public bool ShowImportedApiDocumentsEmptyState => !IsImportDataBusy && !HasImportedApiDocuments;
    public bool ShowImportStatusInfo => ImportDataStatusState == ImportStatusStates.Info;
    public bool ShowImportStatusSuccess => ImportDataStatusState == ImportStatusStates.Success;
    public bool ShowImportStatusError => ImportDataStatusState == ImportStatusStates.Error;
    public string ImportedApiDocumentCountText => ImportedApiDocuments.Count.ToString();
    public string ProjectSettingsDescription => string.IsNullOrWhiteSpace(Project.Description)
        ? ProjectSettingsTexts.EmptyDescription
        : Project.Description;
    public string CurrentProjectSettingsTitle => IsProjectSettingsImportDataSelected ? ProjectSettingsTexts.ImportDataTitle : ProjectSettingsTexts.OverviewTitle;
    public string CurrentProjectSettingsSubtitle => IsProjectSettingsImportDataSelected
        ? ProjectSettingsTexts.ImportSubtitle
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

            return ImportTexts.FormatPendingOverwriteSummary(
                PendingImportPreview.NewEndpointCount,
                PendingImportPreview.ConflictCount);
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
                ImportTexts.OverwriteDetailPrefix
            };
            lines.AddRange(displayedConflicts);
            if (PendingImportPreview.ConflictItems.Count > displayedConflicts.Count)
            {
                lines.Add(ImportTexts.FormatAdditionalConflictItems(PendingImportPreview.ConflictItems.Count - displayedConflicts.Count));
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
