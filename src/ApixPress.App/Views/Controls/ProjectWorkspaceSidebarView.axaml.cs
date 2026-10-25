using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Views.Controls;

public partial class ProjectWorkspaceSidebarView : UserControl
{
    public ProjectWorkspaceSidebarView()
    {
        InitializeComponent();
    }

    // 用例计数芯片：打开该接口的用例抽屉（对齐原型 08 树节点计数开抽屉）
    private void OnOpenUseCasesClick(object? sender, RoutedEventArgs e)
    {
        if (this.FindAncestorOfType<MainWindow>() is { } mainWindow)
        {
            e.Handled = true;
            mainWindow.OpenUseCasesDrawer((sender as Control)?.DataContext as ExplorerItemViewModel);
        }
    }
}
