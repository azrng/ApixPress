namespace ApixPress.App.Services.Interfaces;

/// <summary>
/// 强调色预设服务（T123）：运行时切换品牌强调色并同步派生色族（悬停/渐变/焦点环/链接/浅底文字）。
/// 实现须在 UI 线程调用（直接改画刷实例颜色触发重绘）。
/// </summary>
public interface IAccentPaletteService
{
    /// <summary>按预设标识应用强调色；未知标识回退默认预设（teal）。</summary>
    void Apply(string accentTheme);
}
