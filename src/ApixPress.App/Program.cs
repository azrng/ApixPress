using Avalonia;
using System;
using System.Threading.Tasks;
using ApixPress.App.Helpers;
using ApixPress.App.Services.Implementations;

namespace ApixPress.App;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        // 崩溃日志挂点必须在一切业务逻辑之前：后台线程未处理异常与未观察任务异常
        // 不落盘的话，直接运行（不挂调试器）的闪退将无任何线索可查
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            CrashLog.Write(e.ExceptionObject as Exception, "AppDomain");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            CrashLog.Write(e.Exception, "UnobservedTask");
            e.SetObserved();
        };

        if (AppUpdateRunner.IsUpdateMode(args))
        {
            return AppUpdateRunner.RunAsync(args).GetAwaiter().GetResult();
        }

        App.Services = ServiceBootstrapper.Build();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
