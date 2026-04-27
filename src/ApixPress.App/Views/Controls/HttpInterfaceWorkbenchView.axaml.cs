using System.ComponentModel;
using Avalonia.Controls;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Views.Controls;

public partial class HttpInterfaceWorkbenchView : UserControl
{
    private ProjectTabViewModel? _viewModel;
    private RequestConfigTabViewModel? _configTab;
    private HttpInterfaceParamsTabView? _paramsTabView;
    private HttpInterfaceBodyTabView? _bodyTabView;
    private HttpInterfaceHeadersTabView? _headersTabView;
    private int? _currentSelectedTabIndex;

    public HttpInterfaceWorkbenchView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => Unsubscribe();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Unsubscribe();

        _viewModel = DataContext as ProjectTabViewModel;
        if (_viewModel is null)
        {
            ClearCachedViews();
            return;
        }

        UpdateCachedViewDataContexts();
        Subscribe();
        UpdateHostedContent();
    }

    private void Subscribe()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        WireConfigTab(_viewModel.ConfigTab);
    }

    private void Unsubscribe()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        WireConfigTab(null);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ProjectTabViewModel.ConfigTab), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(e.PropertyName))
        {
            WireConfigTab(_viewModel?.ConfigTab);
            UpdateHostedContent();
        }
    }

    private void OnConfigTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(RequestConfigTabViewModel.SelectedTabIndex), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(e.PropertyName))
        {
            UpdateHostedContent();
        }
    }

    private void WireConfigTab(RequestConfigTabViewModel? configTab)
    {
        if (_configTab is not null)
        {
            _configTab.PropertyChanged -= OnConfigTabPropertyChanged;
        }

        _configTab = configTab;

        if (_configTab is not null)
        {
            _configTab.PropertyChanged += OnConfigTabPropertyChanged;
        }
    }

    private void UpdateHostedContent()
    {
        if (_configTab is null || _viewModel is null)
        {
            _currentSelectedTabIndex = null;
            ConfigTabContentHost.Children.Clear();
            return;
        }

        var selectedTabIndex = _configTab.SelectedTabIndex;
        if (_currentSelectedTabIndex == selectedTabIndex && ConfigTabContentHost.Children.Count > 0)
        {
            return;
        }

        _currentSelectedTabIndex = selectedTabIndex;
        ConfigTabContentHost.Children.Clear();

        Control content = selectedTabIndex switch
        {
            1 => EnsureBodyTabView(),
            2 => EnsureHeadersTabView(),
            _ => EnsureParamsTabView()
        };

        ConfigTabContentHost.Children.Add(content);
    }

    private void ClearCachedViews()
    {
        ConfigTabContentHost.Children.Clear();
        _currentSelectedTabIndex = null;
        _paramsTabView = null;
        _bodyTabView = null;
        _headersTabView = null;
    }

    private void UpdateCachedViewDataContexts()
    {
        if (_paramsTabView is not null)
        {
            _paramsTabView.DataContext = _viewModel;
        }

        if (_bodyTabView is not null)
        {
            _bodyTabView.DataContext = _viewModel;
        }

        if (_headersTabView is not null)
        {
            _headersTabView.DataContext = _viewModel;
        }
    }

    private HttpInterfaceParamsTabView EnsureParamsTabView()
    {
        _paramsTabView ??= new HttpInterfaceParamsTabView
        {
            DataContext = _viewModel
        };
        return _paramsTabView;
    }

    private HttpInterfaceBodyTabView EnsureBodyTabView()
    {
        _bodyTabView ??= new HttpInterfaceBodyTabView
        {
            DataContext = _viewModel
        };
        return _bodyTabView;
    }

    private HttpInterfaceHeadersTabView EnsureHeadersTabView()
    {
        _headersTabView ??= new HttpInterfaceHeadersTabView
        {
            DataContext = _viewModel
        };
        return _headersTabView;
    }
}
