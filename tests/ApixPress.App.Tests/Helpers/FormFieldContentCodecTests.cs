using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;

namespace ApixPress.App.Tests.Helpers;

public sealed class FormFieldContentCodecTests
{
    [Fact]
    public void Encode_TextOnlyFields_ShouldKeepLegacyUrlEncodedFormat()
    {
        var fields = new List<FormFieldDto>
        {
            new() { Name = "name", Value = "张三" },
            new() { Name = "page", Value = "1" }
        };

        var encoded = FormFieldContentCodec.Encode(fields);

        // 与既有数据逐字节兼容：纯文本字段保持 URL 编码串，不以 JSON 形式落盘
        Assert.DoesNotContain("[", encoded);
        Assert.Contains("name=", encoded);
        Assert.Contains("page=1", encoded);
    }

    [Fact]
    public void Encode_WithFileField_ShouldEmitJsonAndRoundTripPreserveFiles()
    {
        var fields = new List<FormFieldDto>
        {
            new() { Name = "title", Value = "hello" },
            new()
            {
                Name = "file",
                FieldType = FormFieldTypes.File,
                Files =
                [
                    new FormFieldFileDto { Path = @"C:\temp\a.txt", Name = "a.txt", SizeBytes = 12 },
                    new FormFieldFileDto { Path = @"C:\temp\b.png", Name = "b.png", SizeBytes = 2048 }
                ]
            }
        };

        var encoded = FormFieldContentCodec.Encode(fields);
        Assert.StartsWith("[", encoded);

        var parsed = FormFieldContentCodec.Parse(encoded);
        Assert.Equal(2, parsed.Count);
        Assert.Equal(FormFieldTypes.File, parsed[1].FieldType);
        Assert.Equal(2, parsed[1].Files.Count);
        Assert.Equal(@"C:\temp\a.txt", parsed[1].Files[0].Path);
        Assert.Equal(2048, parsed[1].Files[1].SizeBytes);
    }

    [Fact]
    public void Parse_LegacyEncodedString_ShouldReturnEnabledTextFields()
    {
        var parsed = FormFieldContentCodec.Parse("name=%E5%BC%A0%E4%B8%89&page=1&=ignored");

        Assert.Equal(2, parsed.Count);
        Assert.Equal("name", parsed[0].Name);
        Assert.Equal("张三", parsed[0].Value);
        Assert.Equal(FormFieldTypes.Text, parsed[0].FieldType);
        Assert.True(parsed[0].IsEnabled);
        Assert.Equal("1", parsed[1].Value);
    }

    [Fact]
    public void Parse_EmptyOrBrokenContent_ShouldReturnEmptyList()
    {
        Assert.Empty(FormFieldContentCodec.Parse(null));
        Assert.Empty(FormFieldContentCodec.Parse(string.Empty));
        // 手工改坏的 JSON 不应让加载崩溃
        Assert.Empty(FormFieldContentCodec.Parse("[{\"broken\""));
    }

    [Fact]
    public void EncodeTextPairs_ShouldAlwaysUseUrlEncodedEvenWithFileFields()
    {
        var fields = new List<FormFieldDto>
        {
            new() { Name = "a", Value = "1" },
            new() { Name = "file", FieldType = FormFieldTypes.File, Files = [new FormFieldFileDto { Path = "p", Name = "n" }] }
        };

        // FormUrlEncoded 模式不承载文件，恒走 URL 编码保证旧解析器可读
        var encoded = FormFieldContentCodec.EncodeTextPairs(fields);
        Assert.DoesNotContain("[", encoded);
        Assert.Contains("a=1", encoded);
    }
}
