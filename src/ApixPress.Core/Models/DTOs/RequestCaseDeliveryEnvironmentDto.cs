namespace ApixPress.App.Models.DTOs;

public sealed class RequestCaseDeliveryEnvironmentDto
{
    public string SourceId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
    public List<RequestCaseDeliveryVariableDto> Variables { get; init; } = [];
}
