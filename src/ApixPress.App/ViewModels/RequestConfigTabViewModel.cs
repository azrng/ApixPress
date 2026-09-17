using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class RequestConfigTabViewModel : ViewModelBase
{
    private readonly IFilePickerService? _filePickerService;
    private readonly List<RequestParameterItemViewModel> _subscribedQueryParameterItems = [];
    private bool _isUpdatingQueryParametersSelectionState;
    private bool? _queryParametersSelectionState;
    // 空状态时也要完整容纳“区域标题 + 表头 + 空提示”，过小会触发 Avalonia Grid 按比例压缩内容，导致副标题被裁剪。
    // 预算需覆盖整卡高度：URL 行（约 54px）+ 页签行（约 36px）+ 内容区（边距 12 + 区域栏 40 + 间距 8 + 表格最小高 114）
    private const double CompactConfigPanelMaxHeight = 288;
    private const double ExpandedConfigPanelMaxHeight = 420;
    private static readonly JsonSerializerOptions FormattedJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private string _jsonBodyFormatStatus = string.Empty;
    private string _bodyExampleTemplate = string.Empty;

    public BatchObservableCollection<RequestParameterItemViewModel> QueryParameters { get; } = [];

    public BatchObservableCollection<RequestParameterItemViewModel> PathParameters { get; } = [];

    public BatchObservableCollection<RequestParameterItemViewModel> Headers { get; } = [];

    public BatchObservableCollection<RequestParameterItemViewModel> FormFields { get; } = [];

    public ObservableCollection<BodyModeOptionViewModel> BodyModeOptions { get; } = [];

    [ObservableProperty]
    private string requestName = string.Empty;

    [ObservableProperty]
    private string requestDescription = string.Empty;

    [ObservableProperty]
    private string requestBody = string.Empty;

    [ObservableProperty]
    private string selectedBodyMode = BodyModes.None;

    [ObservableProperty]
    private BodyModeOptionViewModel? selectedBodyModeOption;

    [ObservableProperty]
    private bool ignoreSslErrors;

    [ObservableProperty]
    private int selectedTabIndex;

    /// <summary>Binary 模式已选文件名（由 RequestBody 路径推导）。</summary>
    [ObservableProperty]
    private string binaryFileName = string.Empty;

    /// <summary>Binary 模式已选文件大小展示文本；文件被外部移动/删除时提示"文件不存在"。</summary>
    [ObservableProperty]
    private string binaryFileSizeText = string.Empty;


    public RequestConfigTabViewModel() : this(null) { }

    public RequestConfigTabViewModel(IFilePickerService? filePickerService)
    {
        _filePickerService = filePickerService;

        var modes = new (string Mode, string DisplayName)[]
        {
            (BodyModes.None, "none"),
            (BodyModes.FormData, "form-data"),
            (BodyModes.FormUrlEncoded, "x-www-form-urlencoded"),
            (BodyModes.RawJson, "JSON"),
            (BodyModes.RawXml, "XML"),
            (BodyModes.RawText, "Text"),
            (BodyModes.Binary, "Binary"),
        };

        foreach (var (mode, displayName) in modes)
        {
            BodyModeOptions.Add(new BodyModeOptionViewModel { Mode = mode, DisplayName = displayName });
        }

        QueryParameters.CollectionChanged += OnQueryParametersCollectionChanged;
        Headers.CollectionChanged += OnHeadersCollectionChanged;
        FormFields.CollectionChanged += OnFormFieldsCollectionChanged;
        SelectedBodyModeOption = BodyModeOptions[0];
        UpdateQueryParametersSelectionState();
    }

    // --- Visibility helpers for the Body tab UI ---

    public bool IsBodyModeFormData =>
        SelectedBodyMode == BodyModes.FormData || SelectedBodyMode == BodyModes.FormUrlEncoded;

    public bool HasFormFields => FormFields.Count > 0;

    public bool ShowFormDataEmptyState => IsBodyModeFormData && !HasFormFields;

    public bool HasQueryParameters => QueryParameters.Count > 0;

    public string QueryParameterCountText => QueryParameters.Count.ToString();

    public bool ShowQueryParametersEmptyState => !HasQueryParameters;

    public bool HasHeaders => Headers.Count > 0;

    public string HeaderCountText => Headers.Count.ToString();

    public bool ShowHeadersEmptyState => !HasHeaders;

    public bool ShowBodyModeEmptyState => SelectedBodyMode == BodyModes.None;

    public bool HasRawBodyEditor =>
        SelectedBodyMode is BodyModes.RawJson or BodyModes.RawXml or BodyModes.RawText;

    public bool IsRawJsonBodyMode => SelectedBodyMode == BodyModes.RawJson;

    public bool CanFormatJsonBody => IsRawJsonBodyMode && !string.IsNullOrWhiteSpace(RequestBody);

    /// <summary>导入接口存在明确的 JSON/XML 示例时允许主动覆盖当前请求体。</summary>
    public bool CanGenerateBodyExample => SelectedBodyMode is BodyModes.RawJson or BodyModes.RawXml
        && !string.IsNullOrWhiteSpace(_bodyExampleTemplate);

    /// <summary>仅 FormData 模式承载文件字段；FormUrlEncoded 与其他模式只按文本发送。</summary>
    public bool IsFormDataOnly => SelectedBodyMode == BodyModes.FormData;

    public bool HasBinaryFile => SelectedBodyMode == BodyModes.Binary && !string.IsNullOrWhiteSpace(RequestBody);

    public bool ShowBinaryEmptyState => SelectedBodyMode == BodyModes.Binary && !HasBinaryFile;

    public bool HasBodyContent => SelectedBodyMode != BodyModes.None;

    public double ConfigPanelMaxHeight => SelectedTabIndex switch
    {
        0 when QueryParameters.Count == 0 => CompactConfigPanelMaxHeight,
        1 when !HasExpandableBodyConfig => CompactConfigPanelMaxHeight,
        2 when Headers.Count == 0 => CompactConfigPanelMaxHeight,
        _ => ExpandedConfigPanelMaxHeight
    };

    private bool HasExpandableBodyConfig => HasRawBodyEditor || HasFormFields;

    public bool? QueryParametersSelectionState
    {
        get => _queryParametersSelectionState;
        set
        {
            if (_queryParametersSelectionState == value)
            {
                return;
            }

            _queryParametersSelectionState = value;
            OnPropertyChanged(nameof(QueryParametersSelectionState));

            if (_isUpdatingQueryParametersSelectionState || value is null)
            {
                return;
            }

            ApplyQueryParametersSelectionState(value.Value);
        }
    }

    public string RequestBodyWatermark => SelectedBodyMode switch
    {
        BodyModes.RawJson => "输入 JSON 内容",
        BodyModes.RawXml => "输入 XML 内容",
        BodyModes.RawText => "输入纯文本内容",
        _ => "请求体内容"
    };

    public string BodyModeHint => !string.IsNullOrWhiteSpace(_jsonBodyFormatStatus)
        ? _jsonBodyFormatStatus
        : SelectedBodyMode switch
        {
            BodyModes.FormData => "按字段组合为 multipart/form-data 请求体，file 类型字段可携带多个文件。",
            BodyModes.FormUrlEncoded => "按字段进行 URL 编码，适合传统表单接口。",
            BodyModes.RawJson => "以 application/json 发送原始 JSON 内容。",
            BodyModes.RawXml => "以 application/xml 发送原始 XML 内容。",
            BodyModes.RawText => "以 text/plain 发送纯文本内容。",
            BodyModes.Binary => "将所选文件以 application/octet-stream 作为请求体发送。",
            _ => "不发送请求体。"
        };

    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(ConfigPanelMaxHeight));
    }

    partial void OnSelectedBodyModeOptionChanged(BodyModeOptionViewModel? value)
    {
        if (value is not null)
            SelectedBodyMode = value.Mode;
    }

    partial void OnRequestBodyChanged(string value)
    {
        ClearJsonBodyFormatStatus();
        RefreshBinaryFileState();
        FormatJsonBodyCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedBodyModeChanged(string value)
    {
        ClearJsonBodyFormatStatus();
        OnPropertyChanged(nameof(IsBodyModeFormData));
        OnPropertyChanged(nameof(IsFormDataOnly));
        OnPropertyChanged(nameof(ShowFormDataEmptyState));
        OnPropertyChanged(nameof(HasRawBodyEditor));
        OnPropertyChanged(nameof(HasBodyContent));
        OnPropertyChanged(nameof(ShowBodyModeEmptyState));
        OnPropertyChanged(nameof(HasBinaryFile));
        OnPropertyChanged(nameof(ShowBinaryEmptyState));
        OnPropertyChanged(nameof(RequestBodyWatermark));
        OnPropertyChanged(nameof(BodyModeHint));
        OnPropertyChanged(nameof(ConfigPanelMaxHeight));
        OnPropertyChanged(nameof(IsRawJsonBodyMode));
        OnPropertyChanged(nameof(CanGenerateBodyExample));
        FormatJsonBodyCommand.NotifyCanExecuteChanged();
        GenerateBodyExampleCommand.NotifyCanExecuteChanged();
        RefreshBinaryFileState();

        // Sync the option selection if changed programmatically
        var match = BodyModeOptions.FirstOrDefault(o => o.Mode == value);
        if (match is not null && SelectedBodyModeOption != match)
            SelectedBodyModeOption = match;
    }

    /// <summary>Binary 文件展示态统一从 RequestBody 路径推导，选择、清除与程序化赋值共用一条口径。</summary>
    private void RefreshBinaryFileState()
    {
        if (SelectedBodyMode != BodyModes.Binary)
        {
            BinaryFileName = string.Empty;
            BinaryFileSizeText = string.Empty;
            return;
        }

        BinaryFileName = System.IO.Path.GetFileName(RequestBody);
        BinaryFileSizeText = File.Exists(RequestBody)
            ? FormFieldFileViewModel.FormatSize(new FileInfo(RequestBody).Length)
            : "文件不存在";
    }

    private void OnFormFieldsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnFormFieldsChanged();
    }

    private void OnFormFieldsChanged()
    {
        OnPropertyChanged(nameof(HasFormFields));
        OnPropertyChanged(nameof(ShowFormDataEmptyState));
        OnPropertyChanged(nameof(ConfigPanelMaxHeight));
    }

    // --- Commands ---

    [RelayCommand(CanExecute = nameof(CanFormatJsonBody))]
    private void FormatJsonBody()
    {
        if (!IsRawJsonBodyMode)
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(RequestBody);
            RequestBody = JsonSerializer.Serialize(document.RootElement, FormattedJsonOptions);
        }
        catch (JsonException)
        {
            _jsonBodyFormatStatus = "JSON 格式无效，未修改请求体。";
            OnPropertyChanged(nameof(BodyModeHint));
        }
    }

    [RelayCommand(CanExecute = nameof(CanGenerateBodyExample))]
    private void GenerateBodyExample()
    {
        if (!CanGenerateBodyExample)
        {
            return;
        }

        RequestBody = _bodyExampleTemplate;
    }

    private void ClearJsonBodyFormatStatus()
    {
        if (string.IsNullOrWhiteSpace(_jsonBodyFormatStatus))
        {
            return;
        }

        _jsonBodyFormatStatus = string.Empty;
        OnPropertyChanged(nameof(BodyModeHint));
    }

    [RelayCommand]
    private void AddQueryParameter()
    {
        QueryParameters.Add(new RequestParameterItemViewModel
        {
            ParameterType = RequestParameterKind.Query,
            Name = string.Empty,
            Value = string.Empty
        });
    }

    [RelayCommand]
    private void AddHeader()
    {
        Headers.Add(new RequestParameterItemViewModel
        {
            ParameterType = RequestParameterKind.Header,
            Name = string.Empty,
            Value = string.Empty
        });
    }

    [RelayCommand]
    private void AddFormField()
    {
        FormFields.Add(new RequestParameterItemViewModel
        {
            ParameterType = RequestParameterKind.FormField,
            Name = string.Empty,
            Value = string.Empty
        });
    }

    [RelayCommand]
    private void RemoveParameter(RequestParameterItemViewModel? item)
    {
        if (item is null) return;
        switch (item.ParameterType)
        {
            case RequestParameterKind.Query:
                QueryParameters.Remove(item);
                break;
            case RequestParameterKind.Path:
                PathParameters.Remove(item);
                break;
            case RequestParameterKind.Header:
                Headers.Remove(item);
                break;
            case RequestParameterKind.FormField:
                FormFields.Remove(item);
                break;
        }
    }

    /// <summary>切换 form-data 字段的 text/file 类型；仅 FormData 模式允许，FormUrlEncoded 不承载文件。</summary>
    [RelayCommand]
    private void ToggleFormFieldType(RequestParameterItemViewModel? item)
    {
        if (item is null || !IsFormDataOnly) return;
        item.FieldType = item.FieldType == FormFieldTypes.File
            ? FormFieldTypes.Text
            : FormFieldTypes.File;
    }

    /// <summary>为文件字段追加一个或多个文件 chip；文件内容不驻留内存，发送时按路径读取。</summary>
    [RelayCommand]
    private async Task AddFormFieldFilesAsync(RequestParameterItemViewModel? item)
    {
        if (item is null || _filePickerService is null) return;

        var files = await _filePickerService.PickOpenFilesAsync("选择上传文件", allowMultiple: true);
        if (files is null) return;

        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file.Path)) continue;
            item.Files.Add(new FormFieldFileViewModel
            {
                Path = file.Path,
                Name = string.IsNullOrWhiteSpace(file.Name) ? System.IO.Path.GetFileName(file.Path) : file.Name,
                SizeBytes = file.SizeBytes
            });
        }
    }

    /// <summary>移除文件 chip；chip 只被一个字段持有，按引用反查所属字段即可。</summary>
    [RelayCommand]
    private void RemoveFormFieldFile(FormFieldFileViewModel? file)
    {
        if (file is null) return;
        FormFields.FirstOrDefault(field => field.Files.Contains(file))?.Files.Remove(file);
    }

    /// <summary>Binary 模式选择单个文件：路径写入 RequestBody 落快照，文件名/大小随之刷新。</summary>
    [RelayCommand]
    private async Task PickBinaryFileAsync()
    {
        if (_filePickerService is null) return;

        var files = await _filePickerService.PickOpenFilesAsync("选择二进制文件", allowMultiple: false);
        var file = files?.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.Path));
        if (file is null) return;

        RequestBody = file.Path;
    }

    [RelayCommand]
    private void ClearBinaryFile()
    {
        RequestBody = string.Empty;
    }


    // --- Populate / Apply / Build / Reset ---

    public void PopulateFromEndpoint(ApiEndpointDto endpoint)
    {
        RequestName = endpoint.Name;
        RequestDescription = endpoint.Description;
        RequestBody = endpoint.RequestBodyTemplate;
        _bodyExampleTemplate = endpoint.RequestBodyTemplate;
        SelectedBodyMode = ResolveEndpointBodyMode(endpoint);
        ReplaceParameters(endpoint.Parameters);
        ReplaceFormFields(SelectedBodyMode, [], endpoint.RequestBodyTemplate);
        OnPropertyChanged(nameof(CanGenerateBodyExample));
        GenerateBodyExampleCommand.NotifyCanExecuteChanged();
    }

    public void ApplySnapshot(RequestSnapshotDto snapshot)
    {
        RequestName = snapshot.Name;
        RequestDescription = snapshot.Description;
        RequestBody = snapshot.BodyContent;
        _bodyExampleTemplate = string.Empty;
        SelectedBodyMode = snapshot.BodyMode;
        IgnoreSslErrors = snapshot.IgnoreSslErrors;
        ReplaceParameters(snapshot);
        ReplaceFormFields(SelectedBodyMode, snapshot.FormFields, snapshot.BodyContent);
        SelectDefaultTabByContent();
    }

    /// <summary>
    /// 按内容选择默认配置页签：请求体有内容（含表单字段）时打开 Body，
    /// 其次有 Query 参数时停在 Params，仅配置了 Header 时打开 Headers，空请求维持 Params。
    /// </summary>
    public void SelectDefaultTabByContent()
    {
        var hasBodyContent = SelectedBodyMode != BodyModes.None
            && (!string.IsNullOrWhiteSpace(RequestBody) || FormFields.Count > 0);
        SelectedTabIndex = hasBodyContent
            ? 1
            : Headers.Count > 0 && QueryParameters.Count == 0
                ? 2
                : 0;
    }

    public RequestSnapshotDto BuildRequestSnapshot(string endpointId, string method, string url, string? requestNameOverride = null)
    {
        // For form modes, serialize form fields into body content
        var bodyMode = SelectedBodyMode;
        var bodyContent = RequestBody;
        var formFields = Array.Empty<FormFieldDto>();

        if (bodyMode is BodyModes.FormData or BodyModes.FormUrlEncoded)
        {
            formFields = FormFields.Select(ToFormFieldDto).ToArray();
            // 快照 FormFields 携带完整编辑态（含禁用字段与文件引用）；BodyContent 只按发送口径编码，
            // FormData 含文件时落 JSON，FormUrlEncoded 恒为 URL 编码串以兼容旧解析器
            var sendableFields = formFields
                .Where(f => f.IsEnabled && !string.IsNullOrWhiteSpace(f.Name))
                .ToList();
            bodyContent = bodyMode == BodyModes.FormData
                ? FormFieldContentCodec.Encode(sendableFields)
                : FormFieldContentCodec.EncodeTextPairs(
                    sendableFields.Where(f => f.FieldType != FormFieldTypes.File).ToList());
        }

        return new RequestSnapshotDto
        {
            EndpointId = endpointId,
            Name = string.IsNullOrWhiteSpace(requestNameOverride) ? RequestName : requestNameOverride.Trim(),
            Method = method,
            Url = url,
            Description = RequestDescription,
            BodyMode = bodyMode,
            BodyContent = bodyContent,
            IgnoreSslErrors = IgnoreSslErrors,
            QueryParameters = QueryParameters.Select(ToKeyValue).ToList(),
            PathParameters = PathParameters.Select(ToKeyValue).ToList(),
            Headers = Headers.Select(ToKeyValue).ToList(),
            FormFields = formFields.ToList()
        };
    }

    public void Reset()
    {
        RequestName = string.Empty;
        RequestDescription = string.Empty;
        RequestBody = string.Empty;
        _bodyExampleTemplate = string.Empty;
        SelectedBodyMode = BodyModes.None;
        IgnoreSslErrors = false;
        QueryParameters.ReplaceWith([]);
        PathParameters.ReplaceWith([]);
        Headers.ReplaceWith([]);
        FormFields.ReplaceWith([]);
        SelectedTabIndex = 0;
        OnPropertyChanged(nameof(CanGenerateBodyExample));
        GenerateBodyExampleCommand.NotifyCanExecuteChanged();
    }

    protected override void DisposeManaged()
    {
        QueryParameters.CollectionChanged -= OnQueryParametersCollectionChanged;
        Headers.CollectionChanged -= OnHeadersCollectionChanged;
        FormFields.CollectionChanged -= OnFormFieldsCollectionChanged;

        foreach (var item in _subscribedQueryParameterItems)
        {
            item.PropertyChanged -= OnQueryParameterItemPropertyChanged;
        }

        _subscribedQueryParameterItems.Clear();
    }

    private void ReplaceParameters(IEnumerable<RequestParameterDto> parameters)
    {
        var queryParameters = new List<RequestParameterItemViewModel>();
        var pathParameters = new List<RequestParameterItemViewModel>();
        var headerParameters = new List<RequestParameterItemViewModel>();

        foreach (var parameter in parameters)
        {
            var item = new RequestParameterItemViewModel
            {
                ParameterType = parameter.ParameterType,
                Name = parameter.Name,
                Value = parameter.DefaultValue,
                Description = parameter.Description,
                IsRequired = parameter.Required
            };

            switch (parameter.ParameterType)
            {
                case RequestParameterKind.Query:
                    queryParameters.Add(item);
                    break;
                case RequestParameterKind.Path:
                    pathParameters.Add(item);
                    break;
                case RequestParameterKind.Header:
                    headerParameters.Add(item);
                    break;
            }
        }

        QueryParameters.ReplaceWith(queryParameters);
        PathParameters.ReplaceWith(pathParameters);
        Headers.ReplaceWith(headerParameters);
        FormFields.ReplaceWith([]);
    }

    private void ReplaceParameters(RequestSnapshotDto snapshot)
    {
        QueryParameters.ReplaceWith(snapshot.QueryParameters.Select(item => ToParameterItem(item, RequestParameterKind.Query)));
        PathParameters.ReplaceWith(snapshot.PathParameters.Select(item => ToParameterItem(item, RequestParameterKind.Path)));
        Headers.ReplaceWith(snapshot.Headers.Select(item => ToParameterItem(item, RequestParameterKind.Header)));
        FormFields.ReplaceWith([]);
    }

    private static RequestKeyValueDto ToKeyValue(RequestParameterItemViewModel item) =>
        new() { Name = item.Name, Value = item.Value, IsEnabled = item.IsEnabled };

    private static RequestParameterItemViewModel ToParameterItem(RequestKeyValueDto item, RequestParameterKind kind) =>
        new() { ParameterType = kind, Name = item.Name, Value = item.Value, IsEnabled = item.IsEnabled };

    private static string ResolveEndpointBodyMode(ApiEndpointDto endpoint)
    {
        if (!string.IsNullOrWhiteSpace(endpoint.RequestBodyMode) && endpoint.RequestBodyMode != BodyModes.None)
        {
            return endpoint.RequestBodyMode;
        }

        return string.IsNullOrWhiteSpace(endpoint.RequestBodyTemplate)
            ? BodyModes.None
            : BodyModes.RawJson;
    }

    /// <summary>
    /// 装载 form 字段：快照 FormFields 优先（完整编辑态，含禁用字段与文件引用），
    /// 旧快照/导入模板无 FormFields 时按 BodyContent 双格式兼容解析。
    /// </summary>
    private void ReplaceFormFields(string bodyMode, IReadOnlyList<FormFieldDto> snapshotFields, string bodyContent)
    {
        if (bodyMode is not (BodyModes.FormData or BodyModes.FormUrlEncoded))
        {
            FormFields.ReplaceWith([]);
            return;
        }

        var source = snapshotFields.Count > 0
            ? snapshotFields
            : FormFieldContentCodec.Parse(bodyContent);

        FormFields.ReplaceWith(source.Select(ToFormFieldItem));
    }

    private static RequestParameterItemViewModel ToFormFieldItem(FormFieldDto field)
    {
        var item = new RequestParameterItemViewModel
        {
            ParameterType = RequestParameterKind.FormField,
            Name = field.Name,
            Value = field.Value,
            IsEnabled = field.IsEnabled,
            FieldType = field.FieldType
        };

        foreach (var file in field.Files)
        {
            item.Files.Add(new FormFieldFileViewModel
            {
                Path = file.Path,
                Name = file.Name,
                SizeBytes = file.SizeBytes
            });
        }

        return item;
    }

    private static FormFieldDto ToFormFieldDto(RequestParameterItemViewModel item) => new()
    {
        Name = item.Name,
        Value = item.Value,
        FieldType = item.FieldType,
        IsEnabled = item.IsEnabled,
        Files = item.Files.Select(file => new FormFieldFileDto
        {
            Path = file.Path,
            Name = file.Name,
            SizeBytes = file.SizeBytes
        }).ToList()
    };

    private void OnQueryParametersCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        SyncQueryParameterSubscriptions();
        UpdateQueryParametersSelectionState();
        OnPropertyChanged(nameof(HasQueryParameters));
        OnPropertyChanged(nameof(QueryParameterCountText));
        OnPropertyChanged(nameof(ShowQueryParametersEmptyState));
        OnPropertyChanged(nameof(ConfigPanelMaxHeight));
    }

    private void OnHeadersCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasHeaders));
        OnPropertyChanged(nameof(HeaderCountText));
        OnPropertyChanged(nameof(ShowHeadersEmptyState));
        OnPropertyChanged(nameof(ConfigPanelMaxHeight));
    }

    private void OnQueryParameterItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RequestParameterItemViewModel.IsEnabled) or null or "")
        {
            UpdateQueryParametersSelectionState();
        }
    }

    private void SyncQueryParameterSubscriptions()
    {
        foreach (var item in _subscribedQueryParameterItems)
        {
            item.PropertyChanged -= OnQueryParameterItemPropertyChanged;
        }

        _subscribedQueryParameterItems.Clear();

        foreach (var item in QueryParameters)
        {
            item.PropertyChanged += OnQueryParameterItemPropertyChanged;
            _subscribedQueryParameterItems.Add(item);
        }
    }

    private void ApplyQueryParametersSelectionState(bool isEnabled)
    {
        if (QueryParameters.Count == 0)
        {
            return;
        }

        _isUpdatingQueryParametersSelectionState = true;
        try
        {
            foreach (var item in QueryParameters)
            {
                item.IsEnabled = isEnabled;
            }
        }
        finally
        {
            _isUpdatingQueryParametersSelectionState = false;
        }

        UpdateQueryParametersSelectionState();
    }

    private void UpdateQueryParametersSelectionState()
    {
        bool? nextState;
        if (QueryParameters.Count == 0)
        {
            nextState = false;
        }
        else
        {
            var enabledCount = QueryParameters.Count(item => item.IsEnabled);
            nextState = enabledCount switch
            {
                0 => false,
                var count when count == QueryParameters.Count => true,
                _ => null
            };
        }

        _isUpdatingQueryParametersSelectionState = true;
        try
        {
            if (_queryParametersSelectionState == nextState)
            {
                return;
            }

            _queryParametersSelectionState = nextState;
            OnPropertyChanged(nameof(QueryParametersSelectionState));
        }
        finally
        {
            _isUpdatingQueryParametersSelectionState = false;
        }
    }
}
