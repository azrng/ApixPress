using System.Reflection;
using ApixPress.App.Data.Context;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApixPress.App;

public static class ServiceBootstrapper
{
    public static IServiceProvider Build()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.RegisterBusinessServices(Assembly.GetExecutingAssembly());
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<DatabaseInitializer>();
        services.AddTransient<MainWindow>();
        services.AddSingleton<IWindowHostService, Services.Implementations.WindowHostService>();

        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<DatabaseInitializer>().Initialize();
        return serviceProvider;
    }
}
