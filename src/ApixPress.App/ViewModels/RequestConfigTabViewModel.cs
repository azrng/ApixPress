using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class RequestConfigTabViewModel : ViewModelBase
{
    private readonly IFilePickerService? _filePickerService;

    public ObservableCollection<RequestParameterItemViewModel> QueryParameters { get; } = [];

    public ObservableCollection<RequestParameterItemViewModel> PathParameters { get; } = [];

    public ObservableCollection<RequestParameterItemViewModel> Headers { get; } = [];

    public ObservableCollection<RequestParameterItemViewModel> FormFields { get; } = [];

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
        };

        foreach (var (mode, displayName) in modes)
        {
            BodyModeOptions.Add(new BodyModeOptionViewModel { Mode = mode, DisplayName = displayName });
        }

        SelectedBodyModeOption = BodyModeOptions[0];
    }

    // --- Visibility helpers for the Body tab UI ---

    public bool IsBodyModeFormData =>
        SelectedBodyMode == BodyModes.FormData || SelectedBodyMode == BodyModes.FormUrlEncoded;

    public bool HasRawBodyEditor =>
        SelectedBodyMode is BodyModes.RawJson or BodyModes.RawXml or BodyModes.RawText;

    public bool HasBodyContent => SelectedBodyMode != BodyModes.None;

    public string RequestBodyWatermark => SelectedBodyMode switch
    {
        BodyModes.RawJson => "输入 JSON 内容",
        BodyModes.RawXml => "输入 XML 内容",
        BodyModes.RawText => "输入纯文本内容",
        _ => "请求体内容"
    };

    partial void OnSelectedBodyModeOptionChanged(BodyModeOptionViewModel? value)
    {
        if (value is not null)
            SelectedBodyMode = value.Mode;
    }

    partial void OnSelectedBodyModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsBodyModeFormData));
        OnPropertyChanged(nameof(HasRawBodyEditor));
        OnPropertyChanged(nameof(HasBodyContent));
        OnPropertyChanged(nameof(RequestBodyWatermark));

        // Sync the option selection if changed programmatically
        var match = BodyModeOptions.FirstOrDefault(o => o.Mode == value);
        if (match is not null && SelectedBodyModeOption != match)
            SelectedBodyModeOption = match;
    }

    // --- Commands ---

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
            ParameterType = RequestParameterKind.Query,
            Name = string.Empty,
            Value = string.Empty
        });
    }

    [RelayCommand]
    private void RemoveParameter(RequestParameterItemViewModel? item)
    {
        if (item is null) return;
        QueryParameters.Remove(item);
        Headers.Remove(item);
        PathParameters.Remove(item);
        FormFields.Remove(item);
    }


    // --- Populate / Apply / Build / Reset ---

    public void PopulateFromEndpoint(ApiEndpointDto endpoint)
    {
        RequestName = endpoint.Name;
        RequestDescription = endpoint.Description;
        RequestBody = endpoint.RequestBodyTemplate;
        SelectedBodyMode = string.IsNullOrWhiteSpace(endpoint.RequestBodyTemplate)
            ? BodyModes.None
            : BodyModes.RawJson;

        QueryParameters.Clear();
        PathParameters.Clear();
        Headers.Clear();
        FormFields.Clear();

        foreach (var parameter in endpoint.Parameters)
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
                    QueryParameters.Add(item);
                    break;
                case RequestParameterKind.Path:
                    PathParameters.Add(item);
                    break;
                case RequestParameterKind.Header:
                    Headers.Add(item);
                    break;
            }
        }
    }

    public void ApplySnapshot(RequestSnapshotDto snapshot)
    {
        RequestName = snapshot.Name;
        RequestDescription = snapshot.Description;
        RequestBody = snapshot.BodyContent;
        SelectedBodyMode = snapshot.BodyMode;
        IgnoreSslErrors = snapshot.IgnoreSslErrors;

        QueryParameters.Clear();
        PathParameters.Clear();
        Headers.Clear();
        FormFields.Clear();

        foreach (var item in snapshot.QueryParameters)
            QueryParameters.Add(ToParameterItem(item, RequestParameterKind.Query));

        foreach (var item in snapshot.PathParameters)
            PathParameters.Add(ToParameterItem(item, RequestParameterKind.Path));

        foreach (var item in snapshot.Headers)
            Headers.Add(ToParameterItem(item, RequestParameterKind.Header));
    }

    public RequestSnapshotDto BuildRequestSnapshot(string endpointId, string method, string url)
    {
        // For form modes, serialize form fields into body content
        var bodyMode = SelectedBodyMode;
        var bodyContent = RequestBody;

        if (bodyMode == BodyModes.FormData || bodyMode == BodyModes.FormUrlEncoded)
        {
            var parts = FormFields
                .Where(f => !string.IsNullOrWhiteSpace(f.Name))
                .Select(f => $"{Uri.EscapeDataString(f.Name)}={Uri.EscapeDataString(f.Value)}");
            bodyContent = string.Join("&", parts);
        }

        return new RequestSnapshotDto
        {
            EndpointId = endpointId,
            Name = RequestName,
            Method = method,
            Url = url,
            Description = RequestDescription,
            BodyMode = bodyMode,
            BodyContent = bodyContent,
            IgnoreSslErrors = IgnoreSslErrors,
            QueryParameters = QueryParameters.Select(ToKeyValue).ToList(),
            PathParameters = PathParameters.Select(ToKeyValue).ToList(),
            Headers = Headers.Select(ToKeyValue).ToList()
        };
    }

    public void Reset()
    {
        RequestName = string.Empty;
        RequestDescription = string.Empty;
        RequestBody = string.Empty;
        SelectedBodyMode = BodyModes.None;
        IgnoreSslErrors = false;
        QueryParameters.Clear();
        PathParameters.Clear();
        Headers.Clear();
        FormFields.Clear();
    }

    private static RequestKeyValueDto ToKeyValue(RequestParameterItemViewModel item) =>
        new() { Name = item.Name, Value = item.Value };

    private static RequestParameterItemViewModel ToParameterItem(RequestKeyValueDto item, RequestParameterKind kind) =>
        new() { ParameterType = kind, Name = item.Name, Value = item.Value };
}
