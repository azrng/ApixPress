using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Tests.ViewModels;

public sealed class ProjectHttpDocumentFormatterTests
{
    private static RequestConfigTabViewModel CreateConfig(string bodyMode, Action<RequestConfigTabViewModel>? setup = null)
    {
        var viewModel = new RequestConfigTabViewModel
        {
            SelectedBodyMode = bodyMode
        };
        setup?.Invoke(viewModel);
        return viewModel;
    }

    [Fact]
    public void GeneratedSnippets_ShouldSkipDisabledHeadersAndEscapeShellExpansion()
    {
        var config = CreateConfig(BodyModes.RawText, vm =>
        {
            vm.RequestBody = "$(echo bad) `cmd` $token";
            vm.Headers.Add(new RequestParameterItemViewModel { Name = "X-Off", Value = "secret", IsEnabled = false });
            vm.Headers.Add(new RequestParameterItemViewModel { Name = "X-On", Value = "ok", IsEnabled = true });
        });
        var curl = ProjectHttpDocumentFormatter.BuildCurlSnippet("POST", "https://example.com", "", config);
        var wget = ProjectHttpDocumentFormatter.BuildWgetSnippet("POST", "https://example.com", "", config);
        var ps = ProjectHttpDocumentFormatter.BuildPowerShellSnippet("POST", "https://example.com", "", config);
        var cs = ProjectHttpDocumentFormatter.BuildCSharpHttpClientSnippet("POST", "https://example.com", "", config);
        foreach (var snippet in new[] { curl, wget, ps, cs })
        {
            Assert.DoesNotContain("X-Off", snippet);
            Assert.Contains("X-On", snippet);
        }
        Assert.Contains("\\$(echo bad) \\`cmd\\` \\$token", curl);
        Assert.Contains("\\$(echo bad) \\`cmd\\` \\$token", wget);
        Assert.Contains("`$(echo bad) ``cmd`` `$token", ps);
    }

    [Fact]
    public void GeneratedSnippets_ShouldResolvePathParametersAndKeepQueryAndFragment()
    {
        var config = CreateConfig(BodyModes.None, vm =>
        {
            vm.PathParameters.Add(new RequestParameterItemViewModel { Name = "teamId", Value = "group/a b" });
            vm.QueryParameters.Add(new RequestParameterItemViewModel { Name = "expand", Value = "profile" });
        });

        var curl = ProjectHttpDocumentFormatter.BuildCurlSnippet(
            "GET",
            "/teams/:teamId?existing=1#details",
            "https://api.example.com",
            config);

        Assert.Contains("https://api.example.com/teams/group%2Fa%20b?existing=1&expand=profile#details", curl);
    }

