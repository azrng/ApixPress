namespace ApixPress.App.Models.DTOs;

public sealed class RequestCaseDeliveryPreviewItemDto
{
    public string Kind { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string DefaultAction { get; init; } = RequestCaseDeliveryDecisionActions.Import;
    public string Detail { get; init; } = string.Empty;
    public string ExistingId { get; init; } = string.Empty;
}
