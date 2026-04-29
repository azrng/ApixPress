using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Services.Implementations;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ProjectRequestEditorViewModel : ViewModelBase
{
    private readonly ProjectTabWorkspaceContext _workspaceContext;

    internal ProjectRequestEditorViewModel(ProjectTabWorkspaceContext workspaceContext)
    {
        _workspaceContext = workspaceContext;
    }

    public RequestConfigTabViewModel ConfigTab => ResolveWorkspaceTabOrFallback().ConfigTab;
    public ResponseSectionViewModel ResponseSection => ResolveWorkspaceTabOrFallback().ResponseSection;

    public IReadOnlyList<string> HttpMethods { get; } = ["GET", "POST", "PUT", "DELETE", "PATCH"];

    public string CurrentEditorTitle => ActiveWorkspaceTab?.EditorTitle ?? "新建...";
    public string CurrentEditorDescription => ActiveWorkspaceTab?.EditorDescription ?? "从下方卡片中选择要创建的工作内容。";
    public string CurrentEditorPrimaryActionText => ActiveWorkspaceTab?.PrimaryActionText ?? "保存";
    public string CurrentEditorUrlWatermark => ActiveWorkspaceTab?.UrlWatermark ?? "输入请求地址";
    public string CurrentHttpInterfaceBaseUrl => IsHttpInterfaceEditor ? _workspaceContext.GetCurrentBaseUrl() : string.Empty;
    public string CurrentHttpInterfaceName
    {
        get => ResolveEditorRequestName("未命名接口", workspace => workspace.IsHttpInterfaceTab);
        set => SetEditorRequestName(value, "未命名接口");
    }

    public string CurrentHttpInterfaceDisplayName => string.IsNullOrWhiteSpace(CurrentHttpInterfaceName)
        ? "未命名接口"
        : CurrentHttpInterfaceName.Trim();

    public string CurrentQuickRequestName
    {
        get => ResolveEditorRequestName("未命名请求", workspace => workspace.IsQuickRequestTab);
        set => SetEditorRequestName(value, "未命名请求");
    }

    public bool IsQuickRequestEditor => ActiveWorkspaceTab?.IsQuickRequestTab ?? false;
    public bool IsHttpInterfaceEditor => ActiveWorkspaceTab?.IsHttpInterfaceTab ?? false;
    public bool IsHttpDebugEditorMode => ActiveWorkspaceTab?.IsHttpDebugView ?? false;
    public bool IsHttpDesignEditorMode => ActiveWorkspaceTab?.IsHttpDesignView ?? false;
    public bool IsHttpDocumentPreviewMode => ActiveWorkspaceTab?.IsHttpDocumentPreviewView ?? false;
    public RequestEditorContentMode CurrentContentMode => ResolveCurrentContentMode();
    public bool ShowHttpWorkbenchContent => IsHttpInterfaceEditor && !IsHttpDocumentPreviewMode;
    public bool ShowHttpDocumentPreviewContent => IsHttpInterfaceEditor && IsHttpDocumentPreviewMode;
    public bool ShowSaveHttpCaseAction => IsHttpInterfaceEditor;
    public string CurrentEditorBaseUrlCaption => IsHttpInterfaceEditor
        ? (string.IsNullOrWhiteSpace(_workspaceContext.GetCurrentBaseUrl()) ? "当前环境未配置 BaseUrl" : _workspaceContext.GetCurrentBaseUrl())
        : IsQuickRequestEditor
            ? "完整地址"
            : string.Empty;
    public bool HasResolvedRequestPreview => !string.IsNullOrWhiteSpace(ResolvedRequestPreviewText);
    public string ResolvedRequestPreviewText => BuildResolvedRequestPreviewText();
    public bool HasHttpDocumentParameters => ConfigTab.QueryParameters.Count > 0;
    public bool HasHttpDocumentHeaders => ConfigTab.Headers.Count > 0;
    public bool HasHttpDocumentRequestDetails => HasHttpDocumentParameters || HasHttpDocumentHeaders || ConfigTab.HasBodyContent;
    public bool ShowHttpDocumentRequestEmpty => !HasHttpDocumentRequestDetails;
    public string CurrentHttpDocumentBodyModeText => ConfigTab.HasBodyContent
        ? (ConfigTab.SelectedBodyModeOption?.DisplayName ?? ConfigTab.SelectedBodyMode)
        : "无";
    public string CurrentHttpDocumentUrl => ProjectHttpDocumentFormatter.BuildUrl(RequestUrl, CurrentHttpInterfaceBaseUrl, ConfigTab.QueryParameters);
    public string CurrentHttpDocumentResponseSummary => ResponseSection.HasResponse
        ? CurrentResponseValidationResultText
        : "等待调试后生成响应示例";
    public string CurrentHttpDocumentBodyPreview => ResponseSection.HasResponse && !string.IsNullOrWhiteSpace(ResponseSection.BodyText)
        ? ResponseSection.BodyText
        : "{ }";
    public string CurrentHttpDocumentCurlSnippet => ProjectHttpDocumentFormatter.BuildCurlSnippet(SelectedMethod, RequestUrl, CurrentHttpInterfaceBaseUrl, ConfigTab);
    public string CurrentResponseValidationResultText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ResponseSection.StatusText))
            {
                return "等待响应";
            }

            if (string.Equals(ResponseSection.StatusText, "请求失败", StringComparison.OrdinalIgnoreCase))
            {
                return "请求失败";
            }

            if (ResponseSection.StatusText.StartsWith("HTTP ", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(ResponseSection.StatusText["HTTP ".Length..], out var code))
            {
                return code is >= 200 and < 300 ? $"成功 ({code})" : $"HTTP {code}";
            }

            return ResponseSection.StatusText;
        }
    }

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
            if (ActiveWorkspaceTab is null || ActiveWorkspaceTab.RequestUrl == value)
            {
                return;
            }

            ActiveWorkspaceTab.RequestUrl = value;
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
        OnPropertyChanged(nameof(ConfigTab));
        OnPropertyChanged(nameof(ResponseSection));
        OnPropertyChanged(nameof(CurrentEditorTitle));
        OnPropertyChanged(nameof(CurrentEditorDescription));
        OnPropertyChanged(nameof(CurrentEditorPrimaryActionText));
        OnPropertyChanged(nameof(CurrentEditorUrlWatermark));
        OnPropertyChanged(nameof(CurrentHttpInterfaceBaseUrl));
        OnPropertyChanged(nameof(CurrentHttpInterfaceName));
        OnPropertyChanged(nameof(CurrentHttpInterfaceDisplayName));
        OnPropertyChanged(nameof(CurrentQuickRequestName));
        OnPropertyChanged(nameof(IsQuickRequestEditor));
        OnPropertyChanged(nameof(IsHttpInterfaceEditor));
        OnPropertyChanged(nameof(IsHttpDebugEditorMode));
        OnPropertyChanged(nameof(IsHttpDesignEditorMode));
        OnPropertyChanged(nameof(IsHttpDocumentPreviewMode));
        OnPropertyChanged(nameof(CurrentContentMode));
        OnPropertyChanged(nameof(ShowHttpWorkbenchContent));
        OnPropertyChanged(nameof(ShowHttpDocumentPreviewContent));
        OnPropertyChanged(nameof(ShowSaveHttpCaseAction));
        OnPropertyChanged(nameof(CurrentEditorBaseUrlCaption));
        OnPropertyChanged(nameof(HasResolvedRequestPreview));
        OnPropertyChanged(nameof(ResolvedRequestPreviewText));
        OnPropertyChanged(nameof(HasHttpDocumentParameters));
        OnPropertyChanged(nameof(HasHttpDocumentHeaders));
        OnPropertyChanged(nameof(HasHttpDocumentRequestDetails));
        OnPropertyChanged(nameof(ShowHttpDocumentRequestEmpty));
        OnPropertyChanged(nameof(CurrentHttpDocumentBodyModeText));
        OnPropertyChanged(nameof(CurrentHttpDocumentUrl));
        OnPropertyChanged(nameof(CurrentHttpDocumentResponseSummary));
        OnPropertyChanged(nameof(CurrentHttpDocumentBodyPreview));
        OnPropertyChanged(nameof(CurrentHttpDocumentCurlSnippet));
        OnPropertyChanged(nameof(CurrentResponseValidationResultText));
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

    private string BuildResolvedRequestPreviewText()
    {
        var workspace = ActiveWorkspaceTab;
        if (workspace is null || workspace.IsLandingTab || string.IsNullOrWhiteSpace(workspace.RequestUrl))
        {
            return string.Empty;
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
}
