using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ProjectInterfaceRootWorkspaceViewModel : ViewModelBase
{
    private readonly string _projectId;
    private readonly ObservableCollection<RequestCaseItemViewModel> _savedRequests;
    private readonly IProjectHttpSettingsService _settingsService;
    private readonly Func<RequestCaseDto, Task> _openHttpInterfaceAsync;
    private readonly Action<string> _setStatusMessage;
    private readonly Action _notifyShellState;
    private readonly ObservableCollection<ProjectHttpInterfaceOverviewItemViewModel> _httpInterfaces = [];

    public ProjectInterfaceRootWorkspaceViewModel(
        string projectId,
        ObservableCollection<RequestCaseItemViewModel> savedRequests,
        IProjectHttpSettingsService settingsService,
        Func<RequestCaseDto, Task> openHttpInterfaceAsync,
        Action<string> setStatusMessage,
        Action notifyShellState)
    {
        _projectId = projectId;
        _savedRequests = savedRequests;
        _settingsService = settingsService;
        _openHttpInterfaceAsync = openHttpInterfaceAsync;
        _setStatusMessage = setStatusMessage;
        _notifyShellState = notifyShellState;

        HttpInterfaces = new ReadOnlyObservableCollection<ProjectHttpInterfaceOverviewItemViewModel>(_httpInterfaces);
        _savedRequests.CollectionChanged += OnSavedRequestsCollectionChanged;
        RefreshHttpInterfaces();
    }

    public ReadOnlyObservableCollection<ProjectHttpInterfaceOverviewItemViewModel> HttpInterfaces { get; }
    public IReadOnlyList<string> AuthModes { get; } = ["无", "Bearer Token"];
    public bool IsAuthSelected => SelectedTabIndex == 0;
    public bool IsAllInterfacesSelected => SelectedTabIndex == 1;
    public bool HasHttpInterfaces => HttpInterfaces.Count > 0;
    public bool ShowHttpInterfacesEmptyState => !HasHttpInterfaces;
    public string InterfaceCountText => HttpInterfaces.Count.ToString();
    public string HttpInterfacesEmptyText => string.IsNullOrWhiteSpace(SearchText) ? "当前还没有保存的 HTTP 接口" : "没有匹配的 HTTP 接口";
    public bool IsBearerMode => SelectedAuthModeIndex == 1;
    public bool IsNoAuthMode => SelectedAuthModeIndex == 0;
    public string AuthBadgeText => IsBearerMode ? "1" : "0";

    [ObservableProperty]
    private int selectedTabIndex;

    [ObservableProperty]
    private int selectedAuthModeIndex;

    [ObservableProperty]
    private string bearerToken = string.Empty;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = "接口根配置会作为 HTTP 接口的默认请求配置。";

    public async Task InitializeAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var settings = await _settingsService.GetAuthSettingsAsync(_projectId, CancellationToken.None);
            ApplyAuthSettings(settings);
            StatusText = "已载入接口根 Auth 配置。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public ProjectHttpAuthSettingsDto BuildAuthSettings()
    {
        return new ProjectHttpAuthSettingsDto
        {
            ProjectId = _projectId,
            AuthMode = IsBearerMode ? ProjectHttpAuthSettingsDto.ModeBearer : ProjectHttpAuthSettingsDto.ModeNone,
            BearerToken = BearerToken.Trim(),
            UpdatedAt = DateTime.UtcNow
        };
    }

    public RequestSnapshotDto ApplyGlobalAuth(RequestSnapshotDto snapshot)
    {
        var settings = BuildAuthSettings();
        if (!settings.IsBearerEnabled || HasEnabledAuthorizationHeader(snapshot))
        {
            return snapshot;
        }

        var headers = snapshot.Headers
            .Select(item => new RequestKeyValueDto
            {
                Name = item.Name,
                Value = item.Value,
                IsEnabled = item.IsEnabled
            })
            .ToList();
        headers.Add(new RequestKeyValueDto
        {
            Name = "Authorization",
            Value = $"Bearer {settings.BearerToken}",
            IsEnabled = true
        });

        return CloneSnapshot(snapshot, headers);
    }

    protected override void DisposeManaged()
    {
        _savedRequests.CollectionChanged -= OnSavedRequestsCollectionChanged;
    }

    [RelayCommand]
    private async Task SaveAuthAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _settingsService.SaveAuthSettingsAsync(BuildAuthSettings(), CancellationToken.None);
            if (result.IsSuccess && result.Data is not null)
            {
                ApplyAuthSettings(result.Data);
                StatusText = IsBearerMode ? "HTTP 接口全局 Bearer Token 已保存。" : "HTTP 接口全局 Auth 已关闭。";
                _setStatusMessage(StatusText);
            }
            else
            {
                StatusText = result.Message;
                _setStatusMessage(result.Message);
            }
        }
        finally
        {
            IsBusy = false;
            _notifyShellState();
        }
    }

    [RelayCommand]
    private void ShowAuth()
    {
        SelectedTabIndex = 0;
    }

    [RelayCommand]
    private void ShowAllInterfaces()
    {
        SelectedTabIndex = 1;
    }

    [RelayCommand]
    private async Task OpenInterfaceAsync(ProjectHttpInterfaceOverviewItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        await _openHttpInterfaceAsync(item.Source);
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsAuthSelected));
        OnPropertyChanged(nameof(IsAllInterfacesSelected));
    }

    partial void OnSelectedAuthModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsBearerMode));
        OnPropertyChanged(nameof(IsNoAuthMode));
        OnPropertyChanged(nameof(AuthBadgeText));
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshHttpInterfaces();
    }

    private void OnSavedRequestsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshHttpInterfaces();
    }

    private void RefreshHttpInterfaces()
    {
        var keyword = SearchText.Trim();
        var items = _savedRequests
            .Select(item => item.SourceCase)
            .Where(item => string.Equals(item.EntryType, ProjectTabRequestEntryTypes.HttpInterface, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(keyword)
                || item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || item.RequestSnapshot.Url.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || item.FolderPath.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.FolderPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ProjectHttpInterfaceOverviewItemViewModel(item, _openHttpInterfaceAsync))
            .ToList();

        _httpInterfaces.ReplaceWith(items);
        OnPropertyChanged(nameof(HasHttpInterfaces));
        OnPropertyChanged(nameof(ShowHttpInterfacesEmptyState));
        OnPropertyChanged(nameof(InterfaceCountText));
        OnPropertyChanged(nameof(HttpInterfacesEmptyText));
    }

    private void ApplyAuthSettings(ProjectHttpAuthSettingsDto settings)
    {
        SelectedAuthModeIndex = string.Equals(settings.AuthMode, ProjectHttpAuthSettingsDto.ModeBearer, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        BearerToken = settings.BearerToken;
    }

    private static bool HasEnabledAuthorizationHeader(RequestSnapshotDto snapshot)
    {
        return snapshot.Headers.Any(item =>
            item.IsEnabled
            && string.Equals(item.Name?.Trim(), "Authorization", StringComparison.OrdinalIgnoreCase));
    }

    private static RequestSnapshotDto CloneSnapshot(RequestSnapshotDto snapshot, List<RequestKeyValueDto> headers)
    {
        return new RequestSnapshotDto
        {
            EndpointId = snapshot.EndpointId,
            Name = snapshot.Name,
            Method = snapshot.Method,
            Url = snapshot.Url,
            Description = snapshot.Description,
            BodyMode = snapshot.BodyMode,
            BodyContent = snapshot.BodyContent,
            IgnoreSslErrors = snapshot.IgnoreSslErrors,
            QueryParameters = snapshot.QueryParameters.ToList(),
            PathParameters = snapshot.PathParameters.ToList(),
            Headers = headers
        };
    }
}
