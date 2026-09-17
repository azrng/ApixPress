using System.Text.Json;
using ApixPress.App.Models.DTOs;

namespace ApixPress.App.Helpers;

/// <summary>
/// form-data 字段列表与 BodyContent 之间的编解码。
/// 双格式兼容：纯文本字段保持旧的 URL 编码串（a=1&amp;b=2，与既有数据逐字节兼容）；
/// 含文件字段时落 JSON 数组——旧写入方会对值整体百分号编码，"[" 不可能出现在旧格式首字符，据此嗅探。
/// </summary>
public static class FormFieldContentCodec
{
    private static readonly JsonSerializerOptions ParseOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>按发送口径编码：文本字段走 URL 编码串，存在文件字段时整体转 JSON（由 FormData 模式调用）。</summary>
    public static string Encode(IReadOnlyList<FormFieldDto> fields)
    {
        if (fields.Any(field => field.FieldType == FormFieldTypes.File && field.Files.Count > 0))
        {
            return JsonSerializer.Serialize(fields);
        }

        return EncodeTextPairs(fields);
    }

    /// <summary>恒以 URL 编码串编码（FormUrlEncoded 模式不承载文件，保证旧解析器可读）。</summary>
    public static string EncodeTextPairs(IReadOnlyList<FormFieldDto> fields)
    {
        return string.Join("&", fields.Select(field =>
            $"{Uri.EscapeDataString(field.Name)}={Uri.EscapeDataString(field.Value)}"));
    }

    /// <summary>解析 BodyContent 为字段列表，自动识别 JSON 与旧 URL 编码两种格式。</summary>
    public static List<FormFieldDto> Parse(string? bodyContent)
    {
        var trimmed = bodyContent?.TrimStart();
        if (string.IsNullOrEmpty(trimmed))
        {
            return [];
        }

        if (trimmed.StartsWith('['))
        {
            try
            {
                return JsonSerializer.Deserialize<List<FormFieldDto>>(trimmed, ParseOptions) ?? [];
            }
            catch (JsonException)
            {
                // BodyContent 可能被手工改坏；解析失败按空处理，避免加载历史记录时崩溃
                return [];
            }
        }

        return trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair =>
            {
                var parts = pair.Split('=', 2);
                var name = Uri.UnescapeDataString(parts[0]);
                return new FormFieldDto
                {
                    Name = name,
                    Value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty,
                    FieldType = FormFieldTypes.Text,
                    IsEnabled = true
                };
            })
            .Where(field => !string.IsNullOrWhiteSpace(field.Name))
            .ToList();
    }
}
