using System.ComponentModel;
using Avalonia.Controls;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Views.Controls;

public partial class RequestEditorWorkspaceView : UserControl
{
    private ProjectTabViewModel? _viewModel;
    private QuickRequestWorkbenchView? _quickRequestWorkbenchView;
    private HttpInterfaceWorkbenchView? _httpInterfaceWorkbenchView;
    private HttpDocumentWorkspaceView? _httpDocumentWorkspaceView;
    private RequestEditorContentMode? _currentMode;
    private bool _isEditorSubscribed;

    public RequestEditorWorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) =>
        {
            SubscribeEditor();
            UpdateHostedContent();
        };
        DetachedFromVisualTree += (_, _) => UnsubscribeEditor();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        UnsubscribeEditor();

        _viewModel = DataContext as ProjectTabViewModel;
        if (_viewModel is null)
        {
            ClearCachedViews();
            return;
        }

        UpdateCachedViewDataContexts();
        SubscribeEditor();
        UpdateHostedContent();
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ProjectRequestEditorViewModel.CurrentContentMode), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(e.PropertyName))
        {
            UpdateHostedContent();
        }
    }

    private void SubscribeEditor()
    {
        if (_viewModel is null || _isEditorSubscribed)
        {
            return;
        }

        _viewModel.Editor.PropertyChanged += OnEditorPropertyChanged;
        _isEditorSubscribed = true;
    }

    private void UnsubscribeEditor()
    {
        if (_viewModel is null || !_isEditorSubscribed)
        {
            return;
        }

        _viewModel.Editor.PropertyChanged -= OnEditorPropertyChanged;
        _isEditorSubscribed = false;
    }

    private void UpdateHostedContent()
    {
        if (_viewModel is null)
        {
            _currentMode = null;
            EditorContentHost.Children.Clear();
            return;
        }

        var mode = _viewModel.Editor.CurrentContentMode;
        if (_currentMode == mode && EditorContentHost.Children.Count > 0)
        {
            return;
        }

        _currentMode = mode;
        EditorContentHost.Children.Clear();

        Control? content = mode switch
        {
            RequestEditorContentMode.QuickRequest => EnsureQuickRequestWorkbenchView(),
            RequestEditorContentMode.HttpWorkbench => EnsureHttpInterfaceWorkbenchView(),
            RequestEditorContentMode.HttpDocumentPreview => EnsureHttpDocumentWorkspaceView(),
            _ => null
        };

        if (content is not null)
        {
            EditorContentHost.Children.Add(content);
        }
    }

    private void ClearCachedViews()
    {
        EditorContentHost.Children.Clear();
        _currentMode = null;
        _quickRequestWorkbenchView = null;
        _httpInterfaceWorkbenchView = null;
        _httpDocumentWorkspaceView = null;
    }

    private void UpdateCachedViewDataContexts()
    {
        if (_quickRequestWorkbenchView is not null)
        {
            _quickRequestWorkbenchView.DataContext = _viewModel;
        }

        if (_httpInterfaceWorkbenchView is not null)
        {
            _httpInterfaceWorkbenchView.DataContext = _viewModel;
        }

        if (_httpDocumentWorkspaceView is not null)
        {
            _httpDocumentWorkspaceView.DataContext = _viewModel;
        }
    }

    private QuickRequestWorkbenchView EnsureQuickRequestWorkbenchView()
    {
        _quickRequestWorkbenchView ??= new QuickRequestWorkbenchView
        {
            DataContext = _viewModel
        };
        return _quickRequestWorkbenchView;
    }

    private HttpInterfaceWorkbenchView EnsureHttpInterfaceWorkbenchView()
    {
        _httpInterfaceWorkbenchView ??= new HttpInterfaceWorkbenchView
        {
            DataContext = _viewModel
        };
        return _httpInterfaceWorkbenchView;
    }

    private HttpDocumentWorkspaceView EnsureHttpDocumentWorkspaceView()
    {
        _httpDocumentWorkspaceView ??= new HttpDocumentWorkspaceView
        {
            DataContext = _viewModel
        };
        return _httpDocumentWorkspaceView;
    }
}
