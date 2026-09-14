using Avalonia.Controls;
using ApixPress.App.Services.Interfaces;
using Ursa.Controls;
using Ursa.Controls.Options;

namespace ApixPress.App.Services.Implementations;

/// <summary>
/// Ursa OverlayDialog 弹层实现：复用主窗口 OverlayDialogHost（按 TopLevelHashCode 定位），
/// 按钮区交给内容自带（Buttons.None），遮罩点击不关闭（保持与旧手写弹窗一致的行为）。
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly IWindowHostService _windowHostService;

    public DialogService(IWindowHostService windowHostService)
    {
        _windowHostService = windowHostService;
    }

    public async Task<DialogResult> ShowDialogAsync<TView>(
        object dataContext,
        string? title = null,
        bool canResize = false,
        bool showCloseButton = true)
        where TView : Control, new()
    {
        var options = new OverlayDialogOptions
        {
            Title = title,
            Buttons = DialogButton.None,
            CanResize = canResize,
            IsCloseButtonVisible = showCloseButton,
            CanLightDismiss = false,
            CanDragMove = true,
            TopLevelHashCode = _windowHostService.MainWindow?.GetHashCode()
        };

        return await OverlayDialog.ShowStandardAsync(new TView(), dataContext, null, options);
    }
}
