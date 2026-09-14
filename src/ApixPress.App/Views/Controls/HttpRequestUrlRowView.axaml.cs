using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ApixPress.App;

namespace ApixPress.App.Views.Controls;

public partial class HttpRequestUrlRowView : UserControl
{
    public HttpRequestUrlRowView()
    {
        InitializeComponent();
    }

    // 窗口失活时的“首击”（切换窗口后的第一次点击、自动化后台点击）伴随焦点/激活抖动，
    // Transient 弹层刚显示就会被光熄灭逻辑关掉；这里等窗口激活稳定后补一次显示。
    private void OnCaretButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Flyout: Flyout flyout } caretButton)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is not Window window || window.IsActive)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!flyout.IsOpen)
            {
                flyout.ShowAt(caretButton);
            }
        }, DispatcherPriority.Background);
    }

    // 「用例」按钮：打开用例管理抽屉（宿主窗口负责抽屉生命周期）
    private void OnOpenUseCasesClick(object? sender, RoutedEventArgs e)
    {
        if (this.FindAncestorOfType<MainWindow>() is { } mainWindow)
        {
            mainWindow.OpenUseCasesDrawer();
        }
    }
}
