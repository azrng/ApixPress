using System.Linq;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
    public bool IsProjectSettingsOverviewSelected => SelectedProjectSettingsSection == ProjectSettingsSections.Overview;
    public bool IsProjectSettingsImportDataSelected => SelectedProjectSettingsSection == ProjectSettingsSections.ImportData;
    public bool ShowProjectSettingsOverviewSection => IsProjectSettingsSection && IsProjectSettingsOverviewSelected;
    public bool ShowProjectSettingsImportDataSection => IsProjectSettingsSection && IsProjectSettingsImportDataSelected;
    public string ProjectSettingsDescription => string.IsNullOrWhiteSpace(Project.Description)
        ? ProjectSettingsTexts.EmptyDescription
        : Project.Description;
    public string CurrentProjectSettingsTitle => IsProjectSettingsImportDataSelected ? ProjectSettingsTexts.ImportDataTitle : ProjectSettingsTexts.OverviewTitle;
    public string CurrentProjectSettingsSubtitle => IsProjectSettingsImportDataSelected
        ? ProjectSettingsTexts.ImportSubtitle
        : string.Empty;

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
