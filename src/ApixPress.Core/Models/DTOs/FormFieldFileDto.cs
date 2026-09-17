namespace ApixPress.App.Models.DTOs;

/// <summary>文件字段的单个文件引用：仅记录路径与元信息，发送时按路径读取文件内容。</summary>
public sealed class FormFieldFileDto
{
    public string Path { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public long SizeBytes { get; init; }
}
