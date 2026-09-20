using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Views.Controls;

/// <summary>
/// 项目标签宿主：按 ProjectTabViewModel 缓存已构建的 ProjectWorkspaceView。
/// 旧实现经 ContentControl.ContentTemplate 绑定 ActiveProjectTab，每次切换标签都整树重建
/// （侧栏、编辑器、惰性工作台全部重新实例化）；缓存后切换标签只是重新挂树，滚动位置等视觉状态保留。
/// 标签关闭时同步释放对应视图，避免持有已 Dispose 的 ViewModel。
/// </summary>
public class ProjectTabHostView : ContentControl
{
    private readonly Dictionary<ProjectTabViewModel, ProjectWorkspaceView> _viewCache = [];
    private MainWindowViewModel? _viewModel;
    private bool _isViewModelSubscribed;

    public ProjectTabHostView()
    {
        DataContextChanged += OnDataContextChanged;
        DetachedFromLogicalTree += (_, _) => ReleaseViewModel();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        ReleaseViewModel();

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.ProjectTabs.CollectionChanged += OnProjectTabsCollectionChanged;
        _isViewModelSubscribed = true;
        UpdateHostedTab();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 空属性名表示全量刷新，同样需要重挂
        if (e.PropertyName is null || e.PropertyName == nameof(MainWindowViewModel.ActiveProjectTab))
        {
            UpdateHostedTab();
        }
    }

    private void OnProjectTabsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Reset)
        {
            PruneClosedTabs();
        }
    }

    private void UpdateHostedTab()
    {
        if (_viewModel is null)
        {
            return;
        }

        var activeTab = _viewModel.ActiveProjectTab;
        if (activeTab is null)
        {
            Content = null;
            return;
        }

        if (Content is ProjectWorkspaceView hosted && ReferenceEquals(hosted.DataContext, activeTab))
        {
            return;
        }

        if (!_viewCache.TryGetValue(activeTab, out var view))
        {
            view = new ProjectWorkspaceView { DataContext = activeTab };
            _viewCache[activeTab] = view;
        }

        Content = view;
    }

    private void PruneClosedTabs()
    {
        if (_viewModel is null)
        {
            return;
        }

        foreach (var tab in _viewCache.Keys.Where(tab => !_viewModel.ProjectTabs.Contains(tab)).ToArray())
        {
            if (_viewCache.Remove(tab, out var view))
            {
                if (ReferenceEquals(Content, view))
                {
                    Content = null;
                }

                // 断开绑定与逻辑树残留，视图随后由 GC 回收
                view.DataContext = null;
            }
        }
    }

    private void ReleaseViewModel()
    {
        if (_isViewModelSubscribed && _viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.ProjectTabs.CollectionChanged -= OnProjectTabsCollectionChanged;
        }

        _isViewModelSubscribed = false;

        foreach (var view in _viewCache.Values)
        {
            view.DataContext = null;
        }

        _viewCache.Clear();
        Content = null;
        _viewModel = null;
    }
}
