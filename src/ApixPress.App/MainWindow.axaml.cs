using Avalonia.Controls;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ApixPress.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly IWindowHostService _windowHostService;

    public MainWindow()
        : this(App.Services.GetRequiredService<MainWindowViewModel>(), App.Services.GetRequiredService<IWindowHostService>())
    {
    }

    public MainWindow(MainWindowViewModel viewModel, IWindowHostService windowHostService)
    {
        _viewModel = viewModel;
        _windowHostService = windowHostService;
        InitializeComponent();
        DataContext = _viewModel;
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private async void OnOpened(object? sender, System.EventArgs e)
    {
        _windowHostService.MainWindow = this;
        await _viewModel.InitializeAsync();
    }

    private void OnClosed(object? sender, System.EventArgs e)
    {
        if (ReferenceEquals(_windowHostService.MainWindow, this))
        {
            _windowHostService.MainWindow = null;
        }
    }
}
