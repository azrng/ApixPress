namespace ApixPress.App.Models.Entities;

public sealed class ApiEndpointEntity
{
    public string Id { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RequestBodyTemplate { get; set; } = string.Empty;
}
