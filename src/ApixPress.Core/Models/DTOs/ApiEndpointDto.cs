namespace ApixPress.App.Models.DTOs;

public sealed class ApiEndpointDto
{
    public string Id { get; init; } = string.Empty;
    public string DocumentId { get; init; } = string.Empty;
    public string GroupName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string RequestBodyMode { get; init; } = BodyModes.None;
    public string RequestBodyTemplate { get; init; } = string.Empty;
    public List<RequestParameterDto> Parameters { get; init; } = [];
}
