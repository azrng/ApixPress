namespace ApixPress.App.Models.Entities;

public sealed class ApiDocumentEntity
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceValue { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
}

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

public sealed class RequestCaseEntity
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string EntryType { get; set; } = "quick-request";
    public string Name { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string ParentId { get; set; } = string.Empty;
    public string TagsJson { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RequestSnapshotJson { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public sealed class EnvironmentVariableEntity
{
    public string Id { get; set; } = string.Empty;
    public string EnvironmentId { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = "Default";
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}
