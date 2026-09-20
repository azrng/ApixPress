namespace ApixPress.App.ViewModels;

/// <summary>
/// 响应 Headers 只读键值行：不可变记录，避免逐行 ViewModel 通知开销
/// </summary>
public sealed record ResponseHeaderRowViewModel(string Name, string Value);
