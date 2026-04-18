using CommunityToolkit.Mvvm.Input;

namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
    [RelayCommand]
    private void OpenProjectImportDialog()
    {
        SelectedWorkspaceSection = WorkspaceSections.ProjectSettings;
        SelectedProjectSettingsSection = ProjectSettingsSections.ImportData;
        SelectedImportDataMode = ImportDataModes.File;
        ClearPendingImportConfirmation();
        IsProjectImportDialogOpen = true;
        StatusMessage = ImportTexts.OpenDialogStatus;
        NotifyShellState();
    }

    [RelayCommand]
    private void CloseProjectImportDialog()
    {
        IsProjectImportDialogOpen = false;
        ClearPendingImportConfirmation();
        StatusMessage = ImportTexts.CloseDialogStatus;
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowImportFileMode()
    {
        SelectedImportDataMode = ImportDataModes.File;
        NotifyShellState();
    }

    [RelayCommand]
    private void ShowImportUrlMode()
    {
        SelectedImportDataMode = ImportDataModes.Url;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task PickSwaggerImportFileAsync()
    {
        if (IsImportDataBusy)
        {
            return;
        }

        var filePath = await _filePickerService.PickSwaggerJsonFileAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            SetImportDataStatus(ImportTexts.PickFileCancelledStatus, ImportStatusStates.Info);
            NotifyShellState();
            return;
        }

        SelectedImportFilePath = filePath;
        var selectedFileStatus = ImportTexts.FormatSelectedFileStatus(Path.GetFileName(filePath));
        SetImportDataStatus(selectedFileStatus, ImportStatusStates.Info);
        StatusMessage = selectedFileStatus;
        NotifyShellState();
    }

    [RelayCommand]
    private async Task ImportSwaggerFileAsync()
    {
        if (!HasSelectedImportFile)
        {
            SetImportDataStatus(ImportTexts.MissingFileStatus, ImportStatusStates.Error);
            StatusMessage = ImportTexts.MissingFileShellStatus;
            NotifyShellState();
            return;
        }

        await ImportSwaggerAsync(
            cancellationToken => _apiWorkspaceService.PreviewImportFromFileAsync(ProjectId, SelectedImportFilePath.Trim(), cancellationToken),
            cancellationToken => _apiWorkspaceService.ImportFromFileAsync(ProjectId, SelectedImportFilePath.Trim(), cancellationToken),
            document => ImportTexts.FormatFileImportSuccess(document.Name),
            ImportTexts.PreviewLocalBusyText,
            ImportTexts.ImportLocalBusyText);
    }

    [RelayCommand]
    private async Task ImportSwaggerUrlAsync()
    {
        var importTargetUrl = ImportUrl.Trim();
        if (string.IsNullOrWhiteSpace(importTargetUrl))
        {
            SetImportDataStatus(ImportTexts.MissingUrlStatus, ImportStatusStates.Error);
            StatusMessage = ImportTexts.MissingUrlShellStatus;
            NotifyShellState();
            return;
        }

        await ImportSwaggerAsync(
            cancellationToken => _apiWorkspaceService.PreviewImportFromUrlAsync(ProjectId, importTargetUrl, cancellationToken),
            cancellationToken => _apiWorkspaceService.ImportFromUrlAsync(ProjectId, importTargetUrl, cancellationToken),
            document => ImportTexts.FormatUrlImportSuccess(document.Name),
            ImportTexts.PreviewUrlBusyText,
            ImportTexts.ImportUrlBusyText);
    }

    [RelayCommand]
    private async Task RefreshImportedApiDocumentsAsync()
    {
        await LoadImportedDocumentsAsync();
        StatusMessage = HasImportedApiDocuments
            ? ImportTexts.FormatRefreshImportedDocumentsSuccess(ImportedApiDocuments.Count)
            : ImportTexts.EmptyRefreshStatus;
        NotifyShellState();
    }
}
