using System.ComponentModel;
using Avalonia.Controls;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Views.Controls;

public partial class RequestEditorContentHostView : UserControl
{
    private ProjectTabViewModel? _viewModel;
    private QuickRequestWorkbenchView? _quickRequestView;
    private HttpInterfaceWorkbenchView? _httpWorkbenchView;
    private HttpDocumentWorkspaceView? _httpDocumentView;
    private RequestEditorContentMode? _currentMode;

    public RequestEditorContentHostView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => UnsubscribeEditor();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        UnsubscribeEditor();
        ClearCachedViews();

        _viewModel = DataContext as ProjectTabViewModel;
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
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.Editor.PropertyChanged += OnEditorPropertyChanged;
    }

    private void UnsubscribeEditor()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.Editor.PropertyChanged -= OnEditorPropertyChanged;
    }

    private void UpdateHostedContent()
    {
        if (_viewModel is null)
        {
            _currentMode = null;
            ContentHost.Content = null;
            return;
        }

        var mode = _viewModel.Editor.CurrentContentMode;
        if (_currentMode == mode && ContentHost.Content is not null)
        {
            return;
        }

        _currentMode = mode;
        ContentHost.Content = mode switch
        {
            RequestEditorContentMode.QuickRequest => (Control)EnsureQuickRequestView(),
            RequestEditorContentMode.HttpWorkbench => EnsureHttpWorkbenchView(),
            RequestEditorContentMode.HttpDocumentPreview => EnsureHttpDocumentView(),
            _ => null
        };
    }

    private void ClearCachedViews()
    {
        ContentHost.Content = null;
        _currentMode = null;
        _quickRequestView = null;
        _httpWorkbenchView = null;
        _httpDocumentView = null;
    }

    private QuickRequestWorkbenchView EnsureQuickRequestView()
    {
        _quickRequestView ??= new QuickRequestWorkbenchView
        {
            DataContext = _viewModel
        };
        return _quickRequestView;
    }

    private HttpInterfaceWorkbenchView EnsureHttpWorkbenchView()
    {
        _httpWorkbenchView ??= new HttpInterfaceWorkbenchView
        {
            DataContext = _viewModel
        };
        return _httpWorkbenchView;
    }

    private HttpDocumentWorkspaceView EnsureHttpDocumentView()
    {
        _httpDocumentView ??= new HttpDocumentWorkspaceView
        {
            DataContext = _viewModel
        };
        return _httpDocumentView;
    }
}
