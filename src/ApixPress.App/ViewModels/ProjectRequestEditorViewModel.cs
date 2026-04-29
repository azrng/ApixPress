using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Services.Implementations;
using ApixPress.App.ViewModels.Base;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace ApixPress.App.ViewModels;

public partial class ProjectRequestEditorViewModel : ViewModelBase
{
    private const int ResolvedPreviewUrlLengthLimit = 4096;
    private readonly ProjectTabWorkspaceContext _workspaceContext;
    private RequestWorkspaceTabViewModel? _subscribedWorkspaceTab;

    private string _currentEditorTitle = "新建...";
    private string _currentEditorDescription = "从下方卡片中选择要创建的工作内容。";
    private string _currentEditorPrimaryActionText = "保存";
    private string _currentEditorUrlWatermark = "输入请求地址";
    private string _currentHttpInterfaceBaseUrl = string.Empty;
    private string _currentHttpInterfaceDisplayName = "未命名接口";
    private bool _isQuickRequestEditor;
    private bool _isHttpInterfaceEditor;
    private bool _isHttpDebugEditorMode;
    private bool _isHttpDesignEditorMode;
    private bool _isHttpDocumentPreviewMode;
    private RequestEditorContentMode _currentContentMode;
    private bool _showHttpWorkbenchContent;
    private bool _showHttpDocumentPreviewContent;
    private bool _showSaveHttpCaseAction;
    private string _currentEditorBaseUrlCaption = string.Empty;
    private bool _hasResolvedRequestPreview;
    private string _resolvedRequestPreviewText = string.Empty;
    private bool _hasHttpDocumentParameters;
    private bool _hasHttpDocumentHeaders;
    private bool _hasHttpDocumentRequestDetails;
    private bool _showHttpDocumentRequestEmpty = true;
    private string _currentHttpDocumentBodyModeText = "无";
    private string _currentHttpDocumentUrl = string.Empty;
    private string _currentHttpDocumentResponseSummary = "等待调试后生成响应示例";
    private string _currentHttpDocumentBodyPreview = "{ }";
    private string _currentHttpDocumentCurlSnippet = string.Empty;
    private bool _canGenerateRequestCode;
    private string _currentResponseValidationResultText = "等待响应";

    internal ProjectRequestEditorViewModel(ProjectTabWorkspaceContext workspaceContext)
    {
        _workspaceContext = workspaceContext;
    }

    public RequestConfigTabViewModel ConfigTab => ResolveWorkspaceTabOrFallback().ConfigTab;
    public ResponseSectionViewModel ResponseSection => ResolveWorkspaceTabOrFallback().ResponseSection;

    public IReadOnlyList<string> HttpMethods { get; } = ["GET", "POST", "PUT", "DELETE", "PATCH"];

    public string CurrentEditorTitle => _currentEditorTitle;
    public string CurrentEditorDescription => _currentEditorDescription;
    public string CurrentEditorPrimaryActionText => _currentEditorPrimaryActionText;
    public string CurrentEditorUrlWatermark => _currentEditorUrlWatermark;
    public string CurrentHttpInterfaceBaseUrl => _currentHttpInterfaceBaseUrl;
    public string CurrentHttpInterfaceName
    {
        get => ResolveEditorRequestName("未命名接口", workspace => workspace.IsHttpInterfaceTab);
        set => SetEditorRequestName(value, "未命名接口");
    }

    public string CurrentHttpInterfaceDisplayName => _currentHttpInterfaceDisplayName;

    public string CurrentQuickRequestName
    {
        get => ResolveEditorRequestName("未命名请求", workspace => workspace.IsQuickRequestTab);
        set => SetEditorRequestName(value, "未命名请求");
    }

