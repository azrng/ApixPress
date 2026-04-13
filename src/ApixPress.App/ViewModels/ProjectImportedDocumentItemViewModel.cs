namespace ApixPress.App.ViewModels;

public sealed class ProjectImportedDocumentItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string SourceTypeText { get; init; } = string.Empty;
    public string SourceValueText { get; init; } = string.Empty;
    public string BaseUrlText { get; init; } = string.Empty;
    public string ImportedAtText { get; init; } = string.Empty;
    public int EndpointCount { get; init; }
    public string EndpointCountText => EndpointCount.ToString();
}