    [Fact]
    public void BuildCSharpSnippet_ShouldApplyContentHeadersAfterContentCreation()
    {
        var config = CreateConfig(BodyModes.FormUrlEncoded, vm =>
        {
            vm.FormFields.Add(new RequestParameterItemViewModel { Name = "id", Value = "1" });
            vm.FormFields.Add(new RequestParameterItemViewModel { Name = "id", Value = "2" });
            vm.Headers.Add(new RequestParameterItemViewModel { Name = "Content-Type", Value = "application/custom; charset=utf-8" });
            vm.Headers.Add(new RequestParameterItemViewModel { Name = "Content-Language", Value = "zh-CN" });
        });
        var cs = ProjectHttpDocumentFormatter.BuildCSharpHttpClientSnippet("POST", "https://example.com", "", config);
        Assert.Contains("new(\"id\", \"1\")", cs);
        Assert.Contains("new(\"id\", \"2\")", cs);
        Assert.Contains("request.Content.Headers.Remove(\"Content-Type\")", cs);
        Assert.Contains("application/custom; charset=utf-8", cs);
        Assert.Contains("request.Content?.Headers.TryAddWithoutValidation(\"Content-Language\", \"zh-CN\")", cs);
        Assert.True(cs.IndexOf("request.Content =", StringComparison.Ordinal) < cs.IndexOf("Content-Language", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildCurlSnippet_FormDataWithFileField_ShouldUseFormUploadSyntax()
    {
        var configTab = CreateConfig(BodyModes.FormData, vm =>
        {
            vm.FormFields.Add(new RequestParameterItemViewModel { Name = "title", Value = "hello" });
            var fileField = new RequestParameterItemViewModel { Name = "file", FieldType = FormFieldTypes.File };
            fileField.Files.Add(new FormFieldFileViewModel { Path = @"C:\temp\a.txt", Name = "a.txt" });
            vm.FormFields.Add(fileField);
        });

        var curl = ProjectHttpDocumentFormatter.BuildCurlSnippet("POST", "/upload", "https://api.example.com", configTab);

        // 文件字段走 -F 上传口径（shell 双引号内反斜杠按既有口径翻倍）；含 -F 时不再输出误导性的 --data-raw 空值
        Assert.Contains("--form \"title=hello\"", curl);
        Assert.Contains("--form \"file=@C:\\\\temp\\\\a.txt\"", curl);
        Assert.DoesNotContain("--data-raw", curl);
    }

    [Fact]
    public void BuildCurlSnippet_BinaryMode_ShouldUseDataBinaryFileSyntax()
    {
        var configTab = CreateConfig(BodyModes.Binary, vm => vm.RequestBody = @"C:\temp\blob.bin");

        var curl = ProjectHttpDocumentFormatter.BuildCurlSnippet("POST", "/blob", "https://api.example.com", configTab);

        // 文件路径不得作为正文文本输出
        Assert.Contains("--data-binary \"@C:\\\\temp\\\\blob.bin\"", curl);
        Assert.DoesNotContain("--data-raw", curl);
    }

    [Fact]
    public void BuildCurlSnippet_FormUrlEncoded_ShouldKeepLegacyDataRaw()
    {
        var configTab = CreateConfig(BodyModes.FormUrlEncoded, vm =>
            vm.FormFields.Add(new RequestParameterItemViewModel { Name = "a", Value = "1" }));

        var curl = ProjectHttpDocumentFormatter.BuildCurlSnippet("POST", "/submit", "https://api.example.com", configTab);

        Assert.Contains("--data-raw \"a=1\"", curl);
    }

    [Fact]
    public void BuildWgetSnippet_FormDataWithFiles_ShouldEmitReadableHintInsteadOfBrokenCommand()
    {
        var configTab = CreateConfig(BodyModes.FormData, vm =>
        {
            var fileField = new RequestParameterItemViewModel { Name = "file", FieldType = FormFieldTypes.File };
            fileField.Files.Add(new FormFieldFileViewModel { Path = @"C:\a.txt", Name = "a.txt" });
            vm.FormFields.Add(fileField);
        });

        var wget = ProjectHttpDocumentFormatter.BuildWgetSnippet("POST", "/upload", "https://api.example.com", configTab);

        // wget 无 multipart 能力，给可读提示而非看似可用实则错误的命令
        Assert.Contains("wget 不支持 multipart 文件上传", wget);
    }

    [Fact]
    public void BuildWgetSnippet_BinaryMode_ShouldUseBodyFile()
    {
        var configTab = CreateConfig(BodyModes.Binary, vm => vm.RequestBody = @"C:\temp\blob.bin");

        var wget = ProjectHttpDocumentFormatter.BuildWgetSnippet("POST", "/blob", "https://api.example.com", configTab);

        Assert.Contains("--body-file=\"C:\\\\temp\\\\blob.bin\"", wget);
    }

    [Fact]
    public void BuildPowerShellSnippet_FormDataWithFileField_ShouldUseFormWithGetItem()
    {
        var configTab = CreateConfig(BodyModes.FormData, vm =>
        {
            var fileField = new RequestParameterItemViewModel { Name = "file", FieldType = FormFieldTypes.File };
            fileField.Files.Add(new FormFieldFileViewModel { Path = @"C:\temp\a.txt", Name = "a.txt" });
            vm.FormFields.Add(fileField);
        });

        var ps = ProjectHttpDocumentFormatter.BuildPowerShellSnippet("POST", "/upload", "https://api.example.com", configTab);

        // PowerShell 转义不翻倍反斜杠，路径保持原样
        Assert.Contains("-Form @{", ps);
        Assert.Contains("\"file\" = (Get-Item \"C:\\temp\\a.txt\")", ps);
    }

    [Fact]
    public void BuildPowerShellSnippet_BinaryMode_ShouldUseInFile()
    {
        var configTab = CreateConfig(BodyModes.Binary, vm => vm.RequestBody = @"C:\temp\blob.bin");

        var ps = ProjectHttpDocumentFormatter.BuildPowerShellSnippet("POST", "/blob", "https://api.example.com", configTab);

        Assert.Contains("-InFile \"C:\\temp\\blob.bin\"", ps);
    }

    [Fact]
    public void BuildCSharpSnippet_FormDataWithFileField_ShouldUseStreamContentWithFilename()
    {
        var configTab = CreateConfig(BodyModes.FormData, vm =>
        {
            var fileField = new RequestParameterItemViewModel { Name = "file", FieldType = FormFieldTypes.File };
            fileField.Files.Add(new FormFieldFileViewModel { Path = @"C:\temp\a.txt", Name = "a.txt" });
            vm.FormFields.Add(fileField);
        });

        var csharp = ProjectHttpDocumentFormatter.BuildCSharpHttpClientSnippet("POST", "/upload", "https://api.example.com", configTab);

        // 三参 Add 写入 filename（反斜杠在普通字符串里翻倍转义）；不得把文件字段当空文本发送
        Assert.True(csharp.Contains("File.OpenRead(\"C:\\\\temp\\\\a.txt\")"), $"ACTUAL={csharp}");
        Assert.True(csharp.Contains(", \"file\", \"a.txt\");"), $"ACTUAL2={csharp}");
        Assert.DoesNotContain("StringContent(\"\"), \"file\"", csharp);
    }

    [Fact]
    public void BuildCSharpSnippet_BinaryMode_ShouldUseStreamContent()
    {
        var configTab = CreateConfig(BodyModes.Binary, vm => vm.RequestBody = @"C:\temp\blob.bin");

        var csharp = ProjectHttpDocumentFormatter.BuildCSharpHttpClientSnippet("POST", "/blob", "https://api.example.com", configTab);

        Assert.Contains("new StreamContent(File.OpenRead(\"C:\\\\temp\\\\blob.bin\"))", csharp);
    }

    [Fact]
    public void ResolveBodyContent_BinaryMode_ShouldReturnEmptyInsteadOfFilePath()
    {
        var configTab = CreateConfig(BodyModes.Binary, vm => vm.RequestBody = @"C:\temp\blob.bin");

        // 文件路径不是正文文本，混进 --data-raw/-Body 会生成错误代码
        Assert.Equal(string.Empty, ProjectHttpDocumentFormatter.ResolveBodyContent(configTab));
    }
}
