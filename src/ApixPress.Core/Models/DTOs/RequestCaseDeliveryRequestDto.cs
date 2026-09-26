namespace ApixPress.App.Models.DTOs;

public sealed class RequestCaseDeliveryRequestDto
{
    public string ProjectId { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public IReadOnlySet<string> SelectedInterfaceIds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlySet<string> SelectedRequestCaseIds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public string OutputFilePath { get; init; } = string.Empty;
}
