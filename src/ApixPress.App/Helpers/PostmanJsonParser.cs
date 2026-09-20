using System.Net;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;
using Azrng.Core.Exceptions;

namespace ApixPress.App.Helpers;

/// <summary>将 Postman Collection v2/v2.1 转换为项目通用的接口文档图。</summary>
public static partial class PostmanJsonParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [GeneratedRegex(@"^(?:https?://)?\{\{\s*baseUrl\s*\}\}", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingBaseUrlVariableRegex();

    /// <summary>剥离开头的 {{baseUrl}} 变量：该变量约定指向环境 BaseUrl，导入后仅保留相对路径，由发送时按当前环境拼接。</summary>
    private static string StripLeadingBaseUrlVariable(string url)
    {
        // 变量不在开头的 URL（含其他变量、绝对地址）原样保留，维持既有导入行为
        if (!LeadingBaseUrlVariableRegex().IsMatch(url))
        {
            return url;
        }

        var stripped = LeadingBaseUrlVariableRegex().Replace(url, string.Empty);
        if (string.IsNullOrWhiteSpace(stripped))
        {
            return "/";
        }

        return stripped.StartsWith('/') ? stripped : $"/{stripped}";
    }

    /// <summary>判断 JSON 根节点是否为 Postman Collection。</summary>
    public static bool IsPostmanCollection(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("item", out var items)
            || items.ValueKind != JsonValueKind.Array
            || !root.TryGetProperty("info", out var info)
            || info.ValueKind != JsonValueKind.Object
            || !info.TryGetProperty("schema", out var schema)
            || schema.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return schema.GetString()?.Contains("postman", StringComparison.OrdinalIgnoreCase) == true;
    }

    public static ParsedDocumentGraph Parse(string json, string sourceType, string sourceValue)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!IsPostmanCollection(root))
        {
            throw new ParameterException("导入失败，文件不是有效的 Postman Collection v2/v2.1。");
        }

        var documentId = Guid.NewGuid().ToString("N");
        var collectionVariables = ReadVariables(root);
        var requests = new List<PostmanRequest>();
        ReadItems(root, ReadAuth(root), string.Empty, requests);

        if (requests.Count == 0)
        {
            throw new ParameterException("导入失败，Postman Collection 中没有可导入的请求。");
        }

        var apiDocument = new ApiDocumentEntity
        {
            Id = documentId,
            Name = ResolveDocumentName(root, sourceValue),
            SourceType = "POSTMAN",
            SourceValue = sourceValue,
            BaseUrl = ResolveBaseUrl(collectionVariables, requests),
            RawJson = json,
            ImportedAt = DateTime.UtcNow
        };
        var endpoints = new List<ApiEndpointEntity>(requests.Count);
        var parameters = new List<RequestParameterEntity>();

        foreach (var request in requests)
        {
            var endpointId = Guid.NewGuid().ToString("N");
            endpoints.Add(new ApiEndpointEntity
            {
                Id = endpointId,
                DocumentId = documentId,
                GroupName = request.FolderPath,
                Name = request.Name,
                Method = request.Method,
                Path = request.Url,
                Description = request.Description,
                RequestBodyMode = request.Body.Mode,
                RequestBodyTemplate = request.Body.Template
            });

            parameters.AddRange(request.Parameters.Select(parameter => new RequestParameterEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                EndpointId = endpointId,
                ParameterType = parameter.ParameterType.ToString(),
                Name = parameter.Name,
                DefaultValue = parameter.Value,
                Description = parameter.Description,
                Required = false
            }));
        }

        return new ParsedDocumentGraph
        {
            Document = apiDocument,
            Endpoints = endpoints,
            Parameters = parameters
        };
    }

    private static void ReadItems(
        JsonElement container,
        JsonElement? inheritedAuth,
        string folderPath,
        ICollection<PostmanRequest> requests)
    {
        if (!container.TryGetProperty("item", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var containerAuth = ReadAuth(container) ?? inheritedAuth;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var itemName = ReadString(item, "name");
            if (item.TryGetProperty("request", out var requestElement)
                && requestElement.ValueKind is JsonValueKind.Object or JsonValueKind.String)
            {
                requests.Add(ReadRequest(itemName, requestElement, containerAuth, folderPath));
                continue;
            }

            if (item.TryGetProperty("item", out var childItems) && childItems.ValueKind == JsonValueKind.Array)
            {
                ReadItems(item, containerAuth, CombineFolderPath(folderPath, itemName), requests);
            }
        }
    }

    private static PostmanRequest ReadRequest(
        string itemName,
        JsonElement requestElement,
        JsonElement? inheritedAuth,
        string folderPath)
    {
        if (requestElement.ValueKind == JsonValueKind.String)
        {
            var rawUrl = requestElement.GetString() ?? string.Empty;
            return new PostmanRequest(
                ResolveRequestName(itemName, "GET", rawUrl),
                "GET",
                NormalizeUrl(rawUrl),
                string.Empty,
                folderPath,
                [],
                RequestBodySeed.Empty);
        }

        var method = ReadString(requestElement, "method").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(method))
        {
            method = "GET";
        }

        var urlInfo = ReadUrl(requestElement);
        var parameters = new List<ParameterSeed>();
        parameters.AddRange(urlInfo.Parameters);
        parameters.AddRange(ReadHeaders(requestElement));
        AddAuthenticationParameters(ReadAuth(requestElement) ?? inheritedAuth, parameters);
        var body = ReadBody(requestElement);
        AddContentTypeHeader(requestElement, body.Mode, parameters);

        return new PostmanRequest(
            ResolveRequestName(itemName, method, urlInfo.Url),
            method,
            urlInfo.Url,
            ReadDescription(requestElement),
            folderPath,
            parameters,
            body);
    }

    private static UrlInfo ReadUrl(JsonElement requestElement)
    {
        if (!requestElement.TryGetProperty("url", out var urlElement))
        {
            return new UrlInfo("/", []);
        }

        if (urlElement.ValueKind == JsonValueKind.String)
        {
            var rawUrl = urlElement.GetString() ?? string.Empty;
            return new UrlInfo(NormalizeUrl(rawUrl), ReadRawQueryParameters(rawUrl));
        }

        if (urlElement.ValueKind != JsonValueKind.Object)
        {
            return new UrlInfo("/", []);
        }

        var raw = ReadString(urlElement, "raw");
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = ComposeUrl(urlElement);
        }

        var query = ReadQueryParameters(urlElement);
        if (query.Count == 0)
        {
            query = ReadRawQueryParameters(raw);
        }

        var pathVariables = ReadPathVariables(urlElement);
        return new UrlInfo(NormalizeUrl(raw), query.Concat(pathVariables).ToList());
    }

    private static string ComposeUrl(JsonElement urlElement)
    {
        var protocol = ReadString(urlElement, "protocol");
        var host = ReadStringArray(urlElement, "host");
        var path = ReadStringArray(urlElement, "path");
        protocol = string.IsNullOrWhiteSpace(protocol) ? "https" : protocol;

        var hostText = string.Join('.', host);
        if (string.IsNullOrWhiteSpace(hostText))
        {
            return "/" + string.Join('/', path);
        }

        return $"{protocol}://{hostText}/{string.Join('/', path)}";
    }

    private static List<ParameterSeed> ReadQueryParameters(JsonElement urlElement)
    {
        if (!urlElement.TryGetProperty("query", out var query) || query.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return query.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object && !ReadBoolean(item, "disabled"))
            .Select(item => new ParameterSeed(
                RequestParameterKind.Query,
                ReadString(item, "key"),
                ReadString(item, "value"),
                ReadString(item, "description")))
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToList();
    }

    private static List<ParameterSeed> ReadPathVariables(JsonElement urlElement)
    {
        if (!urlElement.TryGetProperty("variable", out var variables) || variables.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return variables.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object && !ReadBoolean(item, "disabled"))
            .Select(item => new ParameterSeed(
                RequestParameterKind.Path,
                ResolveKey(item),
                ReadString(item, "value"),
                ReadString(item, "description")))
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToList();
    }

    private static List<ParameterSeed> ReadRawQueryParameters(string rawUrl)
    {
        var queryStart = rawUrl.IndexOf('?');
        if (queryStart < 0 || queryStart == rawUrl.Length - 1)
        {
            return [];
        }

        var query = rawUrl[(queryStart + 1)..];
        var fragmentStart = query.IndexOf('#');
        if (fragmentStart >= 0)
        {
            query = query[..fragmentStart];
        }

        return query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Select(parts => new ParameterSeed(
                RequestParameterKind.Query,
                WebUtility.UrlDecode(parts[0]),
                parts.Length > 1 ? WebUtility.UrlDecode(parts[1]) : string.Empty,
                string.Empty))
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToList();
    }

    private static List<ParameterSeed> ReadHeaders(JsonElement requestElement)
    {
        if (!requestElement.TryGetProperty("header", out var headers) || headers.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return headers.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object && !ReadBoolean(item, "disabled"))
            .Select(item => new ParameterSeed(
                RequestParameterKind.Header,
                ReadString(item, "key"),
                ReadString(item, "value"),
                ReadString(item, "description")))
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToList();
    }

    private static RequestBodySeed ReadBody(JsonElement requestElement)
    {
        if (!requestElement.TryGetProperty("body", out var body) || body.ValueKind != JsonValueKind.Object)
        {
            return RequestBodySeed.Empty;
        }

        return ReadString(body, "mode").Trim().ToLowerInvariant() switch
        {
            "raw" => ReadRawBody(requestElement, body),
            "urlencoded" => ReadUrlEncodedBody(body),
            "formdata" => ReadFormDataBody(body),
            "file" => ReadBinaryBody(body),
            "graphql" => ReadGraphQlBody(body),
            _ => RequestBodySeed.Empty
        };
    }

    private static RequestBodySeed ReadRawBody(JsonElement requestElement, JsonElement body)
    {
        var raw = ReadString(body, "raw");
        var language = string.Empty;
        if (body.TryGetProperty("options", out var options)
            && options.ValueKind == JsonValueKind.Object
            && options.TryGetProperty("raw", out var rawOptions)
            && rawOptions.ValueKind == JsonValueKind.Object)
        {
            language = ReadString(rawOptions, "language");
        }

        if (string.Equals(language, "json", StringComparison.OrdinalIgnoreCase)
            || HasContentType(requestElement, "json")
            || IsJson(raw))
        {
            return new RequestBodySeed(BodyModes.RawJson, raw);
        }

        if (string.Equals(language, "xml", StringComparison.OrdinalIgnoreCase)
            || HasContentType(requestElement, "xml"))
        {
            return new RequestBodySeed(BodyModes.RawXml, raw);
        }

        return new RequestBodySeed(BodyModes.RawText, raw);
    }

    private static RequestBodySeed ReadUrlEncodedBody(JsonElement body)
    {
        var fields = ReadBodyFields(body, "urlencoded", FormFieldTypes.Text);
        return new RequestBodySeed(BodyModes.FormUrlEncoded, FormFieldContentCodec.EncodeTextPairs(
            fields.Where(field => field.IsEnabled).ToList()));
    }

    private static RequestBodySeed ReadFormDataBody(JsonElement body)
    {
        var fields = ReadBodyFields(body, "formdata", FormFieldTypes.Text);
        if (fields.Any(field => field.FieldType == FormFieldTypes.File || !field.IsEnabled))
        {
            return new RequestBodySeed(BodyModes.FormData, JsonSerializer.Serialize(fields, JsonOptions));
        }

        return new RequestBodySeed(BodyModes.FormData, FormFieldContentCodec.EncodeTextPairs(
            fields.Where(field => field.IsEnabled).ToList()));
    }

    private static RequestBodySeed ReadBinaryBody(JsonElement body)
    {
        if (!body.TryGetProperty("file", out var file))
        {
            return RequestBodySeed.Empty;
        }

        var path = file.ValueKind == JsonValueKind.String
            ? file.GetString() ?? string.Empty
            : file.ValueKind == JsonValueKind.Object
                ? ReadString(file, "src")
                : string.Empty;
        return new RequestBodySeed(BodyModes.Binary, path);
    }

    private static List<FormFieldDto> ReadBodyFields(JsonElement body, string propertyName, string defaultType)
    {
        if (!body.TryGetProperty(propertyName, out var fields) || fields.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<FormFieldDto>();
        foreach (var field in fields.EnumerateArray())
        {
            if (field.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var fieldType = string.Equals(ReadString(field, "type"), "file", StringComparison.OrdinalIgnoreCase)
                ? FormFieldTypes.File
                : defaultType;
            result.Add(new FormFieldDto
            {
                Name = ReadString(field, "key"),
                Value = ReadString(field, "value"),
                FieldType = fieldType,
                IsEnabled = !ReadBoolean(field, "disabled"),
                Files = fieldType == FormFieldTypes.File ? ReadFileReferences(field) : []
            });
        }

        return result.Where(field => !string.IsNullOrWhiteSpace(field.Name)).ToList();
    }

    private static List<FormFieldFileDto> ReadFileReferences(JsonElement field)
    {
        var references = new List<string>();
        if (field.TryGetProperty("src", out var source))
        {
            if (source.ValueKind == JsonValueKind.String)
            {
                references.Add(source.GetString() ?? string.Empty);
            }
            else if (source.ValueKind == JsonValueKind.Array)
            {
                references.AddRange(source.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty));
            }
        }

        return references
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => new FormFieldFileDto
            {
                Path = path,
                Name = Path.GetFileName(path)
            })
            .ToList();
    }

    private static RequestBodySeed ReadGraphQlBody(JsonElement body)
    {
        if (!body.TryGetProperty("graphql", out var graphql) || graphql.ValueKind != JsonValueKind.Object)
        {
            return RequestBodySeed.Empty;
        }

        var content = new Dictionary<string, object?>
        {
            ["query"] = ReadString(graphql, "query")
        };
        if (graphql.TryGetProperty("variables", out var variables))
        {
            content["variables"] = variables.Clone();
        }

        return new RequestBodySeed(BodyModes.RawJson, JsonSerializer.Serialize(content, JsonOptions));
    }

    private static void AddAuthenticationParameters(JsonElement? auth, ICollection<ParameterSeed> parameters)
    {
        if (auth is null
            || !auth.Value.TryGetProperty("type", out var typeElement)
            || typeElement.ValueKind != JsonValueKind.String)
        {
            return;
        }

        switch (typeElement.GetString()?.Trim().ToLowerInvariant())
        {
            case "bearer":
                var token = ReadAuthValue(auth.Value, "bearer", "token");
                if (!string.IsNullOrWhiteSpace(token) && !HasHeader(parameters, "Authorization"))
                {
                    var value = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? token
                        : $"Bearer {token}";
                    parameters.Add(new ParameterSeed(RequestParameterKind.Header, "Authorization", value, "Postman bearer 认证"));
                }
                break;
            case "apikey":
                var key = ReadAuthValue(auth.Value, "apikey", "key");
                var apiKeyValue = ReadAuthValue(auth.Value, "apikey", "value");
                var location = ReadAuthValue(auth.Value, "apikey", "in");
                if (!string.IsNullOrWhiteSpace(key) && string.Equals(location, "query", StringComparison.OrdinalIgnoreCase))
                {
                    parameters.Add(new ParameterSeed(RequestParameterKind.Query, key, apiKeyValue, "Postman API Key 认证"));
                }
                else if (!string.IsNullOrWhiteSpace(key) && !HasHeader(parameters, key))
                {
                    parameters.Add(new ParameterSeed(RequestParameterKind.Header, key, apiKeyValue, "Postman API Key 认证"));
                }
                break;
            case "basic":
                var username = ReadAuthValue(auth.Value, "basic", "username");
                var password = ReadAuthValue(auth.Value, "basic", "password");
                if (!HasHeader(parameters, "Authorization")
                    && (!string.IsNullOrWhiteSpace(username) || !string.IsNullOrWhiteSpace(password)))
                {
                    var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
                    parameters.Add(new ParameterSeed(RequestParameterKind.Header, "Authorization", $"Basic {credentials}", "Postman basic 认证"));
                }
                break;
        }
    }

    private static void AddContentTypeHeader(JsonElement requestElement, string bodyMode, ICollection<ParameterSeed> parameters)
    {
        if (bodyMode == BodyModes.None || HasHeader(parameters, "Content-Type") || HasContentType(requestElement, string.Empty))
        {
            return;
        }

        var contentType = bodyMode switch
        {
            BodyModes.RawJson => "application/json",
            BodyModes.RawXml => "application/xml",
            BodyModes.FormData => "multipart/form-data",
            BodyModes.FormUrlEncoded => "application/x-www-form-urlencoded",
            BodyModes.RawText => "text/plain",
            BodyModes.Binary => "application/octet-stream",
            _ => string.Empty
        };
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            parameters.Add(new ParameterSeed(RequestParameterKind.Header, "Content-Type", contentType, "根据 Postman 请求体自动补充"));
        }
    }

    private static bool HasContentType(JsonElement requestElement, string contentTypePart)
    {
        if (!requestElement.TryGetProperty("header", out var headers) || headers.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return headers.EnumerateArray().Any(header =>
            header.ValueKind == JsonValueKind.Object
            && !ReadBoolean(header, "disabled")
            && string.Equals(ReadString(header, "key"), "Content-Type", StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(contentTypePart)
                || ReadString(header, "value").Contains(contentTypePart, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool HasHeader(IEnumerable<ParameterSeed> parameters, string name)
    {
        return parameters.Any(parameter => parameter.ParameterType == RequestParameterKind.Header
            && string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonElement? ReadAuth(JsonElement element)
    {
        if (!element.TryGetProperty("auth", out var auth)
            || auth.ValueKind != JsonValueKind.Object
            || string.IsNullOrWhiteSpace(ReadString(auth, "type")))
        {
            return null;
        }

        return auth;
    }

    private static string ReadAuthValue(JsonElement auth, string propertyName, string key)
    {
        if (!auth.TryGetProperty(propertyName, out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var item in values.EnumerateArray())
        {
            if (string.Equals(ReadString(item, "key"), key, StringComparison.OrdinalIgnoreCase))
            {
                return ReadString(item, "value");
            }
        }

        return string.Empty;
    }

    private static Dictionary<string, string> ReadVariables(JsonElement root)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("variable", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return variables;
        }

        foreach (var item in items.EnumerateArray())
        {
            var key = ResolveKey(item);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var current = ReadString(item, "current");
            variables[key] = string.IsNullOrWhiteSpace(current) ? ReadString(item, "value") : current;
        }

        return variables;
    }

    private static string ResolveBaseUrl(IReadOnlyDictionary<string, string> variables, IEnumerable<PostmanRequest> requests)
    {
        if (variables.TryGetValue("baseUrl", out var baseUrl)
            && Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
        {
            return baseUrl.TrimEnd('/');
        }

        foreach (var request in requests)
        {
            if (Uri.TryCreate(request.Url, UriKind.Absolute, out var requestUri))
            {
                return requestUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            }
        }

        return string.Empty;
    }

    private static string ResolveDocumentName(JsonElement root, string sourceValue)
    {
        if (root.TryGetProperty("info", out var info) && info.ValueKind == JsonValueKind.Object)
        {
            var name = ReadString(info, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return Path.GetFileNameWithoutExtension(sourceValue) switch
        {
            { Length: > 0 } name => name,
            _ => "未命名 Postman 集合"
        };
    }

    private static string ResolveRequestName(string itemName, string method, string url)
    {
        return string.IsNullOrWhiteSpace(itemName) ? $"{method} {url}" : itemName.Trim();
    }

    private static string ReadDescription(JsonElement requestElement)
    {
        if (!requestElement.TryGetProperty("description", out var description))
        {
            return string.Empty;
        }

        return description.ValueKind == JsonValueKind.String
            ? description.GetString() ?? string.Empty
            : description.ValueKind == JsonValueKind.Object
                ? ReadString(description, "content")
                : string.Empty;
    }

    private static string NormalizeUrl(string url)
    {
        var normalized = url.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "/" : StripLeadingBaseUrlVariable(RemoveQueryAndFragment(normalized));
    }

    private static string RemoveQueryAndFragment(string url)
    {
        var end = url.IndexOfAny(['?', '#']);
        var normalized = end >= 0 ? url[..end] : url;
        return string.IsNullOrWhiteSpace(normalized) ? "/" : normalized;
    }

    private static string CombineFolderPath(string parent, string child)
    {
        if (string.IsNullOrWhiteSpace(child))
        {
            return parent;
        }

        return string.IsNullOrWhiteSpace(parent) ? child.Trim() : $"{parent.TrimEnd('/')} / {child.Trim()}";
    }

    private static string ResolveKey(JsonElement element)
    {
        var key = ReadString(element, "key");
        return string.IsNullOrWhiteSpace(key) ? ReadString(element, "name") : key;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool ReadBoolean(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;
    }

    private static List<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return values.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static bool IsJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record PostmanRequest(
        string Name,
        string Method,
        string Url,
        string Description,
        string FolderPath,
        IReadOnlyList<ParameterSeed> Parameters,
        RequestBodySeed Body);

    private sealed record UrlInfo(string Url, IReadOnlyList<ParameterSeed> Parameters);

    private sealed record ParameterSeed(
        RequestParameterKind ParameterType,
        string Name,
        string Value,
        string Description);

    private sealed record RequestBodySeed(string Mode, string Template)
    {
        public static RequestBodySeed Empty { get; } = new(BodyModes.None, string.Empty);
    }
}
