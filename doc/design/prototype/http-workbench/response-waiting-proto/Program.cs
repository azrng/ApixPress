using Avalonia;

namespace ApixPress.ResponseWaitingPrototype;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppBuilder.Configure<App>()
            .UseWin32()
            .UseSkia()
            .UseHarfBuzz()
            .StartWithClassicDesktopLifetime(args);
    }
}
