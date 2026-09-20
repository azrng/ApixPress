using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Azrng.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace ApixPress.App.Helpers;

/// <summary>
/// 按标记接口批量注册应用服务。
/// 本地实现，语义与 Azrng.AspNetCore.Core 的同名扩展保持一致，
/// 使桌面应用不再因该扩展引入 ASP.NET Core 框架引用
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 扫描指定程序集，按 ITransientDependency / IScopedDependency / ISingletonDependency
    /// 标记接口对应的生命周期注册服务：实现类优先按业务接口注册，无业务接口时按自身注册
    /// </summary>
    [RequiresUnreferencedCode("Scans assemblies and registers implementation types via reflection.")]
    public static IServiceCollection RegisterBusinessServices(this IServiceCollection services, params Assembly[] assemblies)
    {
        RegisterUniteServices(services, assemblies, typeof(ITransientDependency), ServiceLifetime.Transient);
        RegisterUniteServices(services, assemblies, typeof(IScopedDependency), ServiceLifetime.Scoped);
        RegisterUniteServices(services, assemblies, typeof(ISingletonDependency), ServiceLifetime.Singleton);
        return services;
    }

    [RequiresUnreferencedCode("Scans assemblies and registers implementation types via reflection.")]
    private static void RegisterUniteServices(IServiceCollection services, IEnumerable<Assembly> assemblies, Type markerType, ServiceLifetime lifetime)
    {
        // 与原实现一致：标记接口本身和 Microsoft./System. 开头的框架接口不作为服务类型
        var ignorePrefixes = new[] { "Microsoft.", "System." };
        var implementTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && t.GetInterfaces().Contains(markerType));

        foreach (var implementType in implementTypes)
        {
            var serviceTypes = implementType.GetInterfaces()
                .Where(x => x != typeof(ITransientDependency)
                            && x != typeof(IScopedDependency)
                            && x != typeof(ISingletonDependency))
                .Where(x => x.FullName is null ||
                            !ignorePrefixes.Any(p => x.FullName.StartsWith(p, StringComparison.Ordinal)))
                .ToList();

            if (serviceTypes.Count > 0)
            {
                foreach (var serviceType in serviceTypes)
                {
                    services.Add(new ServiceDescriptor(serviceType, implementType, lifetime));
                }
            }
            else
            {
                services.Add(new ServiceDescriptor(implementType, implementType, lifetime));
            }
        }
    }
}
