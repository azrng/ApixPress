namespace ApixPress.App.Models.DTOs;

public enum RequestParameterKind
{
    Query,
    Path,
    Header
}

public static class BodyModes
{
    public const string None = "None";
    public const string FormData = "FormData";
    public const string FormUrlEncoded = "FormUrlEncoded";
    public const string RawJson = "RawJson";
    public const string RawXml = "RawXml";
    public const string RawText = "RawText";
}

public sealed class ApiDocumentDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public string SourceValue { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public DateTime ImportedAt { get; init; }
}

public sealed class RequestParameterDto
{
    public string Id { get; init; } = string.Empty;
    public string EndpointId { get; init; } = string.Empty;
    public RequestParameterKind ParameterType { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DefaultValue { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool Required { get; init; }
}

public sealed class ApiEndpointDto
{
    public string Id { get; init; } = string.Empty;
    public string DocumentId { get; init; } = string.Empty;
    public string GroupName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string RequestBodyTemplate { get; init; } = string.Empty;
    public List<RequestParameterDto> Parameters { get; init; } = [];
}

public sealed class RequestKeyValueDto
{
    public string Name { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

public sealed class RequestSnapshotDto
{
    public string EndpointId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string BodyMode { get; init; } = BodyModes.None;
    public string BodyContent { get; init; } = string.Empty;
    public bool IgnoreSslErrors { get; init; }
    public List<RequestKeyValueDto> QueryParameters { get; init; } = [];
    public List<RequestKeyValueDto> PathParameters { get; init; } = [];
    public List<RequestKeyValueDto> Headers { get; init; } = [];
}

public sealed class ResponseHeaderDto
{
    public string Name { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

public sealed class ResponseSnapshotDto
{
    public int? StatusCode { get; init; }
    public long DurationMs { get; init; }
    public long SizeBytes { get; init; }
    public string Content { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public string RequestSummary { get; init; } = string.Empty;
    public List<ResponseHeaderDto> Headers { get; init; } = [];
}

public sealed class RequestCaseDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string GroupName { get; init; } = "默认分组";
    public List<string> Tags { get; init; } = [];
    public string Description { get; init; } = string.Empty;
    public RequestSnapshotDto RequestSnapshot { get; init; } = new();
    public DateTime UpdatedAt { get; init; }
}

public sealed class EnvironmentVariableDto
{
    public string Id { get; init; } = string.Empty;
    public string EnvironmentName { get; init; } = "Default";
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
}
