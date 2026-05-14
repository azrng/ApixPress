namespace ApixPress.App.Services.Interfaces;

public interface IFilePickerService
{
    Task<string?> PickSwaggerJsonFileAsync(CancellationToken cancellationToken);

    Task<string?> PickProjectDataPackageFileAsync(CancellationToken cancellationToken);

    Task<string?> SaveProjectDataExportFileAsync(string suggestedFileName, CancellationToken cancellationToken);

    Task<string?> PickStorageDirectoryAsync(CancellationToken cancellationToken);
}
