using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ApixPress.App.Views.Controls;

public partial class HttpInterfaceHeadersTabView : UserControl
{
    private const string HeaderNameInputClass = "HeaderNameInput";

    public HttpInterfaceHeadersTabView()
    {
        InitializeComponent();

        // 对齐原型 04（T124）：AutoCompleteBox 默认只在输入或按 ↓ 时展开，空的 Header 名被点击时也展开全部内置候选。
        // 在宿主统一监听，避免逐行挂事件；内部 TextBox 会把按下标记为已处理，所以需要 handledEventsToo
        AddHandler(PointerPressedEvent, OnHeaderNamePointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHeaderButton.Click += OnAddHeaderClick;
    }

    private void OnHeaderNamePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // 只认 Header 名输入框本体：下拉项位于弹层内，向上找不到模板里的 TextBox，天然排除。
        // 下拉已开时点输入框会先被光熄灭关闭，这里在同一次按下里重新展开，表现为保持展开、不闪烁
        var textBox = (e.Source as Visual)?.FindAncestorOfType<TextBox>(includeSelf: true);
        if (textBox?.TemplatedParent is AutoCompleteBox autoComplete
            && autoComplete.Classes.Contains(HeaderNameInputClass)
            && string.IsNullOrEmpty(autoComplete.Text)
            && !autoComplete.IsDropDownOpen)
        {
            autoComplete.SetCurrentValue(AutoCompleteBox.IsDropDownOpenProperty, true);
        }
    }

    private void OnAddHeaderClick(object? sender, RoutedEventArgs e)
    {
        // Click 先于 Command 触发：投递到命令追加新行之后再聚焦，用户可直接输入或点开候选
        Dispatcher.UIThread.Post(FocusLastHeaderName);
    }

    private void FocusLastHeaderName()
    {
        var lastIndex = HeaderList.ItemCount - 1;
        if (lastIndex < 0)
        {
            return;
        }

        // 首行添加时列表刚由隐藏转为可见，先同步执行一次布局，让新行容器与模板实例化
        HeaderList.UpdateLayout();
        HeaderList.ScrollIntoView(lastIndex);
        HeaderList.ContainerFromIndex(lastIndex)?
            .GetVisualDescendants()
            .OfType<AutoCompleteBox>()
            .FirstOrDefault()?
            .Focus();
    }
}