    public bool IsQuickRequestEditor => _isQuickRequestEditor;
    public bool IsHttpInterfaceEditor => _isHttpInterfaceEditor;
    public bool IsHttpDebugEditorMode => _isHttpDebugEditorMode;
    public bool IsHttpDesignEditorMode => _isHttpDesignEditorMode;
    public bool IsHttpDocumentPreviewMode => _isHttpDocumentPreviewMode;
    public RequestEditorContentMode CurrentContentMode => _currentContentMode;
    public bool ShowHttpWorkbenchContent => _showHttpWorkbenchContent;
    public bool ShowHttpDocumentPreviewContent => _showHttpDocumentPreviewContent;
    public bool ShowSaveHttpCaseAction => _showSaveHttpCaseAction;
    public string CurrentEditorBaseUrlCaption => _currentEditorBaseUrlCaption;
    public bool HasResolvedRequestPreview => _hasResolvedRequestPreview;
    public string ResolvedRequestPreviewText => _resolvedRequestPreviewText;
    public bool HasHttpDocumentParameters => _hasHttpDocumentParameters;
    public bool HasHttpDocumentHeaders => _hasHttpDocumentHeaders;
    public bool HasHttpDocumentRequestDetails => _hasHttpDocumentRequestDetails;
    public bool ShowHttpDocumentRequestEmpty => _showHttpDocumentRequestEmpty;
    public string CurrentHttpDocumentBodyModeText => _currentHttpDocumentBodyModeText;
    public string CurrentHttpDocumentUrl => _currentHttpDocumentUrl;
    public string CurrentHttpDocumentResponseSummary => _currentHttpDocumentResponseSummary;
    public string CurrentHttpDocumentBodyPreview => _currentHttpDocumentBodyPreview;
    public string CurrentHttpDocumentCurlSnippet => _currentHttpDocumentCurlSnippet;
    public bool CanGenerateRequestCode => _canGenerateRequestCode;
    public string CurrentRequestCodeTitle => RequestCodeTitle;
    public string CurrentRequestCodeCurlCommand => RequestCodeCurlCommand;
    public string CurrentRequestCodeWgetCommand => RequestCodeWgetCommand;
    public string CurrentRequestCodePowerShellCommand => RequestCodePowerShellCommand;
    public string CurrentResponseValidationResultText => _currentResponseValidationResultText;

    public string SelectedMethod
    {
        get => ActiveWorkspaceTab?.SelectedMethod ?? "GET";
        set
        {
            if (ActiveWorkspaceTab is null || ActiveWorkspaceTab.SelectedMethod == value)
            {
                return;
            }

            ActiveWorkspaceTab.SelectedMethod = value;
            NotifyStateChanged();
        }
    }

    public string RequestUrl
    {
        get => ActiveWorkspaceTab?.RequestUrl ?? string.Empty;
        set
        {
            var normalizedValue = NormalizeRequestUrlInput(value);
            if (ActiveWorkspaceTab is null || ActiveWorkspaceTab.RequestUrl == normalizedValue)
            {
                return;
            }

            ActiveWorkspaceTab.RequestUrl = normalizedValue;
            NotifyStateChanged();
        }
    }

    public string CurrentInterfaceFolderPath
    {
        get => ActiveWorkspaceTab?.InterfaceFolderPath ?? "默认模块";
        set
        {
            if (ActiveWorkspaceTab is null || ActiveWorkspaceTab.InterfaceFolderPath == value)
            {
                return;
            }

            ActiveWorkspaceTab.InterfaceFolderPath = value;
            NotifyStateChanged();
        }
    }

    public string CurrentHttpCaseName
    {
        get => ActiveWorkspaceTab?.HttpCaseName ?? "成功";
        set
        {
            if (ActiveWorkspaceTab is null || ActiveWorkspaceTab.HttpCaseName == value)
            {
                return;
            }

            ActiveWorkspaceTab.HttpCaseName = value;
            NotifyStateChanged();
        }
    }

    [ObservableProperty]
    private bool isRequestCodeDialogOpen;

    [ObservableProperty]
    private string requestCodeTitle = string.Empty;

    [ObservableProperty]
    private string requestCodeCurlCommand = string.Empty;

    [ObservableProperty]
    private string requestCodeWgetCommand = string.Empty;

    [ObservableProperty]
    private string requestCodePowerShellCommand = string.Empty;

    [ObservableProperty]
    private int selectedRequestCodeTab;

