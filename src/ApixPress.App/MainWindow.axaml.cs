using Avalonia.Controls;
using Avalonia.Input;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels;
using ApixPress.App.Views.Controls;
using Ursa.Common;
using Ursa.Controls;
using Ursa.Controls.Options;

namespace ApixPress.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly IWindowHostService _windowHostService;
    private bool _isCreateProjectDrawerOpen;
    private bool _isEnvironmentDrawerOpen;
    private bool _isProjectDrawerOpen;
    private bool _isUseCasesDrawerOpen;

    public MainWindow()
    {
        if (!Avalonia.Controls.Design.IsDesignMode)
        {
            throw new InvalidOperationException("MainWindow 必须通过依赖注入创建。");
        }

        _viewModel = null!;
        _windowHostService = null!;
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel viewModel, IWindowHostService windowHostService)
    {
        _viewModel = viewModel;
        _windowHostService = windowHostService;
        InitializeComponent();
        DataContext = _viewModel;
        Opened += OnOpened;
        Closed += OnClosed;
        KeyDown += OnWindowKeyDown;
    }

    private void OnOpened(object? sender, System.EventArgs e)
    {
        _ = RunUiActionAsync(async () =>
        {
            _windowHostService.MainWindow = this;
            _viewModel.UpdateWindowState(WindowState);
            await _viewModel.InitializeAsync();
        });
    }

    private void OnClosed(object? sender, System.EventArgs e)
    {
        if (ReferenceEquals(_windowHostService.MainWindow, this))
        {
            _windowHostService.MainWindow = null;
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.S || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        e.Handled = true;
        _ = RunUiActionAsync(() => _viewModel.SaveCaseCommand.ExecuteAsync(null));
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnMinimizeWindow(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnToggleMaximizeWindow(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        _viewModel.UpdateWindowState(WindowState);
    }

    private void OnCloseWindow(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void OnOpenUseCasesDrawer(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isUseCasesDrawerOpen)
        {
            return;
        }

        _ = RunUiActionAsync(async () =>
        {
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
        });
    }

    private void OnOpenProjectDrawer(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isProjectDrawerOpen)
        {
            return;
        }

        var activeProjectId = _viewModel.ActiveProjectTab?.ProjectId;
        if (!string.IsNullOrWhiteSpace(activeProjectId))
        {
            _viewModel.ProjectPanel.SelectedProject = _viewModel.ProjectPanel.Projects.FirstOrDefault(item =>
                string.Equals(item.Id, activeProjectId, StringComparison.OrdinalIgnoreCase));
        }

        _ = RunUiActionAsync(async () =>
        {
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
        });
    }

    private void OnOpenCreateProjectDrawer(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isCreateProjectDrawerOpen)
        {
            return;
        }

        _ = RunUiActionAsync(async () =>
        {
            _isCreateProjectDrawerOpen = true;
            try
            {
                await Drawer.ShowModal(
                    new CreateProjectDrawerView(),
                    _viewModel,
                    null,
                    new DrawerOptions
                    {
                        Buttons = DialogButton.None,
                        Title = "新建项目",
                        Position = Ursa.Common.Position.Right,
                        MinWidth = 420,
                        MaxWidth = 460,
                        CanResize = true,
                        TopLevelHashCode = GetHashCode()
                    });
            }
            finally
            {
                _isCreateProjectDrawerOpen = false;
            }
        });
    }

    private void OnOpenEnvironmentDrawer(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isEnvironmentDrawerOpen)
        {
            return;
        }

        _ = RunUiActionAsync(async () =>
        {
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
        });
    }

    private async Task RunUiActionAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }
}
