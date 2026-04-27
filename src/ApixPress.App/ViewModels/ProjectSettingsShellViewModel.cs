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
        public const string ExportData = "export-data";
    }

    private readonly Action _showProjectSettingsWorkspace;
    private readonly Action _dismissImportDialog;
    private readonly Func<bool> _isProjectSettingsSection;
    private readonly Func<string> _getProjectDescription;
    private readonly Func<Task> _ensureImportedDocumentsLoadedAsync;
    private readonly Action<string> _setStatusMessage;
    private readonly Action _notifyShellState;

    public ProjectSettingsShellViewModel(
        Action showProjectSettingsWorkspace,
        Action dismissImportDialog,
        Func<bool> isProjectSettingsSection,
        Func<string> getProjectDescription,
        Func<Task> ensureImportedDocumentsLoadedAsync,
        Action<string> setStatusMessage,
        Action notifyShellState)
    {
        _showProjectSettingsWorkspace = showProjectSettingsWorkspace;
        _dismissImportDialog = dismissImportDialog;
        _isProjectSettingsSection = isProjectSettingsSection;
        _getProjectDescription = getProjectDescription;
        _ensureImportedDocumentsLoadedAsync = ensureImportedDocumentsLoadedAsync;
        _setStatusMessage = setStatusMessage;
        _notifyShellState = notifyShellState;
    }

    public bool IsOverviewSelected => SelectedSection == Sections.Overview;
    public bool IsImportDataSelected => SelectedSection == Sections.ImportData;
    public bool IsExportDataSelected => SelectedSection == Sections.ExportData;
    public bool ShowOverviewSection => _isProjectSettingsSection() && IsOverviewSelected;
    public bool ShowImportDataSection => _isProjectSettingsSection() && IsImportDataSelected;
    public bool ShowExportDataSection => _isProjectSettingsSection() && IsExportDataSelected;
    public string ProjectDescription => string.IsNullOrWhiteSpace(_getProjectDescription())
        ? ProjectSettingsTexts.EmptyDescription
        : _getProjectDescription();
    public string CurrentTitle => SelectedSection switch
    {
        Sections.ImportData => ProjectSettingsTexts.ImportDataTitle,
        Sections.ExportData => ProjectSettingsTexts.ExportDataTitle,
        _ => ProjectSettingsTexts.OverviewTitle
    };
    public string CurrentSubtitle => SelectedSection switch
    {
        Sections.ImportData => ProjectSettingsTexts.ImportSubtitle,
        Sections.ExportData => ProjectSettingsTexts.ExportSubtitle,
        _ => string.Empty
    };

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
    private async Task ShowImportDataAsync()
    {
        _showProjectSettingsWorkspace();
        SelectedSection = Sections.ImportData;
        _dismissImportDialog();
        await _ensureImportedDocumentsLoadedAsync();
        _setStatusMessage(ProjectSettingsTexts.ImportDescription);
        _notifyShellState();
    }

    [RelayCommand]
    private void ShowExportData()
    {
        _showProjectSettingsWorkspace();
        SelectedSection = Sections.ExportData;
        _dismissImportDialog();
        _setStatusMessage(ProjectSettingsTexts.ExportDescription);
        _notifyShellState();
    }

    public void NotifyWorkspaceSectionChanged()
    {
        OnPropertyChanged(nameof(ShowOverviewSection));
        OnPropertyChanged(nameof(ShowImportDataSection));
        OnPropertyChanged(nameof(ShowExportDataSection));
    }

    public void NotifyProjectChanged()
    {
        OnPropertyChanged(nameof(ProjectDescription));
    }

    partial void OnSelectedSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsOverviewSelected));
        OnPropertyChanged(nameof(IsImportDataSelected));
        OnPropertyChanged(nameof(IsExportDataSelected));
        OnPropertyChanged(nameof(ShowOverviewSection));
        OnPropertyChanged(nameof(ShowImportDataSection));
        OnPropertyChanged(nameof(ShowExportDataSection));
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
