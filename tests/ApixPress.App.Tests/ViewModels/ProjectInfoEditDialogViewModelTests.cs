using ApixPress.App.Tests.ViewModels;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Tests.ViewModels;

public sealed class ProjectInfoEditDialogViewModelTests
{
    [Fact]
    public void NameMode_ShouldCarryTitleLabelAndMaxLength()
    {
        // 对齐原型 06 editProjectModal：标题 编辑项目名称、label 项目名称、maxlength 80
        var dialog = new ProjectInfoEditDialogViewModel(true, "example", _ => Task.FromResult<string?>(null));

        Assert.Equal("编辑项目名称", dialog.Title);
        Assert.Equal("项目名称", dialog.FieldLabel);
        Assert.True(dialog.IsNameMode);
        Assert.Equal(80, dialog.MaxLength);
        Assert.Equal("example", dialog.Value);
    }

    [Fact]
    public void DescriptionMode_ShouldCarryTitleLabelAndMaxLength()
    {
        var dialog = new ProjectInfoEditDialogViewModel(false, "简介", _ => Task.FromResult<string?>(null));

        Assert.Equal("编辑项目简介", dialog.Title);
        Assert.Equal("项目简介", dialog.FieldLabel);
        Assert.False(dialog.IsNameMode);
        Assert.Equal(300, dialog.MaxLength);
    }

    [Fact]
    public async Task Save_Success_ShouldCloseDialogWithTrimmedValue()
    {
        string? saved = null;
        var dialog = new ProjectInfoEditDialogViewModel(true, "旧名", value =>
        {
            saved = value;
            return Task.FromResult<string?>(null);
        });
        var closed = false;
        dialog.RequestClose += (_, _) => closed = true;

        dialog.Value = "  新名  ";
        await dialog.SaveCommand.ExecuteAsync(null);

        Assert.True(closed);
        Assert.Equal("新名", saved);
        Assert.Equal(string.Empty, dialog.ErrorMessage);
    }

    [Fact]
    public async Task Save_Failure_ShouldSurfaceErrorAndKeepDialogOpen()
    {
        var dialog = new ProjectInfoEditDialogViewModel(true, string.Empty, value =>
            Task.FromResult<string?>(string.IsNullOrWhiteSpace(value) ? "项目名称不能为空。" : null));
        var closed = false;
        dialog.RequestClose += (_, _) => closed = true;

        await dialog.SaveCommand.ExecuteAsync(null);

        Assert.False(closed);
        Assert.Equal("项目名称不能为空。", dialog.ErrorMessage);

        // 修复输入后再次保存成功，弹窗关闭
        dialog.Value = "修正名";
        await dialog.SaveCommand.ExecuteAsync(null);
        Assert.True(closed);
    }
}
