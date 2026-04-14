namespace ApixPress.App.Models.DTOs;

public sealed class RequestSnapshotDto
{
    public string EndpointId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string BodyMode { get; init; } = BodyModes.None;
    public string BodyContent { get; init; } = string.Empty;
    public bool IgnoreSslErrors { get; init; }
    public List<RequestKeyValueDto> QueryParameters { get; init; } = [];
    public List<RequestKeyValueDto> PathParameters { get; init; } = [];
    public List<RequestKeyValueDto> Headers { get; init; } = [];
}
