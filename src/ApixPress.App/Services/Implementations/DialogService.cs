using Avalonia.Controls;
using ApixPress.App.Helpers;
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

    public Task<DialogResult> ShowDialogAsync<TView>(
        object dataContext,
        string? title = null,
        bool canResize = false,
        bool showCloseButton = true)
        where TView : Control, new()
    {
        // 弹窗打开→关闭整段计时：宿主布局、内容物化与模态级联都在这段里
        return PerfLog.MeasureAsync($"弹窗·{title ?? typeof(TView).Name}", () =>
            ShowDialogCoreAsync<TView>(dataContext, title, canResize, showCloseButton));
    }

    private async Task<DialogResult> ShowDialogCoreAsync<TView>(
        object dataContext,
        string? title,
        bool canResize,
        bool showCloseButton)
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

        var view = new TView();
        // 统一弹层键盘交互：ESC 关闭、初始焦点、Enter 主按钮提交
        DialogInteractionAssistant.Attach(view);
        return await OverlayDialog.ShowStandardAsync(view, dataContext, null, options);
    }
}
