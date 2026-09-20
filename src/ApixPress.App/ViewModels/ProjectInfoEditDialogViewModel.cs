using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.ViewModels.Base;
using Irihi.Avalonia.Shared.Contracts;

namespace ApixPress.App.ViewModels;

/// <summary>
/// 单字段编辑弹窗上下文（项目名称/简介、用例重命名共用）：标题与字段标签可参数化，
/// 保存委托由宿主 VM 注入——返回 null 表示成功并关闭弹窗，否则回显错误文案。
/// </summary>
public sealed partial class ProjectInfoEditDialogViewModel : ViewModelBase, IDialogContext
{
    private readonly Func<string, Task<string?>> _saveAsync;

    public ProjectInfoEditDialogViewModel(
        bool isNameMode,
        string initialValue,
        Func<string, Task<string?>> saveAsync,
        string? title = null,
        string? fieldLabel = null)
    {
        _saveAsync = saveAsync;
        IsNameMode = isNameMode;
        title = string.IsNullOrWhiteSpace(title) ? (isNameMode ? "编辑项目名称" : "编辑项目简介") : title;
        fieldLabel = string.IsNullOrWhiteSpace(fieldLabel) ? (isNameMode ? "项目名称" : "项目简介") : fieldLabel;
        this.title = title;
        this.fieldLabel = fieldLabel;
        value = initialValue;
    }

    /// <summary>弹窗异步关闭通道：保存成功、取消与 chrome 关闭共用。</summary>
    public event EventHandler<object?>? RequestClose;

    public void Close() => RequestClose?.Invoke(this, null);

    /// <summary>名称模式：单行输入、maxlength 80；简介模式：多行输入、maxlength 300（对齐原型）。</summary>
    public bool IsNameMode { get; }

    public int MaxLength => IsNameMode ? 80 : 300;

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private string fieldLabel;

    [ObservableProperty]
    private string value;

    /// <summary>保存失败回显文案；空表示无错误。</summary>
    [ObservableProperty]
    private string errorMessage = string.Empty;

    [RelayCommand]
    private async Task SaveAsync()
    {
        var error = await _saveAsync(Value.Trim());
        if (error is not null)
        {
            ErrorMessage = error;
            return;
        }

        RequestClose?.Invoke(this, null);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, null);
}
