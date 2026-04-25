using System.Text.Json;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;
using Azrng.Core.Exceptions;

namespace ApixPress.App.Helpers;

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
            BaseUrl = ResolveBaseUrl(root, sourceType, sourceValue),
            RawJson = json,
            ImportedAt = DateTime.UtcNow
        };

        var endpoints = new List<ApiEndpointEntity>();
        var parameters = new List<RequestParameterEntity>();

        foreach (var pathProperty in pathsElement.EnumerateObject())
        {
            var pathItem = pathProperty.Value;
            var sharedParameters = ReadParameters(pathItem, BodyModes.None);

            foreach (var operation in pathItem.EnumerateObject())
            {
                if (!SupportedMethods.Contains(operation.Name))
                {
                    continue;
                }

                var endpointId = Guid.NewGuid().ToString("N");
                var operationElement = operation.Value;
                var requestBody = ResolveRequestBody(operationElement, root);
                var endpoint = new ApiEndpointEntity
                {
                    Id = endpointId,
                    DocumentId = documentId,
                    GroupName = ResolveGroupName(operationElement, pathProperty.Name),
                    Name = ResolveOperationName(operationElement, pathProperty.Name, operation.Name),
                    Method = operation.Name.ToUpperInvariant(),
                    Path = pathProperty.Name,
                    Description = ResolveDescription(operationElement),
                    RequestBodyMode = requestBody.Mode,
                    RequestBodyTemplate = requestBody.Template
                };

                endpoints.Add(endpoint);

                foreach (var parameter in sharedParameters.Concat(ReadParameters(operationElement, requestBody.Mode)))
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

    private static string ResolveBaseUrl(JsonElement root, string sourceType, string sourceValue)
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

        return InferBaseUrlFromImportSource(sourceType, sourceValue);
    }

    public static string InferBaseUrlFromImportSource(string sourceType, string sourceValue)
    {
        if (!string.Equals(sourceType, "URL", StringComparison.OrdinalIgnoreCase)
            || !Uri.TryCreate(sourceValue, UriKind.Absolute, out var sourceUri))
        {
            return string.Empty;
        }

        var origin = sourceUri.GetLeftPart(UriPartial.Authority);
        var absolutePath = sourceUri.AbsolutePath;
        var markerIndex = absolutePath.IndexOf("/swagger/", StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            return CombineBaseUrl(origin, absolutePath[..markerIndex]);
        }

        var knownSuffixes = new[]
        {
            "/swagger.json",
            "/openapi.json",
            "/v3/api-docs",
            "/v2/api-docs",
            "/api-docs"
        };

        foreach (var suffix in knownSuffixes)
        {
            if (absolutePath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return CombineBaseUrl(origin, absolutePath[..^suffix.Length]);
            }
        }

        return origin;
    }

    private static string CombineBaseUrl(string origin, string prefixPath)
    {
        var normalizedPrefix = prefixPath.TrimEnd('/');
        return string.IsNullOrWhiteSpace(normalizedPrefix)
            ? origin
            : $"{origin}{normalizedPrefix}";
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

    private static RequestBodySeed ResolveRequestBody(JsonElement operationElement, JsonElement root)
    {
        if (!operationElement.TryGetProperty("requestBody", out var requestBody)
            || requestBody.ValueKind != JsonValueKind.Object
            || !requestBody.TryGetProperty("content", out var content)
            || content.ValueKind != JsonValueKind.Object)
        {
            return RequestBodySeed.Empty;
        }

        if (content.TryGetProperty("application/json", out var jsonContent))
        {
            if (jsonContent.TryGetProperty("example", out var example))
            {
                return new RequestBodySeed(BodyModes.RawJson, JsonSerializer.Serialize(example, new JsonSerializerOptions { WriteIndented = true }));
            }

            if (jsonContent.TryGetProperty("examples", out var examples)
                && examples.ValueKind == JsonValueKind.Object)
            {
                foreach (var exampleItem in examples.EnumerateObject())
                {
                    if (exampleItem.Value.TryGetProperty("value", out var exampleValue))
                    {
                        return new RequestBodySeed(BodyModes.RawJson, JsonSerializer.Serialize(exampleValue, new JsonSerializerOptions { WriteIndented = true }));
                    }
                }
            }

            if (jsonContent.TryGetProperty("schema", out var jsonSchema))
            {
                var exampleValue = BuildSchemaExampleValue(jsonSchema, root, 0);
                if (exampleValue is not null)
                {
                    return new RequestBodySeed(BodyModes.RawJson, JsonSerializer.Serialize(exampleValue, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
        }

        if (content.TryGetProperty("multipart/form-data", out var formDataContent))
        {
            return new RequestBodySeed(BodyModes.FormData, BuildFormBodyTemplate(formDataContent, root));
        }

        if (content.TryGetProperty("application/x-www-form-urlencoded", out var formUrlEncodedContent))
        {
            return new RequestBodySeed(BodyModes.FormUrlEncoded, BuildFormBodyTemplate(formUrlEncodedContent, root));
        }

        return RequestBodySeed.Empty;
    }

    private static IEnumerable<ParameterSeed> ReadParameters(JsonElement element, string requestBodyMode)
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

        if (requestBodyMode == BodyModes.RawJson)
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

    private static string BuildFormBodyTemplate(JsonElement content, JsonElement root)
    {
        if (!content.TryGetProperty("schema", out var schema))
        {
            return string.Empty;
        }

        schema = ResolveSchemaReference(schema, root);
        if (!schema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (var property in properties.EnumerateObject())
        {
            var value = ResolveFormFieldExampleValue(property.Value, root);
            parts.Add($"{Uri.EscapeDataString(property.Name)}={Uri.EscapeDataString(value)}");
        }

        return string.Join("&", parts);
    }

    private static object? BuildSchemaExampleValue(JsonElement schema, JsonElement root, int depth)
    {
        if (depth > 8 || schema.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        schema = ResolveSchemaReference(schema, root);

        if (schema.TryGetProperty("example", out var example))
        {
            return JsonSerializer.Deserialize<object>(example.GetRawText());
        }

        if (schema.TryGetProperty("default", out var defaultValue))
        {
            return JsonSerializer.Deserialize<object>(defaultValue.GetRawText());
        }

        if (schema.TryGetProperty("enum", out var enumValues)
            && enumValues.ValueKind == JsonValueKind.Array
            && enumValues.GetArrayLength() > 0)
        {
            return JsonSerializer.Deserialize<object>(enumValues[0].GetRawText());
        }

        var type = ResolveSchemaType(schema);
        if (type == "array")
        {
            return schema.TryGetProperty("items", out var items)
                ? new[] { BuildSchemaExampleValue(items, root, depth + 1) }
                : Array.Empty<object>();
        }

        if (type == "object" || schema.TryGetProperty("properties", out _))
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in properties.EnumerateObject())
                {
                    result[property.Name] = BuildSchemaExampleValue(property.Value, root, depth + 1);
                }
            }

            return result;
        }

        return type switch
        {
            "integer" => 0,
            "number" => 0,
            "boolean" => true,
            _ => "string"
        };
    }

    private static string ResolveFormFieldExampleValue(JsonElement schema, JsonElement root)
    {
        var value = BuildSchemaExampleValue(schema, root, 0);
        if (value is object?[] array)
        {
            return array.Length == 0 ? string.Empty : ResolveScalarExampleText(array[0]);
        }

        return ResolveScalarExampleText(value);
    }

    private static string ResolveScalarExampleText(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            bool boolean => boolean ? "true" : "false",
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonElement element => element.GetRawText(),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static JsonElement ResolveSchemaReference(JsonElement schema, JsonElement root)
    {
        if (!schema.TryGetProperty("$ref", out var refElement) || refElement.ValueKind != JsonValueKind.String)
        {
            return schema;
        }

        var reference = refElement.GetString();
        const string prefix = "#/components/schemas/";
        if (string.IsNullOrWhiteSpace(reference) || !reference.StartsWith(prefix, StringComparison.Ordinal))
        {
            return schema;
        }

        var schemaName = reference[prefix.Length..];
        return root.TryGetProperty("components", out var components)
               && components.TryGetProperty("schemas", out var schemas)
               && schemas.TryGetProperty(schemaName, out var resolved)
            ? resolved
            : schema;
    }

    private static string ResolveSchemaType(JsonElement schema)
    {
        if (schema.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String)
        {
            return type.GetString() ?? string.Empty;
        }

        return string.Empty;
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

    private readonly record struct RequestBodySeed(string Mode, string Template)
    {
        public static RequestBodySeed Empty { get; } = new(BodyModes.None, string.Empty);
    }
}
