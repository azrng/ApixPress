using System.Text.Json;

namespace ApixPress.App.Helpers;

/// <summary>按 JSON 文档特征选择已有 OpenAPI 或 Postman 解析器。</summary>
public static class ImportDocumentParser
{
    public static ParsedDocumentGraph Parse(string json, string sourceType, string sourceValue)
    {
        using var document = JsonDocument.Parse(json);
        return PostmanJsonParser.IsPostmanCollection(document.RootElement)
            ? PostmanJsonParser.Parse(json, sourceType, sourceValue)
            : OpenApiJsonParser.Parse(json, sourceType, sourceValue);
    }
}
