using Avalonia.Platform.Storage;
using ApixPress.App.Models.DTOs;
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
            Title = "选择 OpenAPI / Swagger / Postman JSON 文件",
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

    public async Task<string?> PickProjectDataPackageFileAsync(CancellationToken cancellationToken)
    {
        if (_windowHostService.MainWindow is null)
        {
            return null;
        }

        var files = await _windowHostService.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 ApixPress 项目数据包",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("ApixPress 项目数据包")
                {
                    Patterns = ["*.apixpkg.json"]
                }
            ]
        });

        cancellationToken.ThrowIfCancellationRequested();
        return files.FirstOrDefault() is { } file ? file.TryGetLocalPath() : null;
    }

    public async Task<string?> SaveProjectDataExportFileAsync(string suggestedFileName, CancellationToken cancellationToken)
    {
        if (_windowHostService.MainWindow is null)
        {
            return null;
        }

        var file = await _windowHostService.MainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出项目数据包",
            SuggestedFileName = suggestedFileName,
            ShowOverwritePrompt = true,
            DefaultExtension = "apixpkg.json",
            FileTypeChoices =
            [
                new FilePickerFileType("ApixPress 项目数据包")
                {
                    Patterns = ["*.apixpkg.json"]
                }
            ]
        });

        cancellationToken.ThrowIfCancellationRequested();
        return file?.TryGetLocalPath();
    }

    public async Task<string?> SaveHtmlDocumentFileAsync(string suggestedFileName, CancellationToken cancellationToken)
    {
        if (_windowHostService.MainWindow is null)
        {
            return null;
        }

        var file = await _windowHostService.MainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出 HTML 接口文档",
            SuggestedFileName = suggestedFileName,
            ShowOverwritePrompt = true,
            DefaultExtension = "html",
            FileTypeChoices =
            [
                new FilePickerFileType("HTML 文档")
                {
                    Patterns = ["*.html"]
                }
            ]
        });

        cancellationToken.ThrowIfCancellationRequested();
        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickRequestCaseDeliveryFileAsync(CancellationToken cancellationToken)
    {
        if (_windowHostService.MainWindow is null)
        {
            return null;
        }

        var files = await _windowHostService.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择请求用例交付包",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("ApixPress 请求用例交付包")
                {
                    Patterns = ["*.apixdelivery.json"]
                }
            ]
        });

        cancellationToken.ThrowIfCancellationRequested();
        return files.FirstOrDefault() is { } file ? file.TryGetLocalPath() : null;
    }

    public async Task<string?> SaveRequestCaseDeliveryFileAsync(string suggestedFileName, CancellationToken cancellationToken)
    {
        if (_windowHostService.MainWindow is null)
        {
            return null;
        }

        var file = await _windowHostService.MainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出请求用例交付包",
            SuggestedFileName = suggestedFileName,
            ShowOverwritePrompt = true,
            DefaultExtension = "apixdelivery.json",
            FileTypeChoices =
            [
                new FilePickerFileType("ApixPress 请求用例交付包")
                {
                    Patterns = ["*.apixdelivery.json"]
                }
            ]
        });

        cancellationToken.ThrowIfCancellationRequested();
        return file?.TryGetLocalPath();
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

    public async Task<IReadOnlyList<FormFieldFileDto>?> PickOpenFilesAsync(string title, bool allowMultiple, CancellationToken cancellationToken = default)
    {
        if (_windowHostService.MainWindow is null)
        {
            return null;
        }

        var files = await _windowHostService.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = allowMultiple
        });

        cancellationToken.ThrowIfCancellationRequested();
        if (files.Count == 0)
        {
            return null;
        }

        return files.Select(file =>
        {
            var path = file.TryGetLocalPath() ?? string.Empty;
            return new FormFieldFileDto
            {
                Path = path,
                Name = file.Name,
                SizeBytes = path.Length > 0 && File.Exists(path) ? new FileInfo(path).Length : 0
            };
        }).ToList();
    }
}
