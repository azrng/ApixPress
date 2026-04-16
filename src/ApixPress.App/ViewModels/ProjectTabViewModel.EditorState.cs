namespace ApixPress.App.ViewModels;

public partial class ProjectTabViewModel
{
    public string CurrentEditorTitle => ActiveWorkspaceTab?.EditorTitle ?? "新建...";
    public string CurrentEditorDescription => ActiveWorkspaceTab?.EditorDescription ?? "从下方卡片中选择要创建的工作内容。";
    public string CurrentEditorPrimaryActionText => ActiveWorkspaceTab?.PrimaryActionText ?? "保存";
    public string CurrentEditorUrlWatermark => ActiveWorkspaceTab?.UrlWatermark ?? "输入请求地址";
    public bool ShowEditorBaseUrlPrefix => IsHttpInterfaceEditor;
    public string CurrentEditorBaseUrlPrefix => IsHttpInterfaceEditor ? EnvironmentPanel.SelectedEnvironment?.BaseUrl ?? string.Empty : string.Empty;
    public string CurrentHttpInterfaceBaseUrl => IsHttpInterfaceEditor ? EnvironmentPanel.SelectedEnvironment?.BaseUrl ?? string.Empty : string.Empty;
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

    public bool IsHttpDebugEditorMode => ActiveWorkspaceTab?.IsHttpDebugView ?? false;
    public bool IsHttpDesignEditorMode => ActiveWorkspaceTab?.IsHttpDesignView ?? false;
    public bool IsHttpDocumentPreviewMode => ActiveWorkspaceTab?.IsHttpDocumentPreviewView ?? false;
    public bool ShowHttpWorkbenchContent => IsHttpInterfaceEditor && !IsHttpDocumentPreviewMode;
    public bool ShowHttpDocumentPreviewContent => IsHttpInterfaceEditor && IsHttpDocumentPreviewMode;
    public bool ShowSaveHttpCaseAction => IsHttpInterfaceEditor;
    public string CurrentEditorBaseUrlCaption => IsHttpInterfaceEditor
        ? (string.IsNullOrWhiteSpace(EnvironmentPanel.SelectedEnvironment?.BaseUrl) ? "当前环境未配置 BaseUrl" : EnvironmentPanel.SelectedEnvironment.BaseUrl)
        : IsQuickRequestEditor
            ? "完整地址"
            : string.Empty;
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
            NotifyWorkspaceEditorState();
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
            NotifyWorkspaceEditorState();
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
            NotifyWorkspaceEditorState();
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
            NotifyWorkspaceEditorState();
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
        NotifyWorkspaceEditorState();
    }
}