    [RelayCommand]
    private void OpenRequestCodeDialog()
    {
        if (!CanGenerateRequestCode)
        {
            return;
        }

        RefreshRequestCodeDialogContent();
        SelectedRequestCodeTab = 0;
        IsRequestCodeDialogOpen = true;
    }

    [RelayCommand]
    private void CloseRequestCodeDialog()
    {
        IsRequestCodeDialogOpen = false;
        ClearRequestCodeDialogContent();
    }

    [RelayCommand]
    private void ShowHttpDebugEditorMode()
    {
        if (ActiveWorkspaceTab is null || !ActiveWorkspaceTab.IsHttpInterfaceTab)
        {
            return;
        }

        ActiveWorkspaceTab.HttpEditorViewIndex = 0;
        NotifyStateChanged();
    }

    [RelayCommand]
    private void ShowHttpDesignEditorMode()
    {
        if (ActiveWorkspaceTab is null || !ActiveWorkspaceTab.IsHttpInterfaceTab)
        {
            return;
        }

        ActiveWorkspaceTab.HttpEditorViewIndex = 1;
        NotifyStateChanged();
    }

    [RelayCommand]
    private void ShowHttpDocumentPreviewMode()
    {
        if (ActiveWorkspaceTab is null || !ActiveWorkspaceTab.IsHttpInterfaceTab)
        {
            return;
        }

        ActiveWorkspaceTab.HttpEditorViewIndex = 2;
        NotifyStateChanged();
    }

    public void NotifyStateChanged()
    {
        EnsureActiveWorkspaceSubscriptions();
        OnPropertyChanged(nameof(ConfigTab));
        OnPropertyChanged(nameof(ResponseSection));
        OnPropertyChanged(nameof(CurrentHttpInterfaceName));
        OnPropertyChanged(nameof(CurrentQuickRequestName));
        RefreshComputedState();
        if (IsRequestCodeDialogOpen && CanGenerateRequestCode)
        {
            RefreshRequestCodeDialogContent();
        }
        else if (!CanGenerateRequestCode)
        {
            IsRequestCodeDialogOpen = false;
            ClearRequestCodeDialogContent();
        }
        OnPropertyChanged(nameof(SelectedMethod));
        OnPropertyChanged(nameof(RequestUrl));
        OnPropertyChanged(nameof(CurrentInterfaceFolderPath));
        OnPropertyChanged(nameof(CurrentHttpCaseName));
    }

    private RequestWorkspaceTabViewModel? ActiveWorkspaceTab => _workspaceContext.GetActiveWorkspaceTab();

    private RequestWorkspaceTabViewModel ResolveWorkspaceTabOrFallback()
    {
        return ActiveWorkspaceTab ?? _workspaceContext.GetFallbackWorkspaceTab();
    }

    private void EnsureActiveWorkspaceSubscriptions()
    {
        var activeWorkspaceTab = ActiveWorkspaceTab;
        if (ReferenceEquals(_subscribedWorkspaceTab, activeWorkspaceTab))
        {
            return;
        }

        DetachWorkspaceSubscriptions(_subscribedWorkspaceTab);
        _subscribedWorkspaceTab = activeWorkspaceTab;
        AttachWorkspaceSubscriptions(_subscribedWorkspaceTab);
    }

    private void AttachWorkspaceSubscriptions(RequestWorkspaceTabViewModel? workspaceTab)
    {
        if (workspaceTab is null)
        {
            return;
        }

        workspaceTab.ConfigTab.PropertyChanged += OnConfigTabPropertyChanged;
        workspaceTab.ConfigTab.QueryParameters.CollectionChanged += OnConfigCollectionChanged;
        workspaceTab.ConfigTab.PathParameters.CollectionChanged += OnConfigCollectionChanged;
        workspaceTab.ConfigTab.Headers.CollectionChanged += OnConfigCollectionChanged;
        workspaceTab.ConfigTab.FormFields.CollectionChanged += OnConfigCollectionChanged;
        workspaceTab.ResponseSection.PropertyChanged += OnResponseSectionPropertyChanged;
    }

