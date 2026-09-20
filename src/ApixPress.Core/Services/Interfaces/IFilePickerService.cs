using ApixPress.App.Models.DTOs;

namespace ApixPress.App.Services.Interfaces;

public interface IFilePickerService
{
    Task<string?> PickSwaggerJsonFileAsync(CancellationToken cancellationToken);

    Task<string?> PickProjectDataPackageFileAsync(CancellationToken cancellationToken);

    Task<string?> SaveProjectDataExportFileAsync(string suggestedFileName, CancellationToken cancellationToken);

    Task<string?> PickStorageDirectoryAsync(CancellationToken cancellationToken);

    /// <summary>选择一个或多个本地文件，返回路径与大小元信息；取消或主窗口不可用时返回 null。</summary>
    Task<IReadOnlyList<FormFieldFileDto>?> PickOpenFilesAsync(string title, bool allowMultiple, CancellationToken cancellationToken = default);
}
