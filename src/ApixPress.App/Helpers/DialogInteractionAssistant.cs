using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Irihi.Avalonia.Shared.Contracts;

namespace ApixPress.App.Helpers;

/// <summary>
/// 弹层键盘与焦点交互统一挂载：ESC 关闭（走 DataContext 的 IDialogContext 通道，Ursa 宿主已订阅该事件）、
/// 打开后聚焦第一个可编辑输入框并全选、Enter 触发 DialogPrimaryButton 主按钮。
/// 由 DialogService 与抽屉宿主调用，避免逐弹窗重复实现。
/// </summary>
public static class DialogInteractionAssistant
{
    /// <summary>危险确认类弹层在内容根节点挂该类，禁用 Enter 默认提交。</summary>
    public const string NoDefaultSubmitClass = "DialogNoDefaultSubmit";

    public const string PrimaryButtonClass = "DialogPrimaryButton";

    public static void Attach(Control content)
    {
        content.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        content.AttachedToVisualTree += OnContentAttachedToVisualTree;
    }

    private static void OnContentAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not Control content)
        {
            return;
        }

        content.AttachedToVisualTree -= OnContentAttachedToVisualTree;
        // 延迟到弹层布局完成后设焦点，避免被 Ursa 宿主自身的焦点处理覆盖
        Dispatcher.UIThread.Post(() => FocusInitialInput(content), DispatcherPriority.Loaded);
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control content)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            // DataContext 未实现 IDialogContext 时不拦截，让事件继续传播
            if (content.DataContext is IDialogContext dialogContext)
            {
                e.Handled = true;
                dialogContext.Close();
            }

            return;
        }

        if (e.Key is not (Key.Enter or Key.Return)
            || e.KeyModifiers != KeyModifiers.None
            || HasNoDefaultSubmit(content))
        {
            return;
        }

        // 多行输入、代码编辑器与下拉展开状态中的 Enter 是控件自身操作，不触发主按钮
        if (e.Source is TextBox { AcceptsReturn: true }
            || IsWithinComboBox(e.Source as Control))
        {
            return;
        }

        var primary = FindPrimaryButton(content);
        if (primary is null)
        {
            return;
        }

        e.Handled = true;
        if (primary.Command?.CanExecute(primary.CommandParameter) == true)
        {
            primary.Command.Execute(primary.CommandParameter);
        }
    }

    private static bool IsWithinComboBox(Control? source)
    {
        for (var current = source; current is not null; current = current.GetVisualParent() as Control)
        {
            if (current is ComboBox)
            {
                return true;
            }
        }

        return false;
    }

    // 标记可能挂在内容根或内部容器上，两种位置都识别
    private static bool HasNoDefaultSubmit(Control content)
    {
        return content.Classes.Contains(NoDefaultSubmitClass)
            || content.GetVisualDescendants()
                .OfType<Control>()
                .Any(control => control.Classes.Contains(NoDefaultSubmitClass));
    }

    private static Button? FindPrimaryButton(Control content)
    {
        return content.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => button.Classes.Contains(PrimaryButtonClass)
                && button.IsVisible
                && button.IsEffectivelyEnabled);
    }

    private static void FocusInitialInput(Control content)
    {
        var input = content.GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(textBox => textBox.IsVisible
                && textBox.IsEffectivelyEnabled
                && !textBox.IsReadOnly);
        if (input is not null)
        {
            input.Focus();
            input.SelectAll();
            return;
        }

        // 无输入框的弹层把焦点给非主按钮（通常是取消），避免 Enter/Space 直接命中确认动作
        content.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => button.IsVisible
                && button.IsEffectivelyEnabled
                && !button.Classes.Contains(PrimaryButtonClass))
            ?.Focus();
    }
}
