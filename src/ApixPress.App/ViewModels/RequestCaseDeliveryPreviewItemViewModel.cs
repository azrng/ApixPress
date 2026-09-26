using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Models.DTOs;

namespace ApixPress.App.ViewModels;

public partial class RequestCaseDeliveryPreviewItemViewModel : ObservableObject
{
    public RequestCaseDeliveryPreviewItemViewModel(RequestCaseDeliveryPreviewItemDto source)
    {
        Kind = source.Kind;
        SourceId = source.SourceId;
        Name = source.Name;
        Status = source.Status;
        ExistingId = source.ExistingId;
        Detail = source.Detail;
        SelectedAction = ActionOptions.FirstOrDefault(item =>
            string.Equals(item.Key, source.DefaultAction, StringComparison.OrdinalIgnoreCase))
            ?? ActionOptions[0];
    }

    public static IReadOnlyList<RequestCaseDeliveryActionOption> ActionOptions { get; } =
    [
        new(RequestCaseDeliveryDecisionActions.Import, "导入"),
        new(RequestCaseDeliveryDecisionActions.Update, "覆盖更新"),
        new(RequestCaseDeliveryDecisionActions.Skip, "跳过"),
        new(RequestCaseDeliveryDecisionActions.Duplicate, "另存")
    ];

    public string Kind { get; }
    public string SourceId { get; }
    public string Name { get; }
    public string Status { get; }
    public string ExistingId { get; }
    public string Detail { get; }
    public string KindText => Kind switch
    {
        "interface" => "接口",
        "request-case" => "请求用例",
        "environment" => "环境",
        _ => Kind
    };

    [ObservableProperty]
    private RequestCaseDeliveryActionOption selectedAction = ActionOptions[0];
}
