namespace ApixPress.App.Models.DTOs;

public sealed class ApiImportConflictDto
{
    public string ExistingDocumentId { get; init; } = string.Empty;
    public string ExistingDocumentName { get; init; } = string.Empty;
    public string ExistingEndpointId { get; init; } = string.Empty;
    public string ExistingEndpointName { get; init; } = string.Empty;
    public string ImportedEndpointName { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
}
