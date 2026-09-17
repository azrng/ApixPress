using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class RequestParameterItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string value = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private bool isRequired;

    [ObservableProperty]
    private bool isEnabled = true;

    /// <summary>form-data 字段类型（Text/File），仅 FormData 模式允许切换；其他参数类型恒为 Text。</summary>
    [ObservableProperty]
    private string fieldType = FormFieldTypes.Text;

    /// <summary>文件字段引用的文件 chip 集合；切回文本类型时保留，便于来回切换不丢失已选文件。</summary>
    public System.Collections.ObjectModel.ObservableCollection<FormFieldFileViewModel> Files { get; } = [];

    public bool IsFileField => FieldType == FormFieldTypes.File;

    public string FieldTypeDisplayText => IsFileField ? "file" : "text";

    public RequestParameterKind ParameterType { get; init; }

    partial void OnFieldTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsFileField));
        OnPropertyChanged(nameof(FieldTypeDisplayText));
    }
}
