using CommunityToolkit.Mvvm.ComponentModel;

namespace ApixPress.App.ViewModels;

public partial class RequestCaseDeliveryItemViewModel : ObservableObject
{
    public string Id { get; init; } = string.Empty;
    public string ParentId { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string FolderPath { get; init; } = string.Empty;
    public string KindText => string.Equals(Kind, "interface", StringComparison.OrdinalIgnoreCase) ? "接口" : "请求用例";
    public string SummaryText => string.IsNullOrWhiteSpace(Method) ? FolderPath : $"{Method} {Url}";

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool isAutoIncluded;
}
