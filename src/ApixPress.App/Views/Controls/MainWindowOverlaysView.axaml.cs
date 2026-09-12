using System.ComponentModel;
using Avalonia.Controls;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Views.Controls;

public partial class MainWindowOverlaysView : UserControl
{
    private MainWindowViewModel? _viewModel;
    private bool _isSubscribed;

    public MainWindowOverlaysView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Subscribe();
        DetachedFromVisualTree += (_, _) =>
        {
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _viewModel.ShellPanels.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _isSubscribed = false;
        };
    }

    /// <summary>属性名 -> 弹层宿主与惰性工厂。首次可见才实例化弹层内容，避免九个弹层全部常驻视觉树。</summary>
    private static (string PropertyName, ContentControl Host, Func<Control> Factory)[] ResolveHosts(
        MainWindowOverlaysView view) =>
    [
        ("IsNotificationCenterOpen", view.NotificationCenterHost, () => new NotificationCenterOverlayView()),
        ("IsEnvironmentManagerOpen", view.EnvironmentManagerHost, () => new EnvironmentManagerDialogView()),
        ("ShowQuickRequestSaveDialog", view.QuickRequestSaveHost, () => new QuickRequestSaveDialogView()),
        ("ShowRequestCodeDialog", view.RequestCodeHost, () => new RequestCodeDialogView()),
        ("ShowWorkspaceDeleteConfirmDialog", view.WorkspaceDeleteConfirmHost, () => new WorkspaceDeleteConfirmDialogView()),
        ("ShowCreateFolderDialog", view.WorkspaceCreateFolderHost, () => new WorkspaceCreateFolderDialogView()),
        ("ShowProjectImportDialog", view.ProjectImportHost, () => new ProjectImportDialogView()),
        ("ShowProjectImportOverwriteConfirmDialog", view.ImportOverwriteConfirmHost, () => new ImportOverwriteConfirmDialogView()),
        ("IsSettingsDialogOpen", view.SettingsDialogHost, () => new SettingsDialogView()),
        ("IsCreateProjectDialogOpen", view.CreateProjectDialogHost, () => new CreateProjectDialogView())
    ];

    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.ShellPanels.PropertyChanged += OnViewModelPropertyChanged;
        _isSubscribed = true;

        // 初始已打开的弹层立即补建
        foreach (var (propertyName, host, _) in ResolveHosts(this))
        {
            if (host.Content is null && IsOverlayVisible(propertyName, host))
            {
                host.Content = CreateOverlayContent(propertyName);
            }
        }
    }

    private Control CreateOverlayContent(string propertyName) => propertyName switch
    {
        "IsNotificationCenterOpen" => new NotificationCenterOverlayView(),
        "IsEnvironmentManagerOpen" => new EnvironmentManagerDialogView(),
        "ShowQuickRequestSaveDialog" => new QuickRequestSaveDialogView(),
        "ShowRequestCodeDialog" => new RequestCodeDialogView(),
        "ShowWorkspaceDeleteConfirmDialog" => new WorkspaceDeleteConfirmDialogView(),
        "ShowCreateFolderDialog" => new WorkspaceCreateFolderDialogView(),
        "ShowProjectImportDialog" => new ProjectImportDialogView(),
        "ShowProjectImportOverwriteConfirmDialog" => new ImportOverwriteConfirmDialogView(),
        "IsSettingsDialogOpen" => new SettingsDialogView(),
        _ => new CreateProjectDialogView()
    };

    private bool IsOverlayVisible(string propertyName, Control host)
    {
        // 通知中心的可见性挂在 ShellPanels 子对象上，其余直接取宿主 IsVisible
        return propertyName == "IsNotificationCenterOpen"
            ? _viewModel?.ShellPanels.IsNotificationCenterOpen == true
            : host.IsVisible;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        foreach (var (propertyName, host, _) in ResolveHosts(this))
        {
            var isTarget = e.PropertyName == propertyName
                || (propertyName == "IsNotificationCenterOpen"
                    && e.PropertyName == nameof(MainWindowShellPanelsViewModel.IsNotificationCenterOpen));
            if (!isTarget || host.Content is not null)
            {
                continue;
            }

            if (IsOverlayVisible(propertyName, host))
            {
                host.Content = CreateOverlayContent(propertyName);
            }
        }
    }
}
