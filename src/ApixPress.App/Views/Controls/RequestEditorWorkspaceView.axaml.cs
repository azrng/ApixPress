using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using ApixPress.App.ViewModels;
using ApixPress.App.Helpers;

namespace ApixPress.App.Views.Controls;

public partial class RequestEditorWorkspaceView : UserControl
{
    private ProjectTabViewModel? _viewModel;
    private bool _isSubscribed;

    public RequestEditorWorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            Unsubscribe();
            _viewModel = DataContext as ProjectTabViewModel;
            EnsureSubscribed();
        };
        AttachedToVisualTree += (_, _) => EnsureSubscribed();
        DetachedFromVisualTree += (_, _) => Unsubscribe();
    }

    private void EnsureSubscribed()
    {
        if (_isSubscribed || _viewModel is null)
        {
            return;
        }

        _viewModel.Editor.PropertyChanged += OnEditorPropertyChanged;
        _isSubscribed = true;
        if (Dispatcher.UIThread.CheckAccess())
        {
            RefreshLazyHosts();
        }

        // 非 UI 线程（仅测试场景）：不投递也不触碰控件，属性变化到达 UI 线程后会自动补建
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed || _viewModel is null)
        {
            return;
        }

        _viewModel.Editor.PropertyChanged -= OnEditorPropertyChanged;
        _isSubscribed = false;
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshLazyHosts(e.PropertyName);
    }

    private void RefreshLazyHosts(string? propertyName = null)
    {
        if (_viewModel is null || !_isSubscribed)
        {
            return;
        }

        if (propertyName is null or nameof(ProjectRequestEditorViewModel.IsQuickRequestEditor)
            && _viewModel.Editor.IsQuickRequestEditor && QuickRequestWorkbenchHost.Content is null)
        {
            QuickRequestWorkbenchHost.Content = PerfLog.Measure("右侧工作台·快捷请求创建", () => new QuickRequestWorkbenchView());
        }

        if (propertyName is null or nameof(ProjectRequestEditorViewModel.ShowHttpWorkbenchContent)
            && _viewModel.Editor.ShowHttpWorkbenchContent && HttpWorkbenchHost.Content is null)
        {
            HttpWorkbenchHost.Content = PerfLog.Measure("右侧工作台·HTTP 创建", () => new HttpInterfaceWorkbenchView());
        }

        if (propertyName is null or nameof(ProjectRequestEditorViewModel.ShowHttpDocumentPreviewContent)
            && _viewModel.Editor.ShowHttpDocumentPreviewContent && HttpDocumentHost.Content is null)
        {
            HttpDocumentHost.Content = PerfLog.Measure("右侧工作台·文档创建", () => new HttpDocumentWorkspaceView());
        }
    }
}
