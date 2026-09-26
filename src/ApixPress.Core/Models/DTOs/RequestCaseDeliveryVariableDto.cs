namespace ApixPress.App.Models.DTOs;

public sealed class RequestCaseDeliveryVariableDto
{
    public string SourceId { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
}
