namespace ApixPress.App.Models.Entities;

public sealed class RequestParameterEntity
{
    public string Id { get; set; } = string.Empty;
    public string EndpointId { get; set; } = string.Empty;
    public string ParameterType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DefaultValue { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; }
}
