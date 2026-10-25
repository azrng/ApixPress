using Avalonia.Input;
using System.ComponentModel;
using Avalonia.Controls;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Views.Controls;

public partial class HttpInterfaceWorkbenchView : UserControl
{
    private ProjectTabViewModel? _viewModel;
    private RequestConfigTabViewModel? _configTab;
    private ResponseSectionViewModel? _responseSection;
    private HttpInterfaceParamsTabView? _paramsTabView;
    private HttpInterfaceBodyTabView? _bodyTabView;
    private HttpInterfaceHeadersTabView? _headersTabView;
    private int? _currentSelectedTabIndex;
    private bool _isSubscribed;

    public HttpInterfaceWorkbenchView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) =>
        {
            Subscribe();
            UpdateHostedContent();
            UpdateResponseContent();
        };
        DetachedFromVisualTree += (_, _) => Unsubscribe();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Unsubscribe();
        ResponseBodyHost.Content = null;
        ActualRequestHost.Content = null;

        _viewModel = DataContext as ProjectTabViewModel;
        if (_viewModel is null)
        {
            ClearCachedViews();
            return;
        }

        UpdateCachedViewDataContexts();
        Subscribe();
        UpdateHostedContent();
        UpdateResponseContent();
    }

    private void Subscribe()
    {
        if (_viewModel is null || _isSubscribed)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        WireConfigTab(_viewModel.ConfigTab);
        WireResponseSection(_viewModel.ResponseSection);
        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (_viewModel is not null && _isSubscribed)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        WireConfigTab(null);
        WireResponseSection(null);
        _isSubscribed = false;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ProjectTabViewModel.ConfigTab), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(e.PropertyName))
        {
            WireConfigTab(_viewModel?.ConfigTab);
            UpdateHostedContent();
        }

        if (e.PropertyName == nameof(ProjectTabViewModel.ResponseSection)
            || string.IsNullOrWhiteSpace(e.PropertyName))
        {
            WireResponseSection(_viewModel?.ResponseSection);
            UpdateResponseContent();
        }
    }

    private void WireResponseSection(ResponseSectionViewModel? responseSection)
    {
        if (ReferenceEquals(_responseSection, responseSection))
        {
            return;
        }

        if (_responseSection is not null)
        {
            _responseSection.PropertyChanged -= OnResponsePropertyChanged;
        }

        _responseSection = responseSection;
        if (_responseSection is not null)
        {
            _responseSection.PropertyChanged += OnResponsePropertyChanged;
        }
    }

    private void OnResponsePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ResponseSectionViewModel.HasResponse)
            or nameof(ResponseSectionViewModel.SelectedResponseTab) or null or "")
        {
            UpdateResponseContent();
        }
    }

    private void UpdateResponseContent()
    {
        if (_responseSection?.HasResponse != true || _viewModel is null)
        {
            return;
        }

        // 首次点击接口只创建请求配置与响应空态，重量级内容等到真正被查看时才实例化。
        if (_responseSection.SelectedResponseTab == 0)
        {
            ResponseBodyHost.Content = _viewModel;
        }
        else if (_responseSection.SelectedResponseTab == 2)
        {
            ActualRequestHost.Content = _viewModel;
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
        ResponseBodyHost.Content = null;
        ActualRequestHost.Content = null;
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
    private void OnResponseSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not ProjectTabViewModel viewModel) return;
        var command = e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            ? viewModel.ResponseSection.PreviousBodySearchMatchCommand
            : viewModel.ResponseSection.NextBodySearchMatchCommand;
        if (command.CanExecute(null)) command.Execute(null);
        e.Handled = true;
    }
}
