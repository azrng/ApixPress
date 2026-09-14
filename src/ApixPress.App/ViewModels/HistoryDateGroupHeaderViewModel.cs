namespace ApixPress.App.ViewModels;

/// <summary>
/// 请求历史日期分组标题（今天 / 昨天 / 更早日期），用于扁平列表内的分组行
/// </summary>
public sealed class HistoryDateGroupHeaderViewModel
{
    public HistoryDateGroupHeaderViewModel(string title, int count)
    {
        Title = title;
        Count = count;
    }

    public string Title { get; }

    public int Count { get; }
}
