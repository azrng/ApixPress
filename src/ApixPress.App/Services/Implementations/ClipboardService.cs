using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.DependencyInjection;

namespace ApixPress.App.Services.Implementations;

public sealed class ClipboardService : IClipboardService, ISingletonDependency
{
    private readonly IWindowHostService _windowHostService;

    public ClipboardService(IWindowHostService windowHostService)
    {
        _windowHostService = windowHostService;
    }

    public async Task<bool> CopyTextAsync(string text, CancellationToken cancellationToken = default)
    {
        var window = _windowHostService.MainWindow;
        var clipboard = window is null ? null : TopLevel.GetTopLevel(window)?.Clipboard;
        if (clipboard is null)
        {
            return false;
        }

        // Avalonia 12 移除了 SetTextAsync，改用 DataFormat.Text 写入
        await clipboard.SetValueAsync(DataFormat.Text, text ?? string.Empty);
        return true;
    }
}
