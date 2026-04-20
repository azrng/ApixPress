using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ProjectSettingsShellViewModel : ViewModelBase
{
    private static class Sections
    {
        public const string Overview = "overview";
        public const string ImportData = "import-data";
    }

    private readonly Action _showProjectSettingsWorkspace;
    private readonly Action _dismissImportDialog;
    private readonly Func<bool> _isProjectSettingsSection;
    private readonly Func<string> _getProjectDescription;
    private readonly Action<string> _setStatusMessage;
    private readonly Action _notifyShellState;

    public ProjectSettingsShellViewModel(
        Action showProjectSettingsWorkspace,
        Action dismissImportDialog,
        Func<bool> isProjectSettingsSection,
        Func<string> getProjectDescription,
        Action<string> setStatusMessage,
        Action notifyShellState)
    {
        _showProjectSettingsWorkspace = showProjectSettingsWorkspace;
        _dismissImportDialog = dismissImportDialog;
        _isProjectSettingsSection = isProjectSettingsSection;
        _getProjectDescription = getProjectDescription;
        _setStatusMessage = setStatusMessage;
        _notifyShellState = notifyShellState;
    }

    public bool IsOverviewSelected => SelectedSection == Sections.Overview;
    public bool IsImportDataSelected => SelectedSection == Sections.ImportData;
    public bool ShowOverviewSection => _isProjectSettingsSection() && IsOverviewSelected;
    public bool ShowImportDataSection => _isProjectSettingsSection() && IsImportDataSelected;
    public string ProjectDescription => string.IsNullOrWhiteSpace(_getProjectDescription())
        ? ProjectSettingsTexts.EmptyDescription
        : _getProjectDescription();
    public string CurrentTitle => IsImportDataSelected ? ProjectSettingsTexts.ImportDataTitle : ProjectSettingsTexts.OverviewTitle;
    public string CurrentSubtitle => IsImportDataSelected ? ProjectSettingsTexts.ImportSubtitle : string.Empty;

    [ObservableProperty]
    private string selectedSection = Sections.Overview;

    [RelayCommand]
    private void OpenWorkspace()
    {
        ShowOverviewInternal(ProjectSettingsTexts.OverviewDescription);
    }

    [RelayCommand]
    private void ShowOverview()
    {
        ShowOverviewInternal(ProjectSettingsTexts.OverviewDescription);
    }

    [RelayCommand]
    private void ShowImportData()
    {
        _showProjectSettingsWorkspace();
        SelectedSection = Sections.ImportData;
        _dismissImportDialog();
        _setStatusMessage(ProjectSettingsTexts.ImportDescription);
        _notifyShellState();
    }

    public void NotifyWorkspaceSectionChanged()
    {
        OnPropertyChanged(nameof(ShowOverviewSection));
        OnPropertyChanged(nameof(ShowImportDataSection));
    }

    public void NotifyProjectChanged()
    {
        OnPropertyChanged(nameof(ProjectDescription));
    }

    partial void OnSelectedSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsOverviewSelected));
        OnPropertyChanged(nameof(IsImportDataSelected));
        OnPropertyChanged(nameof(ShowOverviewSection));
        OnPropertyChanged(nameof(ShowImportDataSection));
        OnPropertyChanged(nameof(CurrentTitle));
        OnPropertyChanged(nameof(CurrentSubtitle));
    }

    private void ShowOverviewInternal(string statusMessage)
    {
        _showProjectSettingsWorkspace();
        SelectedSection = Sections.Overview;
        _dismissImportDialog();
        _setStatusMessage(statusMessage);
        _notifyShellState();
    }
}
