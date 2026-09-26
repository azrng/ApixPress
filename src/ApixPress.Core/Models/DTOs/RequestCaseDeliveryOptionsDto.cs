namespace ApixPress.App.Models.DTOs;

public sealed class RequestCaseDeliveryOptionsDto
{
    public List<RequestCaseDeliveryCandidateDto> Interfaces { get; init; } = [];
    public List<RequestCaseDeliveryCandidateDto> RequestCases { get; init; } = [];
    public int EnvironmentCount { get; init; }
    public int VariableCount { get; init; }
}
