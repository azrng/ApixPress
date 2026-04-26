using System.Collections.ObjectModel;
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

        FormFields.CollectionChanged += (_, _) => OnFormFieldsChanged();
        SelectedBodyModeOption = BodyModeOptions[0];
    }

    // --- Visibility helpers for the Body tab UI ---

    public bool IsBodyModeFormData =>
        SelectedBodyMode == BodyModes.FormData || SelectedBodyMode == BodyModes.FormUrlEncoded;

    public bool HasFormFields => FormFields.Count > 0;

    public bool ShowFormDataEmptyState => IsBodyModeFormData && !HasFormFields;

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
        OnPropertyChanged(nameof(ShowFormDataEmptyState));
        OnPropertyChanged(nameof(HasRawBodyEditor));
        OnPropertyChanged(nameof(HasBodyContent));
        OnPropertyChanged(nameof(RequestBodyWatermark));

        // Sync the option selection if changed programmatically
        var match = BodyModeOptions.FirstOrDefault(o => o.Mode == value);
        if (match is not null && SelectedBodyModeOption != match)
            SelectedBodyModeOption = match;
    }

    private void OnFormFieldsChanged()
    {
        OnPropertyChanged(nameof(HasFormFields));
        OnPropertyChanged(nameof(ShowFormDataEmptyState));
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
        SelectedBodyMode = ResolveEndpointBodyMode(endpoint);
        ReplaceParameters(endpoint.Parameters);
        ReplaceFormFieldsFromBodyContent(SelectedBodyMode, endpoint.RequestBodyTemplate);
    }

    public void ApplySnapshot(RequestSnapshotDto snapshot)
    {
        RequestName = snapshot.Name;
        RequestDescription = snapshot.Description;
        RequestBody = snapshot.BodyContent;
        SelectedBodyMode = snapshot.BodyMode;
        IgnoreSslErrors = snapshot.IgnoreSslErrors;
        ReplaceParameters(snapshot);
        ReplaceFormFieldsFromBodyContent(SelectedBodyMode, snapshot.BodyContent);
    }

    public RequestSnapshotDto BuildRequestSnapshot(string endpointId, string method, string url)
    {
        // For form modes, serialize form fields into body content
        var bodyMode = SelectedBodyMode;
        var bodyContent = RequestBody;

        if (bodyMode == BodyModes.FormData || bodyMode == BodyModes.FormUrlEncoded)
        {
            var parts = FormFields
                .Where(f => f.IsEnabled && !string.IsNullOrWhiteSpace(f.Name))
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
        QueryParameters.ReplaceWith([]);
        PathParameters.ReplaceWith([]);
        Headers.ReplaceWith([]);
        FormFields.ReplaceWith([]);
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

    private void ReplaceFormFieldsFromBodyContent(string bodyMode, string bodyContent)
    {
        if (bodyMode is not (BodyModes.FormData or BodyModes.FormUrlEncoded))
        {
            FormFields.ReplaceWith([]);
            return;
        }

        FormFields.ReplaceWith(ParseFormFields(bodyContent));
    }

    private static IEnumerable<RequestParameterItemViewModel> ParseFormFields(string bodyContent)
    {
        foreach (var pair in bodyContent.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var name = Uri.UnescapeDataString(parts[0]);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            yield return new RequestParameterItemViewModel
            {
                ParameterType = RequestParameterKind.Query,
                Name = name,
                Value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty,
                IsEnabled = true
            };
        }
    }
}
