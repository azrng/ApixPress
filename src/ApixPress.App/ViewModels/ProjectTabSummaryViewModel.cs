using System.Collections.ObjectModel;
using System.Linq;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public class ProjectTabSummaryViewModel : ViewModelBase
{
    private readonly Func<ProjectWorkspaceItemViewModel> _getProject;
    private readonly Func<ProjectEnvironmentItemViewModel?> _getSelectedEnvironment;
    private readonly Func<RequestWorkspaceTabViewModel?> _getActiveWorkspaceTab;
    private readonly Func<ObservableCollection<RequestCaseItemViewModel>> _getSavedRequests;
    private readonly Func<ObservableCollection<RequestHistoryItemViewModel>> _getRequestHistory;
    private readonly Func<int> _getEnvironmentCount;
    private readonly Func<int> _getImportedDocumentCount;

    public ProjectTabSummaryViewModel(
        Func<ProjectWorkspaceItemViewModel> getProject,
        Func<ProjectEnvironmentItemViewModel?> getSelectedEnvironment,
        Func<RequestWorkspaceTabViewModel?> getActiveWorkspaceTab,
        Func<ObservableCollection<RequestCaseItemViewModel>> getSavedRequests,
        Func<ObservableCollection<RequestHistoryItemViewModel>> getRequestHistory,
        Func<int> getEnvironmentCount,
        Func<int> getImportedDocumentCount)
    {
        _getProject = getProject;
        _getSelectedEnvironment = getSelectedEnvironment;
        _getActiveWorkspaceTab = getActiveWorkspaceTab;
        _getSavedRequests = getSavedRequests;
        _getRequestHistory = getRequestHistory;
        _getEnvironmentCount = getEnvironmentCount;
        _getImportedDocumentCount = getImportedDocumentCount;
    }

    public string ProjectSettingsSidebarTitle => ProjectSettingsTexts.SidebarTitle;
    public string ProjectSettingsSidebarDescription => ProjectSettingsTexts.SidebarDescription;
    public string ProjectSettingsOverviewGroupTitle => ProjectSettingsTexts.OverviewGroupTitle;
    public string ProjectSettingsImportGroupTitle => ProjectSettingsTexts.ImportGroupTitle;
    public string ProjectSettingsImportDataTitle => ProjectSettingsTexts.ImportDataTitle;
    public string ProjectSettingsExportDataTitle => ProjectSettingsTexts.ExportDataTitle;
    public string ProjectSettingsOverviewCardTitle => ProjectSettingsTexts.OverviewCardTitle;
    public string ProjectSettingsOverviewCardDescription => ProjectSettingsTexts.OverviewCardDescription;
    public string ProjectSettingsImportCardTitle => ProjectSettingsTexts.ImportCardTitle;
    public string ProjectSettingsImportCardDescription => ProjectSettingsTexts.ImportCardDescription;
    public string ProjectSettingsImportFormatName => ProjectSettingsTexts.ImportFormatName;
    public string ProjectSettingsImportFormatDescription => ProjectSettingsTexts.ImportFormatDescription;
    public string ProjectSettingsImportPackageCardTitle => ProjectSettingsTexts.ImportPackageCardTitle;
    public string ProjectSettingsImportPackageCardDescription => ProjectSettingsTexts.ImportPackageCardDescription;
    public string ProjectSettingsImportPackageFormatName => ProjectSettingsTexts.ImportPackageFormatName;
    public string ProjectSettingsImportPackageFormatDescription => ProjectSettingsTexts.ImportPackageFormatDescription;
    public string ProjectSettingsExportCardTitle => ProjectSettingsTexts.ExportCardTitle;
    public string ProjectSettingsExportCardDescription => ProjectSettingsTexts.ExportCardDescription;
    public string ProjectSettingsExportFormatName => ProjectSettingsTexts.ExportFormatName;
    public string ProjectSettingsExportFormatDescription => ProjectSettingsTexts.ExportFormatDescription;

    public string TabTitle => _getProject().Name;
    public string ProjectSummary => string.IsNullOrWhiteSpace(_getProject().Description) ? "暂无项目备注" : _getProject().Description;
    public string CurrentEnvironmentLabel => _getSelectedEnvironment()?.Name ?? "未选择环境";
    public string CurrentBaseUrlText => string.IsNullOrWhiteSpace(_getSelectedEnvironment()?.BaseUrl)
        ? "当前环境暂未配置 BaseUrl"
        : _getSelectedEnvironment()!.BaseUrl;
    public bool HasEnvironmentContext => _getActiveWorkspaceTab() is { IsLandingTab: false };
    public bool HasSavedRequests => _getSavedRequests().Count > 0;
    public bool HasHistory => _getRequestHistory().Count > 0;
    public bool ShowHistoryEmptyState => !HasHistory;
    public bool IsQuickRequestEditor => _getActiveWorkspaceTab()?.IsQuickRequestTab ?? false;
    public bool IsHttpInterfaceEditor => _getActiveWorkspaceTab()?.IsHttpInterfaceTab ?? false;
    public bool IsRequestEditorOpen => _getActiveWorkspaceTab() is { IsLandingTab: false };
    public string SavedRequestCountText => _getSavedRequests().Count(item =>
        string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.QuickRequest, StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.SourceCase.EntryType, ProjectTabRequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase)).ToString();
    public string HistoryCountText => _getRequestHistory().Count.ToString();
    public string EnvironmentCountText => _getEnvironmentCount().ToString();
    public string CurrentEnvironmentSummaryText => ProjectSettingsTexts.FormatCurrentEnvironmentSummary(CurrentEnvironmentLabel);
    public string ImportedApiDocumentSummaryText => ProjectSettingsTexts.FormatImportedApiDocumentSummary(_getImportedDocumentCount());

    public void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(TabTitle));
        OnPropertyChanged(nameof(ProjectSummary));
        OnPropertyChanged(nameof(CurrentEnvironmentLabel));
        OnPropertyChanged(nameof(CurrentEnvironmentSummaryText));
        OnPropertyChanged(nameof(CurrentBaseUrlText));
        OnPropertyChanged(nameof(HasEnvironmentContext));
        OnPropertyChanged(nameof(HasSavedRequests));
        OnPropertyChanged(nameof(HasHistory));
        OnPropertyChanged(nameof(ShowHistoryEmptyState));
        OnPropertyChanged(nameof(ImportedApiDocumentSummaryText));
        OnPropertyChanged(nameof(IsQuickRequestEditor));
        OnPropertyChanged(nameof(IsHttpInterfaceEditor));
        OnPropertyChanged(nameof(IsRequestEditorOpen));
        OnPropertyChanged(nameof(SavedRequestCountText));
        OnPropertyChanged(nameof(HistoryCountText));
        OnPropertyChanged(nameof(EnvironmentCountText));
    }
}
