using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using ApixPress.App.ViewModels;

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
            _viewModel = DataContext as ProjectTabViewModel;
            Unsubscribe();
            EnsureSubscribed();
        };
        AttachedToVisualTree += (_, _) => EnsureSubscribed();
        DetachedFromVisualTree += (_, _) => Unsubscribe();
    }

    /// <summary>编辑器属性名 -> 工作台宿主。首次可见才实例化对应工作台。</summary>
    private static (string PropertyName, ContentControl Host, Func<Control> Factory)[] ResolveHosts(
        RequestEditorWorkspaceView view) =>
    [
        ("IsQuickRequestEditor", view.QuickRequestWorkbenchHost, () => new QuickRequestWorkbenchView()),
        ("ShowHttpWorkbenchContent", view.HttpWorkbenchHost, () => new HttpInterfaceWorkbenchView()),
        ("ShowHttpDocumentPreviewContent", view.HttpDocumentHost, () => new HttpDocumentWorkspaceView())
    ];

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

        foreach (var (propertyName2, host, factory) in ResolveHosts(this))
        {
            if (host.Content is not null)
            {
                continue;
            }

            var visible = propertyName2 switch
            {
                "IsQuickRequestEditor" => _viewModel.Editor.IsQuickRequestEditor,
                "ShowHttpWorkbenchContent" => _viewModel.Editor.ShowHttpWorkbenchContent,
                _ => _viewModel.Editor.ShowHttpDocumentPreviewContent
            };

            if (!visible)
            {
                continue;
            }

            if (propertyName is null || propertyName == propertyName2)
            {
                host.Content = factory();
            }
        }
    }
}
