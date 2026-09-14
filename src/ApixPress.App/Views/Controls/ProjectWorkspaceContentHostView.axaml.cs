using System.ComponentModel;
using Avalonia.Controls;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Views.Controls;

public partial class ProjectWorkspaceContentHostView : UserControl
{
    private ProjectTabViewModel? _viewModel;
    private ProjectWorkspaceSidebarView? _sidebarView;
    private ProjectInnerTabBarView? _innerTabBarView;
    private WorkspaceLandingView? _landingView;
    private ProjectInterfaceRootWorkspaceView? _interfaceRootView;
    private RequestEditorWorkspaceView? _requestEditorView;
    private RequestHistoryDetailView? _requestHistoryView;
    private ProjectSettingsWorkspaceView? _projectSettingsView;
    private ProjectWorkspaceContentMode? _currentMode;
    private bool _isShellSubscribed;

    public ProjectWorkspaceContentHostView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) =>
        {
            SubscribeShell();
            UpdateHostedContent();
        };
        DetachedFromVisualTree += (_, _) => UnsubscribeShell();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        UnsubscribeShell();

        _viewModel = DataContext as ProjectTabViewModel;
        if (_viewModel is null)
        {
            ClearCachedViews();
            return;
        }

        UpdateCachedViewDataContexts();
        SubscribeShell();
        UpdateHostedContent();
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ProjectWorkspaceShellViewModel.CurrentContentMode), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(e.PropertyName))
        {
            UpdateHostedContent();
        }
    }

    private void SubscribeShell()
    {
        if (_viewModel is null || _isShellSubscribed)
        {
            return;
        }

        _viewModel.Shell.PropertyChanged += OnShellPropertyChanged;
        _isShellSubscribed = true;
    }

    private void UnsubscribeShell()
    {
        if (_viewModel is null || !_isShellSubscribed)
        {
            return;
        }

        _viewModel.Shell.PropertyChanged -= OnShellPropertyChanged;
        _isShellSubscribed = false;
    }

    private void UpdateHostedContent()
    {
        if (_viewModel is null)
        {
            _currentMode = null;
            HostGrid.Children.Clear();
            HostGrid.ColumnDefinitions.Clear();
            return;
        }

        var mode = _viewModel.Shell.CurrentContentMode;
        if (_currentMode == mode && HostGrid.Children.Count > 0)
        {
            return;
        }

        _currentMode = mode;
        HostGrid.Children.Clear();
        HostGrid.ColumnDefinitions.Clear();

        if (mode == ProjectWorkspaceContentMode.ProjectSettings)
        {
            var settingsView = EnsureProjectSettingsView();
            DetachFromParent(settingsView);
            HostGrid.Children.Add(settingsView);
            return;
        }

        HostGrid.ColumnDefinitions = new ColumnDefinitions("0,280,*");

        var sidebarView = EnsureSidebarView();
        DetachFromParent(sidebarView);
        Grid.SetColumn(sidebarView, 1);
        HostGrid.Children.Add(sidebarView);

        Control contentView = mode switch
        {
            ProjectWorkspaceContentMode.InterfaceRoot => EnsureInterfaceRootView(),
            ProjectWorkspaceContentMode.RequestEditor => EnsureRequestEditorView(),
            ProjectWorkspaceContentMode.RequestHistory => EnsureRequestHistoryView(),
            _ => EnsureLandingView()
        };

        // 项目内请求标签栏只在内容列顶部（对齐 Apifox：侧栏不被标签条压住）
        var contentColumn = new Grid();
        contentColumn.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        contentColumn.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        var tabBar = EnsureInnerTabBarView();
        DetachFromParent(tabBar);
        DetachFromParent(contentView);
        Grid.SetRow(tabBar, 0);
        Grid.SetRow(contentView, 1);
        contentColumn.Children.Add(tabBar);
        contentColumn.Children.Add(contentView);

        Grid.SetColumn(contentColumn, 2);
        HostGrid.Children.Add(contentColumn);
    }

    // 缓存视图复用时可能仍挂在上一次构建的旧 contentColumn 上（HostGrid.Clear 只解除 HostGrid 一层，
    // 旧中间容器对子控件的父级关系还在），不先解除会在 Children.Add 时抛“already has a visual parent”导致闪退
    private static void DetachFromParent(Control control)
    {
        if (control.Parent is Panel panel)
        {
            panel.Children.Remove(control);
        }
    }

    private void ClearCachedViews()
    {
        HostGrid.Children.Clear();
        HostGrid.ColumnDefinitions.Clear();
        _currentMode = null;
        _sidebarView = null;
        _innerTabBarView = null;
        _landingView = null;
        _interfaceRootView = null;
        _requestEditorView = null;
        _requestHistoryView = null;
        _projectSettingsView = null;
    }

    private void UpdateCachedViewDataContexts()
    {
        if (_sidebarView is not null)
        {
            _sidebarView.DataContext = _viewModel;
        }

        if (_innerTabBarView is not null)
        {
            _innerTabBarView.DataContext = _viewModel;
        }

        if (_landingView is not null)
        {
            _landingView.DataContext = _viewModel;
        }

        if (_interfaceRootView is not null)
        {
            _interfaceRootView.DataContext = _viewModel;
        }

        if (_requestEditorView is not null)
        {
            _requestEditorView.DataContext = _viewModel;
        }

        if (_requestHistoryView is not null)
        {
            _requestHistoryView.DataContext = _viewModel;
        }

        if (_projectSettingsView is not null)
        {
            _projectSettingsView.DataContext = _viewModel;
        }
    }

    private ProjectWorkspaceSidebarView EnsureSidebarView()
    {
        _sidebarView ??= new ProjectWorkspaceSidebarView
        {
            DataContext = _viewModel
        };
        return _sidebarView;
    }

    private ProjectInnerTabBarView EnsureInnerTabBarView()
    {
        _innerTabBarView ??= new ProjectInnerTabBarView
        {
            DataContext = _viewModel
        };
        return _innerTabBarView;
    }

    private WorkspaceLandingView EnsureLandingView()
    {
        _landingView ??= new WorkspaceLandingView
        {
            DataContext = _viewModel
        };
        return _landingView;
    }

    private ProjectInterfaceRootWorkspaceView EnsureInterfaceRootView()
    {
        _interfaceRootView ??= new ProjectInterfaceRootWorkspaceView
        {
            DataContext = _viewModel
        };
        return _interfaceRootView;
    }

    private RequestEditorWorkspaceView EnsureRequestEditorView()
    {
        _requestEditorView ??= new RequestEditorWorkspaceView
        {
            DataContext = _viewModel
        };
        return _requestEditorView;
    }

    private RequestHistoryDetailView EnsureRequestHistoryView()
    {
        _requestHistoryView ??= new RequestHistoryDetailView
        {
            DataContext = _viewModel
        };
        return _requestHistoryView;
    }

    private ProjectSettingsWorkspaceView EnsureProjectSettingsView()
    {
        _projectSettingsView ??= new ProjectSettingsWorkspaceView
        {
            DataContext = _viewModel
        };
        return _projectSettingsView;
    }

}
