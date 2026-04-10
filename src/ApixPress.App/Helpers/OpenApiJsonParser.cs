using System.Text.Json;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;
using Azrng.Core.Exceptions;

namespace ApixPress.App.Helpers;

public sealed class ParsedDocumentGraph
{
    public required ApiDocumentEntity Document { get; init; }
    public required IReadOnlyList<ApiEndpointEntity> Endpoints { get; init; }
    public required IReadOnlyList<RequestParameterEntity> Parameters { get; init; }
}

public static class OpenApiJsonParser
{
    private static readonly HashSet<string> SupportedMethods =
    [
        "get", "post", "put", "delete", "patch", "options", "head"
    ];

    public static ParsedDocumentGraph Parse(string json, string sourceType, string sourceValue)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("paths", out var pathsElement) || pathsElement.ValueKind != JsonValueKind.Object)
        {
            throw new ParameterException("导入失败，文档中未找到有效的 paths 节点。");
        }

        var documentId = Guid.NewGuid().ToString("N");
        var apiDocument = new ApiDocumentEntity
        {
            Id = documentId,
            Name = ResolveDocumentName(root, sourceValue),
            SourceType = sourceType,
            SourceValue = sourceValue,
            BaseUrl = ResolveBaseUrl(root),
            RawJson = json,
            ImportedAt = DateTime.UtcNow
        };

        var endpoints = new List<ApiEndpointEntity>();
        var parameters = new List<RequestParameterEntity>();

        foreach (var pathProperty in pathsElement.EnumerateObject())
        {
            var pathItem = pathProperty.Value;
            var sharedParameters = ReadParameters(pathItem, null);

            foreach (var operation in pathItem.EnumerateObject())
            {
                if (!SupportedMethods.Contains(operation.Name))
                {
                    continue;
                }

                var endpointId = Guid.NewGuid().ToString("N");
                var operationElement = operation.Value;
                var requestBodyTemplate = ResolveRequestBodyTemplate(operationElement);
                var endpoint = new ApiEndpointEntity
                {
                    Id = endpointId,
                    DocumentId = documentId,
                    GroupName = ResolveGroupName(operationElement, pathProperty.Name),
                    Name = ResolveOperationName(operationElement, pathProperty.Name, operation.Name),
                    Method = operation.Name.ToUpperInvariant(),
                    Path = pathProperty.Name,
                    Description = ResolveDescription(operationElement),
                    RequestBodyTemplate = requestBodyTemplate
                };

                endpoints.Add(endpoint);

                foreach (var parameter in sharedParameters.Concat(ReadParameters(operationElement, requestBodyTemplate)))
                {
                    parameters.Add(parameter with { EndpointId = endpointId });
                }
            }
        }

        return new ParsedDocumentGraph
        {
            Document = apiDocument,
            Endpoints = endpoints,
            Parameters = parameters
        };
    }

    private static string ResolveDocumentName(JsonElement root, string sourceValue)
    {
        if (root.TryGetProperty("info", out var info)
            && info.TryGetProperty("title", out var title)
            && title.ValueKind == JsonValueKind.String)
        {
            return title.GetString() ?? "未命名文档";
        }

        return Path.GetFileNameWithoutExtension(sourceValue) switch
        {
            { Length: > 0 } name => name,
            _ => "未命名文档"
        };
    }

    private static string ResolveBaseUrl(JsonElement root)
    {
        if (root.TryGetProperty("servers", out var servers)
            && servers.ValueKind == JsonValueKind.Array
            && servers.GetArrayLength() > 0)
        {
            var first = servers[0];
            if (first.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
            {
                return url.GetString() ?? string.Empty;
            }
        }

        var scheme = "https";
        if (root.TryGetProperty("schemes", out var schemes)
            && schemes.ValueKind == JsonValueKind.Array
            && schemes.GetArrayLength() > 0
            && schemes[0].ValueKind == JsonValueKind.String)
        {
            scheme = schemes[0].GetString() ?? scheme;
        }

        if (root.TryGetProperty("host", out var host) && host.ValueKind == JsonValueKind.String)
        {
            var basePath = root.TryGetProperty("basePath", out var basePathElement) && basePathElement.ValueKind == JsonValueKind.String
                ? basePathElement.GetString() ?? string.Empty
                : string.Empty;
            return $"{scheme}://{host.GetString()}{basePath}";
        }

        return string.Empty;
    }

    private static string ResolveGroupName(JsonElement operationElement, string path)
    {
        if (operationElement.TryGetProperty("tags", out var tags)
            && tags.ValueKind == JsonValueKind.Array
            && tags.GetArrayLength() > 0
            && tags[0].ValueKind == JsonValueKind.String)
        {
            return tags[0].GetString() ?? "默认分组";
        }

        return path.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "默认分组";
    }

    private static string ResolveOperationName(JsonElement operationElement, string path, string method)
    {
        if (operationElement.TryGetProperty("summary", out var summary) && summary.ValueKind == JsonValueKind.String)
        {
            return summary.GetString() ?? $"{method.ToUpperInvariant()} {path}";
        }

        if (operationElement.TryGetProperty("operationId", out var operationId) && operationId.ValueKind == JsonValueKind.String)
        {
            return operationId.GetString() ?? $"{method.ToUpperInvariant()} {path}";
        }

        return $"{method.ToUpperInvariant()} {path}";
    }

    private static string ResolveDescription(JsonElement operationElement)
    {
        if (operationElement.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String)
        {
            return description.GetString() ?? string.Empty;
        }

        if (operationElement.TryGetProperty("summary", out var summary) && summary.ValueKind == JsonValueKind.String)
        {
            return summary.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static string ResolveRequestBodyTemplate(JsonElement operationElement)
    {
        if (!operationElement.TryGetProperty("requestBody", out var requestBody)
            || requestBody.ValueKind != JsonValueKind.Object
            || !requestBody.TryGetProperty("content", out var content)
            || content.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        if (content.TryGetProperty("application/json", out var jsonContent))
        {
            if (jsonContent.TryGetProperty("example", out var example))
            {
                return JsonSerializer.Serialize(example, new JsonSerializerOptions { WriteIndented = true });
            }

            if (jsonContent.TryGetProperty("examples", out var examples)
                && examples.ValueKind == JsonValueKind.Object)
            {
                foreach (var exampleItem in examples.EnumerateObject())
                {
                    if (exampleItem.Value.TryGetProperty("value", out var exampleValue))
                    {
                        return JsonSerializer.Serialize(exampleValue, new JsonSerializerOptions { WriteIndented = true });
                    }
                }
            }
        }

        return string.Empty;
    }

    private static IEnumerable<ParameterSeed> ReadParameters(JsonElement element, string? requestBodyTemplate)
    {
        var result = new List<ParameterSeed>();
        if (element.TryGetProperty("parameters", out var parametersElement) && parametersElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var parameter in parametersElement.EnumerateArray())
            {
                var name = parameter.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                    ? nameElement.GetString() ?? string.Empty
                    : string.Empty;
                var @in = parameter.TryGetProperty("in", out var inElement) && inElement.ValueKind == JsonValueKind.String
                    ? inElement.GetString() ?? string.Empty
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(@in))
                {
                    continue;
                }

                var kind = @in.ToLowerInvariant() switch
                {
                    "query" => RequestParameterKind.Query,
                    "path" => RequestParameterKind.Path,
                    "header" => RequestParameterKind.Header,
                    _ => (RequestParameterKind?)null
                };

                if (kind is null)
                {
                    continue;
                }

                result.Add(new ParameterSeed
                {
                    EndpointId = string.Empty,
                    ParameterType = kind.Value,
                    Name = name,
                    DefaultValue = ResolveParameterDefaultValue(parameter),
                    Description = parameter.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String
                        ? description.GetString() ?? string.Empty
                        : string.Empty,
                    Required = parameter.TryGetProperty("required", out var required)
                               && required.ValueKind == JsonValueKind.True
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(requestBodyTemplate))
        {
            result.Add(new ParameterSeed
            {
                EndpointId = string.Empty,
                ParameterType = RequestParameterKind.Header,
                Name = "Content-Type",
                DefaultValue = "application/json",
                Description = "根据 OpenAPI requestBody 自动补充",
                Required = false
            });
        }

        return result;
    }

    private static string ResolveParameterDefaultValue(JsonElement parameter)
    {
        if (parameter.TryGetProperty("example", out var example))
        {
            return example.ValueKind switch
            {
                JsonValueKind.String => example.GetString() ?? string.Empty,
                JsonValueKind.Number => example.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => example.GetRawText()
            };
        }

        if (parameter.TryGetProperty("schema", out var schema)
            && schema.ValueKind == JsonValueKind.Object
            && schema.TryGetProperty("default", out var defaultValue))
        {
            return defaultValue.ValueKind switch
            {
                JsonValueKind.String => defaultValue.GetString() ?? string.Empty,
                JsonValueKind.Number => defaultValue.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => defaultValue.GetRawText()
            };
        }

        return string.Empty;
    }

    private sealed record ParameterSeed
    {
        public string EndpointId { get; init; } = string.Empty;
        public RequestParameterKind ParameterType { get; init; }
        public string Name { get; init; } = string.Empty;
        public string DefaultValue { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public bool Required { get; init; }

        public static implicit operator RequestParameterEntity(ParameterSeed seed)
        {
            return new RequestParameterEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                EndpointId = seed.EndpointId,
                ParameterType = seed.ParameterType.ToString(),
                Name = seed.Name,
                DefaultValue = seed.DefaultValue,
                Description = seed.Description,
                Required = seed.Required
            };
        }
    }
}
