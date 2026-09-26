namespace ApixPress.App.Models.DTOs;

public sealed class RequestCaseDeliverySourceDto
{
    public string ProjectId { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string Product { get; init; } = "ApixPress";
}
