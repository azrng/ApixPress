using Avalonia.Controls;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels;
using ApixPress.App.Views.Controls;
using Microsoft.Extensions.DependencyInjection;
using Ursa.Common;
using Ursa.Controls;
using Ursa.Controls.Options;

namespace ApixPress.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly IWindowHostService _windowHostService;
    private bool _isEnvironmentDrawerOpen;
    private bool _isProjectDrawerOpen;
    private bool _isUseCasesDrawerOpen;

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

    private async void OnOpenUseCasesDrawer(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isUseCasesDrawerOpen)
        {
            return;
        }

        _isUseCasesDrawerOpen = true;
        try
        {
            await Drawer.ShowModal(
                new UseCasesDrawerView(),
                _viewModel,
                null,
                new DrawerOptions
                {
                    Buttons = DialogButton.None,
                    Title = "用例管理",
                    Position = Ursa.Common.Position.Right,
                    MinWidth = 420,
                    MaxWidth = 460,
                    CanResize = true,
                    TopLevelHashCode = GetHashCode()
                });
        }
        finally
        {
            _isUseCasesDrawerOpen = false;
        }
    }

    private async void OnOpenProjectDrawer(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isProjectDrawerOpen)
        {
            return;
        }

        _isProjectDrawerOpen = true;
        try
        {
            await Drawer.ShowModal(
                new ProjectDrawerView(),
                _viewModel,
                null,
                new DrawerOptions
                {
                    Buttons = DialogButton.None,
                    Title = "项目管理",
                    Position = Ursa.Common.Position.Right,
                    MinWidth = 920,
                    MaxWidth = 1024,
                    CanResize = true,
                    TopLevelHashCode = GetHashCode()
                });
        }
        finally
        {
            _isProjectDrawerOpen = false;
        }
    }

    private async void OnOpenEnvironmentDrawer(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isEnvironmentDrawerOpen)
        {
            return;
        }

        _isEnvironmentDrawerOpen = true;
        try
        {
            await Drawer.ShowModal(
                new EnvironmentDrawerView(),
                _viewModel,
                null,
                new DrawerOptions
                {
                    Buttons = DialogButton.None,
                    Title = "环境变量",
                    Position = Ursa.Common.Position.Right,
                    MinWidth = 420,
                    MaxWidth = 460,
                    CanResize = true,
                    TopLevelHashCode = GetHashCode()
                });
        }
        finally
        {
            _isEnvironmentDrawerOpen = false;
        }
    }
}
