namespace ApixPress.App.Models.DTOs;

public static class RequestCaseDeliveryDecisionActions
{
    public const string Import = "import";
    public const string Update = "update";
    public const string Skip = "skip";
    public const string Duplicate = "duplicate";
}

public sealed class RequestCaseDeliveryDecisionDto
{
    public string Kind { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
    public string Action { get; init; } = RequestCaseDeliveryDecisionActions.Import;
}
