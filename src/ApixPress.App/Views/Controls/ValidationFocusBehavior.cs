using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace ApixPress.App.Views.Controls;

/// <summary>提交失败后聚焦有错误的输入控件，避免在视图模型中操作视图。</summary>
public sealed class ValidationFocusBehavior : AvaloniaObject
{
    public static readonly AttachedProperty<bool> HasErrorProperty =
        AvaloniaProperty.RegisterAttached<ValidationFocusBehavior, Control, bool>("HasError");

    static ValidationFocusBehavior()
    {
        HasErrorProperty.Changed.AddClassHandler<Control>((control, change) =>
        {
            if (change.NewValue is true)
                Dispatcher.UIThread.Post(() => { if (GetHasError(control)) control.Focus(); });
        });
    }

    public static bool GetHasError(Control control) => control.GetValue(HasErrorProperty);
    public static void SetHasError(Control control, bool value) => control.SetValue(HasErrorProperty, value);
}
