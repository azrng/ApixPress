using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;
using Azrng.Core.Results;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IRequestExecutionService _requestExecutionService;
    private readonly IRequestCaseService _requestCaseService;
    private readonly IRequestHistoryService _requestHistoryService;
    private bool _initialized;

    public MainWindowViewModel(
        IRequestExecutionService requestExecutionService,
        IRequestCaseService requestCaseService,
        IRequestHistoryService requestHistoryService,
        IEnvironmentVariableService environmentVariableService)
    {
        _requestExecutionService = requestExecutionService;
        _requestCaseService = requestCaseService;
        _requestHistoryService = requestHistoryService;

        ConfigTab = new RequestConfigTabViewModel(null);
        ResponseSection = new ResponseSectionViewModel();
        UseCasesPanel = new UseCasesPanelViewModel(requestCaseService);
        EnvironmentPanel = new EnvironmentPanelViewModel(environmentVariableService);
        HistoryPanel = new RequestHistoryPanelViewModel(requestHistoryService);

        UseCasesPanel.CaseApplied += OnCaseApplied;
    }

    // --- Sub-ViewModels ---

    public RequestConfigTabViewModel ConfigTab { get; }
    public ResponseSectionViewModel ResponseSection { get; }
    public UseCasesPanelViewModel UseCasesPanel { get; }
    public EnvironmentPanelViewModel EnvironmentPanel { get; }
    public RequestHistoryPanelViewModel HistoryPanel { get; }

    // --- Request history ---

    public ObservableCollection<RequestHistoryItemViewModel> RequestHistory => HistoryPanel.HistoryItems;

    [ObservableProperty]
    private bool hasNoHistory = true;

    [ObservableProperty]
    private string historySearchText = string.Empty;

    // --- HTTP methods ---

    public IReadOnlyList<string> HttpMethods { get; } = ["GET", "POST", "PUT", "DELETE", "PATCH"];

    // --- Top-bar / request-bar properties ---

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "准备就绪";

    [ObservableProperty]
    private string selectedMethod = "GET";

    [ObservableProperty]
    private string requestUrl = "https://";

    // --- Initialization ---

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        await RefreshWorkspaceAsync();
    }

    // --- Request history commands ---

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        await HistoryPanel.ClearHistoryAsync();
        await RefreshHistoryAsync();
        StatusMessage = "历史记录已清空。";
    }

    [RelayCommand]
    private void LoadHistoryItem(RequestHistoryItemViewModel? item)
    {
        if (item is null) return;

        SelectedMethod = item.Method;
        RequestUrl = item.Url;
        ConfigTab.ApplySnapshot(item.RequestSnapshot);

        if (item.ResponseSnapshot is not null)
        {
            ResponseSection.ApplyResult(
                ResultModel<ResponseSnapshotDto>.Success(item.ResponseSnapshot),
                item.RequestSnapshot);
        }

        StatusMessage = $"已加载 {item.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm} 的请求。";
    }

    [RelayCommand]
    private async Task SaveHistoryAsCaseAsync(RequestHistoryItemViewModel? item)
    {
        if (item is null) return;

        var snapshot = item.RequestSnapshot;
        var caseDto = new RequestCaseDto
        {
            Name = $"{snapshot.Method} {snapshot.Url}",
            GroupName = "历史导入",
            Description = $"从 {item.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm} 的历史记录创建",
            RequestSnapshot = snapshot,
            UpdatedAt = DateTime.UtcNow
        };

        await _requestCaseService.SaveAsync(caseDto, CancellationToken.None);
        await UseCasesPanel.LoadCasesAsync();
        StatusMessage = "已保存为用例。";
    }

    // --- Core request commands ---

    [RelayCommand]
    private async Task SendRequestAsync()
    {
        if (string.IsNullOrWhiteSpace(RequestUrl))
        {
            StatusMessage = "请求地址不能为空。";
            return;
        }

        IsBusy = true;
        var snapshot = ConfigTab.BuildRequestSnapshot(
            string.Empty,
            SelectedMethod,
            RequestUrl);
        var result = await _requestExecutionService.SendAsync(
            snapshot,
            EnvironmentPanel.ActiveEnvironmentName,
            CancellationToken.None);
        IsBusy = false;

        ResponseSection.ApplyResult(result, snapshot);
        StatusMessage = result.IsSuccess ? "请求发送完成。" : result.Message;

        // Save to history
        if (result.IsSuccess || result.Data is not null)
        {
            await _requestHistoryService.AddAsync(snapshot, result.Data, CancellationToken.None);
            await RefreshHistoryAsync();
        }
    }

    [RelayCommand]
    private async Task SaveCaseAsync()
    {
        var snapshot = ConfigTab.BuildRequestSnapshot(
            string.Empty,
            SelectedMethod,
            RequestUrl);
        await UseCasesPanel.SaveCaseAsync(snapshot);
        StatusMessage = "用例已保存。";
    }

    [RelayCommand]
    private async Task RefreshWorkspaceAsync()
    {
        IsBusy = true;
        await UseCasesPanel.LoadCasesAsync();
        await EnvironmentPanel.LoadVariablesAsync();
        await RefreshHistoryAsync();
        IsBusy = false;
    }

    // --- Private helpers ---

    private async Task RefreshHistoryAsync()
    {
        await HistoryPanel.LoadHistoryAsync();
        HasNoHistory = RequestHistory.Count == 0;
    }

    private void OnCaseApplied(RequestSnapshotDto snapshot)
    {
        SelectedMethod = snapshot.Method;
        RequestUrl = snapshot.Url;
        ConfigTab.ApplySnapshot(snapshot);
        StatusMessage = "已加载选中用例。";
    }
}
