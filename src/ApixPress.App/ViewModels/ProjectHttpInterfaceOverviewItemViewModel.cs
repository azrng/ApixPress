using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels.Base;
using CommunityToolkit.Mvvm.Input;

namespace ApixPress.App.ViewModels;

public sealed partial class ProjectHttpInterfaceOverviewItemViewModel : ViewModelBase
{
    private readonly Func<RequestCaseDto, Task> _openAsync;

    public ProjectHttpInterfaceOverviewItemViewModel(
        RequestCaseDto source,
        Func<RequestCaseDto, Task> openAsync)
    {
        Source = source;
        _openAsync = openAsync;
    }

    public RequestCaseDto Source { get; }
    public string Id => Source.Id;
    public string Name => string.IsNullOrWhiteSpace(Source.Name) ? "未命名接口" : Source.Name;
    public string Method => string.IsNullOrWhiteSpace(Source.RequestSnapshot.Method) ? "GET" : Source.RequestSnapshot.Method.ToUpperInvariant();
    public string Path => string.IsNullOrWhiteSpace(Source.RequestSnapshot.Url) ? "/" : Source.RequestSnapshot.Url;
    public string GroupPath => string.IsNullOrWhiteSpace(Source.FolderPath) ? "根目录" : $"根目录/{Source.FolderPath}";
    public string StatusText => "已发布";
    public string TagText => string.IsNullOrWhiteSpace(Source.FolderPath) ? "根目录" : Source.FolderPath.Split('/')[^1];
    public bool IsGetMethod => Method == "GET";
    public bool IsPostMethod => Method == "POST";
    public bool IsPutMethod => Method == "PUT";
    public bool IsDeleteMethod => Method == "DELETE";
    public bool IsPatchMethod => Method == "PATCH";

    [RelayCommand]
    private Task OpenAsync()
    {
        return _openAsync(Source);
    }
}
