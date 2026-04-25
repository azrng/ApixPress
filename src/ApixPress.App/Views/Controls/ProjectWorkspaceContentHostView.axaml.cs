using System.ComponentModel;
using Avalonia.Controls;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Views.Controls;

public partial class ProjectWorkspaceContentHostView : UserControl
{
    private ProjectTabViewModel? _viewModel;
    private ProjectWorkspaceSidebarView? _sidebarView;
    private WorkspaceLandingView? _landingView;
    private RequestEditorWorkspaceView? _requestEditorView;
    private RequestHistoryDetailView? _requestHistoryView;
    private ProjectSettingsWorkspaceView? _projectSettingsView;
    private ProjectWorkspaceContentMode? _currentMode;

    public ProjectWorkspaceContentHostView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => UnsubscribeShell();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        UnsubscribeShell();
        ClearCachedViews();

        _viewModel = DataContext as ProjectTabViewModel;
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
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.Shell.PropertyChanged += OnShellPropertyChanged;
    }

    private void UnsubscribeShell()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.Shell.PropertyChanged -= OnShellPropertyChanged;
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
        if (IsInterfaceManagementMode(_currentMode) && IsInterfaceManagementMode(mode) && HostGrid.Children.Count > 0)
        {
            _currentMode = mode;
            return;
        }

        if (_currentMode == mode && HostGrid.Children.Count > 0)
        {
            return;
        }

        _currentMode = mode;
        HostGrid.Children.Clear();
        HostGrid.ColumnDefinitions.Clear();

        if (mode == ProjectWorkspaceContentMode.ProjectSettings)
        {
            HostGrid.Children.Add(EnsureProjectSettingsView());
            return;
        }

        HostGrid.ColumnDefinitions = new ColumnDefinitions("0,286,*");

        var sidebarView = EnsureSidebarView();
        Grid.SetColumn(sidebarView, 1);
        HostGrid.Children.Add(sidebarView);

        if (IsInterfaceManagementMode(mode))
        {
            var landingView = EnsureLandingView();
            Grid.SetColumn(landingView, 2);
            HostGrid.Children.Add(landingView);

            var editorView = EnsureRequestEditorView();
            Grid.SetColumn(editorView, 2);
            HostGrid.Children.Add(editorView);
            return;
        }

        Control contentView = mode switch
        {
            ProjectWorkspaceContentMode.RequestHistory => EnsureRequestHistoryView(),
            _ => EnsureLandingView()
        };
        Grid.SetColumn(contentView, 2);
        HostGrid.Children.Add(contentView);
    }

    private void ClearCachedViews()
    {
        HostGrid.Children.Clear();
        HostGrid.ColumnDefinitions.Clear();
        _currentMode = null;
        _sidebarView = null;
        _landingView = null;
        _requestEditorView = null;
        _requestHistoryView = null;
        _projectSettingsView = null;
    }

    private ProjectWorkspaceSidebarView EnsureSidebarView()
    {
        _sidebarView ??= new ProjectWorkspaceSidebarView
        {
            DataContext = _viewModel
        };
        return _sidebarView;
    }

    private WorkspaceLandingView EnsureLandingView()
    {
        _landingView ??= new WorkspaceLandingView
        {
            DataContext = _viewModel
        };
        return _landingView;
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

    private static bool IsInterfaceManagementMode(ProjectWorkspaceContentMode? mode)
    {
        return mode is ProjectWorkspaceContentMode.Landing or ProjectWorkspaceContentMode.RequestEditor;
    }
}
