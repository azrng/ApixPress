using Avalonia.Platform.Storage;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.DependencyInjection;

namespace ApixPress.App.Services.Implementations;

public sealed class FilePickerService : IFilePickerService, ITransientDependency
{
    private readonly IWindowHostService _windowHostService;

    public FilePickerService(IWindowHostService windowHostService)
    {
        _windowHostService = windowHostService;
    }

    public async Task<string?> PickSwaggerJsonFileAsync(CancellationToken cancellationToken)
    {
        if (_windowHostService.MainWindow is null)
        {
            return null;
        }

        var files = await _windowHostService.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 Swagger / OpenAPI JSON 文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("JSON 文档")
                {
                    Patterns = ["*.json"]
                }
            ]
        });

        cancellationToken.ThrowIfCancellationRequested();
        return files.FirstOrDefault() is { } file ? file.TryGetLocalPath() : null;
    }

    public async Task<string?> PickStorageDirectoryAsync(CancellationToken cancellationToken)
    {
        if (_windowHostService.MainWindow is null)
        {
            return null;
        }

        var folders = await _windowHostService.MainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择数据库存储目录",
            AllowMultiple = false
        });

        cancellationToken.ThrowIfCancellationRequested();
        return folders.FirstOrDefault() is { } folder ? folder.TryGetLocalPath() : null;
    }
}
