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
}
