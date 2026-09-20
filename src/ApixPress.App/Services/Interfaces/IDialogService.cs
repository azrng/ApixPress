using Avalonia.Controls;
using Ursa.Controls;

namespace ApixPress.App.Services.Interfaces;

/// <summary>
/// 应用模态弹层入口：统一通过 Ursa OverlayDialogHost 展示 StandardDialogControl 承载的内容视图，
/// 关闭由内容侧 DataContext（IDialogContext）驱动，结果以 DialogResult 异步返回。
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// 以模态对话框展示内容视图，等待关闭并返回结果。
    /// </summary>
    /// <param name="dataContext">内容视图与对话框控件的 DataContext；实现 IDialogContext 时由其驱动关闭与结果。</param>
    /// <param name="title">对话框 chrome 标题；传 null 时由内容自带标题区（配合 showCloseButton=false）。</param>
    /// <param name="canResize">是否允许拖拽调整对话框尺寸。</param>
    /// <param name="showCloseButton">是否显示 chrome 右上角关闭按钮。</param>
    Task<DialogResult> ShowDialogAsync<TView>(
        object dataContext,
        string? title = null,
        bool canResize = false,
        bool showCloseButton = true)
        where TView : Control, new();
}
