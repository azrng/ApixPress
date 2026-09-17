using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

/// <summary>form-data 文件字段的单个文件 chip：展示文件名与大小，支持按路径移除。</summary>
public partial class FormFieldFileViewModel : ViewModelBase
{
    [ObservableProperty]
    private string path = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    public long SizeBytes { get; init; }

    public string SizeText => FormatSize(SizeBytes);

    /// <summary>按 B/KB/MB/GB 分档格式化文件大小，超过两位小数截断。</summary>
    public static string FormatSize(long bytes)
    {
        if (bytes < 0)
        {
            return "0 B";
        }

        const long Kb = 1024;
        const long Mb = Kb * 1024;
        const long Gb = Mb * 1024;
        return bytes switch
        {
            >= Gb => $"{bytes / (double)Gb:0.##} GB",
            >= Mb => $"{bytes / (double)Mb:0.##} MB",
            >= Kb => $"{bytes / (double)Kb:0.##} KB",
            _ => $"{bytes} B"
        };
    }
}
