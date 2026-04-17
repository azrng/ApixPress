using System.Reflection;
using ApixPress.App.Data.Context;
using ApixPress.App.Helpers;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels;
using Azrng.Core.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApixPress.App;

public static class ServiceBootstrapper
{
    public static IServiceProvider Build()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonStream(EmbeddedResourceReader.OpenRequiredStream(assembly, "appsettings.json"))
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.ConfigureDefaultJson(_ => { });
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