    private void DetachWorkspaceSubscriptions(RequestWorkspaceTabViewModel? workspaceTab)
    {
        if (workspaceTab is null)
        {
            return;
        }

        workspaceTab.ConfigTab.PropertyChanged -= OnConfigTabPropertyChanged;
        workspaceTab.ConfigTab.QueryParameters.CollectionChanged -= OnConfigCollectionChanged;
        workspaceTab.ConfigTab.PathParameters.CollectionChanged -= OnConfigCollectionChanged;
        workspaceTab.ConfigTab.Headers.CollectionChanged -= OnConfigCollectionChanged;
        workspaceTab.ConfigTab.FormFields.CollectionChanged -= OnConfigCollectionChanged;
        workspaceTab.ResponseSection.PropertyChanged -= OnResponseSectionPropertyChanged;
    }

    private void OnConfigTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        if (e.PropertyName is nameof(RequestConfigTabViewModel.RequestName)
            or nameof(RequestConfigTabViewModel.RequestDescription)
            or nameof(RequestConfigTabViewModel.RequestBody)
            or nameof(RequestConfigTabViewModel.SelectedBodyMode)
            or nameof(RequestConfigTabViewModel.SelectedBodyModeOption)
            or nameof(RequestConfigTabViewModel.IgnoreSslErrors))
        {
            OnPropertyChanged(nameof(CurrentHttpInterfaceName));
            OnPropertyChanged(nameof(CurrentQuickRequestName));
            RefreshComputedState();
        }
    }

    private void OnConfigCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        RefreshComputedState();
    }

    private void OnResponseSectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        if (e.PropertyName is nameof(ResponseSectionViewModel.HasResponse)
            or nameof(ResponseSectionViewModel.StatusText)
            or nameof(ResponseSectionViewModel.BodyText))
        {
            RefreshComputedState();
        }
    }

    private void RefreshRequestCodeDialogContent()
    {
        RequestCodeTitle = "生成代码";
        RequestCodeCurlCommand = BuildRequestCodeCurlCommand();
        RequestCodeWgetCommand = BuildRequestCodeWgetCommand();
        RequestCodePowerShellCommand = BuildRequestCodePowerShellCommand();
        OnPropertyChanged(nameof(CurrentRequestCodeTitle));
        OnPropertyChanged(nameof(CurrentRequestCodeCurlCommand));
        OnPropertyChanged(nameof(CurrentRequestCodeWgetCommand));
        OnPropertyChanged(nameof(CurrentRequestCodePowerShellCommand));
    }

    private string BuildRequestCodeCurlCommand()
    {
        if (!CanGenerateRequestCode)
        {
            return "请先打开一个 HTTP 接口或快捷请求标签。";
        }

        try
        {
            return ProjectHttpDocumentFormatter.BuildCurlSnippet(SelectedMethod, RequestUrl, CurrentHttpInterfaceBaseUrl, ConfigTab);
        }
        catch (Exception exception)
        {
            return $"生成请求代码失败：{exception.Message}";
        }
    }

    private string BuildRequestCodeWgetCommand()
    {
        if (!CanGenerateRequestCode)
        {
            return "请先打开一个 HTTP 接口或快捷请求标签。";
        }

        try
        {
            return ProjectHttpDocumentFormatter.BuildWgetSnippet(SelectedMethod, RequestUrl, CurrentHttpInterfaceBaseUrl, ConfigTab);
        }
        catch (Exception exception)
        {
            return $"生成 wget 代码失败：{exception.Message}";
        }
    }

    private string BuildRequestCodePowerShellCommand()
    {
        if (!CanGenerateRequestCode)
        {
            return "请先打开一个 HTTP 接口或快捷请求标签。";
        }

        try
        {
            return ProjectHttpDocumentFormatter.BuildPowerShellSnippet(SelectedMethod, RequestUrl, CurrentHttpInterfaceBaseUrl, ConfigTab);
        }
        catch (Exception exception)
        {
            return $"生成 PowerShell 代码失败：{exception.Message}";
        }
    }

    private void ClearRequestCodeDialogContent()
    {
        RequestCodeTitle = string.Empty;
        RequestCodeCurlCommand = string.Empty;
        RequestCodeWgetCommand = string.Empty;
        RequestCodePowerShellCommand = string.Empty;
        SelectedRequestCodeTab = 0;
        OnPropertyChanged(nameof(CurrentRequestCodeTitle));
        OnPropertyChanged(nameof(CurrentRequestCodeCurlCommand));
        OnPropertyChanged(nameof(CurrentRequestCodeWgetCommand));
        OnPropertyChanged(nameof(CurrentRequestCodePowerShellCommand));
    }

    private string BuildResolvedRequestPreviewText()
    {
        var workspace = ActiveWorkspaceTab;
        if (workspace is null || workspace.IsLandingTab || string.IsNullOrWhiteSpace(workspace.RequestUrl))
        {
            return string.Empty;
        }

        if (workspace.RequestUrl.Length > ResolvedPreviewUrlLengthLimit)
        {
            return "发送预览不可用：请求地址过长，请确认粘贴内容是否只包含 URL。";
        }

        if (workspace.RequestUrl.Contains('\r') || workspace.RequestUrl.Contains('\n'))
        {
            return "发送预览不可用：请求地址包含换行，请确认粘贴内容是否只包含 URL。";
        }

        var variables = new Dictionary<string, string>(_workspaceContext.GetActiveVariables(), StringComparer.OrdinalIgnoreCase);
        var baseUrl = RequestExecutionService.ReplaceVariables(_workspaceContext.GetCurrentBaseUrl(), variables);
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            variables["baseUrl"] = baseUrl;
        }

        try
        {
            var snapshot = workspace.BuildSnapshot();
            var finalUrl = RequestExecutionService.BuildUrl(snapshot, baseUrl, variables);
            return $"发送预览：{snapshot.Method.ToUpperInvariant()} {finalUrl}";
        }
        catch (Exception exception)
        {
            return $"发送预览不可用：{exception.Message}";
        }
    }

    private void RefreshComputedState()
    {
        var workspace = ActiveWorkspaceTab;
        var configTab = ResolveWorkspaceTabOrFallback().ConfigTab;
        var responseSection = ResolveWorkspaceTabOrFallback().ResponseSection;
        var currentHttpInterfaceName = ResolveEditorRequestName("未命名接口", tab => tab.IsHttpInterfaceTab);

        SetComputedProperty(ref _currentEditorTitle, workspace?.EditorTitle ?? "新建...", nameof(CurrentEditorTitle));
        SetComputedProperty(ref _currentEditorDescription, workspace?.EditorDescription ?? "从下方卡片中选择要创建的工作内容。", nameof(CurrentEditorDescription));
        SetComputedProperty(ref _currentEditorPrimaryActionText, workspace?.PrimaryActionText ?? "保存", nameof(CurrentEditorPrimaryActionText));
        SetComputedProperty(ref _currentEditorUrlWatermark, workspace?.UrlWatermark ?? "输入请求地址", nameof(CurrentEditorUrlWatermark));
        SetComputedProperty(ref _currentHttpInterfaceBaseUrl, workspace?.IsHttpInterfaceTab == true ? _workspaceContext.GetCurrentBaseUrl() : string.Empty, nameof(CurrentHttpInterfaceBaseUrl));
        SetComputedProperty(ref _currentHttpInterfaceDisplayName, string.IsNullOrWhiteSpace(currentHttpInterfaceName) ? "未命名接口" : currentHttpInterfaceName.Trim(), nameof(CurrentHttpInterfaceDisplayName));
        SetComputedProperty(ref _isQuickRequestEditor, workspace?.IsQuickRequestTab ?? false, nameof(IsQuickRequestEditor));
        SetComputedProperty(ref _isHttpInterfaceEditor, workspace?.IsHttpInterfaceTab ?? false, nameof(IsHttpInterfaceEditor));
        SetComputedProperty(ref _isHttpDebugEditorMode, workspace?.IsHttpDebugView ?? false, nameof(IsHttpDebugEditorMode));
        SetComputedProperty(ref _isHttpDesignEditorMode, workspace?.IsHttpDesignView ?? false, nameof(IsHttpDesignEditorMode));
        SetComputedProperty(ref _isHttpDocumentPreviewMode, workspace?.IsHttpDocumentPreviewView ?? false, nameof(IsHttpDocumentPreviewMode));
        SetComputedProperty(ref _currentContentMode, ResolveCurrentContentMode(), nameof(CurrentContentMode));
        SetComputedProperty(ref _showHttpWorkbenchContent, _isHttpInterfaceEditor && !_isHttpDocumentPreviewMode, nameof(ShowHttpWorkbenchContent));
        SetComputedProperty(ref _showHttpDocumentPreviewContent, _isHttpInterfaceEditor && _isHttpDocumentPreviewMode, nameof(ShowHttpDocumentPreviewContent));
        SetComputedProperty(ref _showSaveHttpCaseAction, _isHttpInterfaceEditor, nameof(ShowSaveHttpCaseAction));
        SetComputedProperty(ref _currentEditorBaseUrlCaption, ResolveCurrentEditorBaseUrlCaption(), nameof(CurrentEditorBaseUrlCaption));

        var resolvedRequestPreviewText = BuildResolvedRequestPreviewText();
        SetComputedProperty(ref _resolvedRequestPreviewText, resolvedRequestPreviewText, nameof(ResolvedRequestPreviewText));
        SetComputedProperty(ref _hasResolvedRequestPreview, !string.IsNullOrWhiteSpace(resolvedRequestPreviewText), nameof(HasResolvedRequestPreview));

        var hasHttpDocumentParameters = configTab.QueryParameters.Count > 0;
        var hasHttpDocumentHeaders = configTab.Headers.Count > 0;
        var hasHttpDocumentRequestDetails = hasHttpDocumentParameters || hasHttpDocumentHeaders || configTab.HasBodyContent;
        SetComputedProperty(ref _hasHttpDocumentParameters, hasHttpDocumentParameters, nameof(HasHttpDocumentParameters));
        SetComputedProperty(ref _hasHttpDocumentHeaders, hasHttpDocumentHeaders, nameof(HasHttpDocumentHeaders));
        SetComputedProperty(ref _hasHttpDocumentRequestDetails, hasHttpDocumentRequestDetails, nameof(HasHttpDocumentRequestDetails));
        SetComputedProperty(ref _showHttpDocumentRequestEmpty, !hasHttpDocumentRequestDetails, nameof(ShowHttpDocumentRequestEmpty));
        SetComputedProperty(ref _currentHttpDocumentBodyModeText, configTab.HasBodyContent
            ? (configTab.SelectedBodyModeOption?.DisplayName ?? configTab.SelectedBodyMode)
            : "无", nameof(CurrentHttpDocumentBodyModeText));
        SetComputedProperty(ref _currentHttpDocumentUrl, ResolveCurrentHttpDocumentUrl(), nameof(CurrentHttpDocumentUrl));
        SetComputedProperty(ref _currentResponseValidationResultText, ResolveCurrentResponseValidationResultText(responseSection), nameof(CurrentResponseValidationResultText));
        SetComputedProperty(ref _currentHttpDocumentResponseSummary, responseSection.HasResponse
            ? _currentResponseValidationResultText
            : "等待调试后生成响应示例", nameof(CurrentHttpDocumentResponseSummary));
        SetComputedProperty(ref _currentHttpDocumentBodyPreview, responseSection.HasResponse && !string.IsNullOrWhiteSpace(responseSection.BodyText)
            ? responseSection.BodyText
            : "{ }", nameof(CurrentHttpDocumentBodyPreview));
        SetComputedProperty(ref _currentHttpDocumentCurlSnippet, ResolveCurrentHttpDocumentCurlSnippet(), nameof(CurrentHttpDocumentCurlSnippet));
        SetComputedProperty(ref _canGenerateRequestCode, workspace is not null && !workspace.IsLandingTab, nameof(CanGenerateRequestCode));
    }

    private string ResolveCurrentEditorBaseUrlCaption()
    {
        if (_isHttpInterfaceEditor)
        {
            return string.IsNullOrWhiteSpace(_workspaceContext.GetCurrentBaseUrl())
                ? "当前环境未配置 BaseUrl"
                : _workspaceContext.GetCurrentBaseUrl();
        }

        return _isQuickRequestEditor ? "完整地址" : string.Empty;
    }

    private string ResolveCurrentHttpDocumentUrl()
    {
        try
        {
            return ProjectHttpDocumentFormatter.BuildUrl(RequestUrl, CurrentHttpInterfaceBaseUrl, ConfigTab.QueryParameters);
        }
        catch (Exception exception)
        {
            return $"请求地址预览不可用：{exception.Message}";
        }
    }

    private string ResolveCurrentHttpDocumentCurlSnippet()
    {
        try
        {
            return ProjectHttpDocumentFormatter.BuildCurlSnippet(SelectedMethod, RequestUrl, CurrentHttpInterfaceBaseUrl, ConfigTab);
        }
        catch (Exception exception)
        {
            return $"请求示例生成失败：{exception.Message}";
        }
    }

    private static string ResolveCurrentResponseValidationResultText(ResponseSectionViewModel responseSection)
    {
        if (string.IsNullOrWhiteSpace(responseSection.StatusText))
        {
            return "等待响应";
        }

        if (string.Equals(responseSection.StatusText, "请求失败", StringComparison.OrdinalIgnoreCase))
        {
            return "请求失败";
        }

        if (responseSection.StatusText.StartsWith("HTTP ", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(responseSection.StatusText["HTTP ".Length..], out var code))
        {
            return code is >= 200 and < 300 ? $"成功 ({code})" : $"HTTP {code}";
        }

        return responseSection.StatusText;
    }

    private string ResolveEditorRequestName(string fallbackName, Func<RequestWorkspaceTabViewModel, bool> matchEditorType)
    {
        if (ActiveWorkspaceTab is null)
        {
            return string.Empty;
        }

        var currentName = ActiveWorkspaceTab.ConfigTab.RequestName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(currentName))
        {
            return fallbackName;
        }

        return matchEditorType(ActiveWorkspaceTab)
            && string.Equals(currentName, ActiveWorkspaceTab.ResolveGeneratedRequestName(), StringComparison.Ordinal)
                ? fallbackName
                : currentName;
    }

    private void SetEditorRequestName(string? value, string fallbackName)
    {
        if (ActiveWorkspaceTab is null)
        {
            return;
        }

        var normalizedValue = string.Equals(value?.Trim(), fallbackName, StringComparison.Ordinal)
            ? string.Empty
            : value?.Trim() ?? string.Empty;
        if (ActiveWorkspaceTab.ConfigTab.RequestName == normalizedValue)
        {
            return;
        }

        ActiveWorkspaceTab.ConfigTab.RequestName = normalizedValue;
        NotifyStateChanged();
    }

    private RequestEditorContentMode ResolveCurrentContentMode()
    {
        if (ActiveWorkspaceTab is null || ActiveWorkspaceTab.IsLandingTab)
        {
            return RequestEditorContentMode.None;
        }

        if (ActiveWorkspaceTab.IsQuickRequestTab)
        {
            return RequestEditorContentMode.QuickRequest;
        }

        if (ActiveWorkspaceTab.IsHttpDocumentPreviewView)
        {
            return RequestEditorContentMode.HttpDocumentPreview;
        }

        return ActiveWorkspaceTab.IsHttpInterfaceTab
            ? RequestEditorContentMode.HttpWorkbench
            : RequestEditorContentMode.None;
    }

    private void SetComputedProperty<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    internal static string NormalizeRequestUrlInput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var urlMatch = HttpUrlRegex().Match(trimmed);
        if (urlMatch.Success)
        {
            return CleanUrlCandidate(urlMatch.Value);
        }

        return trimmed
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
    }

    private static string CleanUrlCandidate(string value)
    {
        return value
            .Trim()
            .Trim('\'', '"', '`', '<', '>')
            .TrimEnd('\\', ',', ';');
    }

    [GeneratedRegex("https?://[^\\s'\\\"<>]+", RegexOptions.IgnoreCase)]
    private static partial Regex HttpUrlRegex();

    protected override void DisposeManaged()
    {
        DetachWorkspaceSubscriptions(_subscribedWorkspaceTab);
        _subscribedWorkspaceTab = null;
    }
}
