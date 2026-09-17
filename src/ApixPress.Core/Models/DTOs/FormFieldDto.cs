namespace ApixPress.App.Models.DTOs;

/// <summary>
/// form-data 表单字段：文本字段携带 <see cref="Value"/>，文件字段携带 <see cref="Files"/>。
/// 随 <see cref="RequestSnapshotDto"/> 持久化；BodyContent 中的历史编码格式由 FormFieldContentCodec 兼容解析。
/// </summary>
public sealed class FormFieldDto
{
    public string Name { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string FieldType { get; init; } = FormFieldTypes.Text;

    public bool IsEnabled { get; init; } = true;

    public List<FormFieldFileDto> Files { get; init; } = [];
}
