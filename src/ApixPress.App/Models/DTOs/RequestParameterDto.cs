namespace ApixPress.App.Models.DTOs;

public sealed class RequestParameterDto
{
    public string Id { get; init; } = string.Empty;
    public string EndpointId { get; init; } = string.Empty;
    public RequestParameterKind ParameterType { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DefaultValue { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool Required { get; init; }
}
