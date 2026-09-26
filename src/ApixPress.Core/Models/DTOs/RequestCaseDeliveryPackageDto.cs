namespace ApixPress.App.Models.DTOs;

public sealed class RequestCaseDeliveryPackageDto
{
    public const string Format = "apixpress.request-case-delivery";
    public const string CurrentSchemaVersion = "apixpress.request-case-delivery.v1";

    public string FormatName { get; init; } = Format;
    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public DateTime ExportedAt { get; init; }
    public RequestCaseDeliverySourceDto Source { get; init; } = new();
    public List<RequestCaseDeliveryInterfaceDto> Interfaces { get; init; } = [];
    public List<RequestCaseDeliveryCaseDto> RequestCases { get; init; } = [];
    public List<RequestCaseDeliveryEnvironmentDto> Environments { get; init; } = [];
}
