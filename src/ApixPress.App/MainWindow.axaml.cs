using Avalonia.Controls;
using Avalonia.Input;
using ApixPress.App.Data.Context;
using ApixPress.App.Helpers;
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
    private readonly DatabaseInitializer _databaseInitializer;
    private readonly IRequestHistoryRetentionService _historyRetentionService;
    private bool _isCreateProjectDrawerOpen;
    private bool _isEnvironmentDrawerOpen;
    private bool _isUseCasesDrawerOpen;

    private readonly IAppNotificationService _appNotificationService;

    public MainWindow()
    {
        if (!Avalonia.Controls.Design.IsDesignMode)
        {
            throw new InvalidOperationException("MainWindow 必须通过依赖注入创建。");
        }

        _viewModel = null!;
        _windowHostService = null!;
        _databaseInitializer = null!;
        _historyRetentionService = null!;
        _appNotificationService = null!;
        InitializeComponent();
    }

    public MainWindow(
        MainWindowViewModel viewModel,
        IWindowHostService windowHostService,
        DatabaseInitializer databaseInitializer,
        IRequestHistoryRetentionService historyRetentionService,
        IAppNotificationService appNotificationService)
    {
        _viewModel = viewModel;
        _windowHostService = windowHostService;
        _databaseInitializer = databaseInitializer;
        _historyRetentionService = historyRetentionService;
        _appNotificationService = appNotificationService;
        InitializeComponent();
        DataContext = _viewModel;
        Opened += OnOpened;
        Closed += OnClosed;
        Closing += OnWindowClosing;
        KeyDown += OnWindowKeyDown;
    }

    private void OnOpened(object? sender, System.EventArgs e)
    {
        _ = RunUiActionAsync(async () =>
        {
            _windowHostService.MainWindow = this;
            _viewModel.UpdateWindowState(WindowState);
            await PerfLog.MeasureAsync("启动·数据库初始化", () => _databaseInitializer.InitializeAsync());
            await PerfLog.MeasureAsync("启动·主视图模型初始化", () => _viewModel.InitializeAsync());
            // 历史保留策略在 DB 就绪后启动：立即清理一次过期记录，之后每 24 小时重复
            _historyRetentionService.Start();
        });
    }

    private void OnClosed(object? sender, System.EventArgs e)
    {
        Opened -= OnOpened;
        Closed -= OnClosed;
        Closing -= OnWindowClosing;
        KeyDown -= OnWindowKeyDown;

        if (ReferenceEquals(_windowHostService.MainWindow, this))
        {
            _windowHostService.MainWindow = null;
        }

        _historyRetentionService.Stop();
        _viewModel.Dispose();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // 更新包下载/应用期间阻止误关窗口：主窗口一关更新进度就没了，提示用户先取消或等待完成
        if (!_viewModel.ShellPanels.SettingsCenter.IsUpdating)
        {
            return;
        }

        e.Cancel = true;
        _appNotificationService.Show(
            "更新进行中",
            "更新包正在下载或应用，可到设置中心取消更新后再关闭应用。",
            Avalonia.Controls.Notifications.NotificationType.Warning,
            System.TimeSpan.FromSeconds(6));
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

    // 供视图层（如侧栏接口节点的用例计数芯片）调用的抽屉入口
    internal void OpenUseCasesDrawer()
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
                await OverlayDrawer.ShowStandardAsync(
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
                await OverlayDrawer.ShowStandardAsync(
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
        OpenEnvironmentDrawer();
    }

    // 供视图层（环境芯片「管理环境…」入口）调用的抽屉入口
    internal void OpenEnvironmentDrawer()
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
                await OverlayDrawer.ShowStandardAsync(
                    new EnvironmentDrawerView(),
                    _viewModel,
                    null,
                    new DrawerOptions
                    {
                        Buttons = DialogButton.None,
                        Title = "环境变量",
                        Position = Ursa.Common.Position.Right,
                        MinWidth = 480,
                        MaxWidth = 560,
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
            // 初始化/保存等后台动作失败时给用户可见反馈，不再静默吞掉
            _appNotificationService.ShowError("操作失败", exception.Message);
        }
    }
}
