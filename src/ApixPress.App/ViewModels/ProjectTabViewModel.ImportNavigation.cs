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
        StatusMessage = "请选择 OpenAPI / Swagger 导入方式。";
        NotifyShellState();
    }

    [RelayCommand]
    private void CloseProjectImportDialog()
    {
        IsProjectImportDialogOpen = false;
        ClearPendingImportConfirmation();
        StatusMessage = "已返回导入数据页面。";
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
            SetImportDataStatus("未选择文件，当前保持原有导入配置。", ImportStatusStates.Info);
            NotifyShellState();
            return;
        }

        SelectedImportFilePath = filePath;
        SetImportDataStatus($"已选择 Swagger 文件：{Path.GetFileName(filePath)}", ImportStatusStates.Info);
        StatusMessage = $"已选择 Swagger 文件：{Path.GetFileName(filePath)}";
        NotifyShellState();
    }

    [RelayCommand]
    private async Task ImportSwaggerFileAsync()
    {
        if (!HasSelectedImportFile)
        {
            SetImportDataStatus("请先选择要导入的 Swagger/OpenAPI JSON 文件。", ImportStatusStates.Error);
            StatusMessage = "请先选择要导入的 Swagger 文件。";
            NotifyShellState();
            return;
        }

        await ImportSwaggerAsync(
            cancellationToken => _apiWorkspaceService.PreviewImportFromFileAsync(ProjectId, SelectedImportFilePath.Trim(), cancellationToken),
            cancellationToken => _apiWorkspaceService.ImportFromFileAsync(ProjectId, SelectedImportFilePath.Trim(), cancellationToken),
            document => $"Swagger 文件导入成功：{document.Name}");
    }

    [RelayCommand]
    private async Task ImportSwaggerUrlAsync()
    {
        var importTargetUrl = ImportUrl.Trim();
        if (string.IsNullOrWhiteSpace(importTargetUrl))
        {
            SetImportDataStatus("请输入 Swagger/OpenAPI 文档 URL。", ImportStatusStates.Error);
            StatusMessage = "请输入 Swagger 文档 URL。";
            NotifyShellState();
            return;
        }

        await ImportSwaggerAsync(
            cancellationToken => _apiWorkspaceService.PreviewImportFromUrlAsync(ProjectId, importTargetUrl, cancellationToken),
            cancellationToken => _apiWorkspaceService.ImportFromUrlAsync(ProjectId, importTargetUrl, cancellationToken),
            document => $"Swagger URL 导入成功：{document.Name}");
    }

    [RelayCommand]
    private async Task RefreshImportedApiDocumentsAsync()
    {
        await LoadImportedDocumentsAsync();
        StatusMessage = HasImportedApiDocuments
            ? $"已刷新已导入数据，共 {ImportedApiDocuments.Count} 份文档。"
            : "已刷新导入数据，当前项目还没有 Swagger 文档。";
        NotifyShellState();
    }
}
