using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ApixPress.App.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ApixPress.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // UI 线程异常挂点：绑定回调、输入事件、渲染回调里的异常落盘；
        // 不设 e.Handled=true——状态已损坏时静默继续比带日志退出更危险
        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            CrashLog.Write(e.Exception, "UIThread");
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = PerfLog.Measure("启动·主窗口构造", () => Services.GetRequiredService<MainWindow>());
            PerfLog.Log("启动·进程到主窗口就绪", Program.StartupStopwatch.ElapsedMilliseconds);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
