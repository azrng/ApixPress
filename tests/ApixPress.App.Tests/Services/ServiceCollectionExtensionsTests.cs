using ApixPress.App.Helpers;
using Azrng.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace ApixPress.App.Tests.Services;

public interface ITransientSampleService;

public interface IMultiFirstSampleService;

public interface IMultiSecondSampleService;

public sealed class TransientSampleService : ITransientSampleService, ITransientDependency;

public sealed class ScopedSampleService : IScopedDependency;

public sealed class SingletonWithFrameworkInterfaceSampleService : IDisposable, ISingletonDependency
{
    public void Dispose()
    {
    }
}

public sealed class MultiInterfaceSampleService : IMultiFirstSampleService, IMultiSecondSampleService, ITransientDependency;

internal sealed class InternalSampleService : ITransientDependency;

public sealed class ServiceCollectionExtensionsTests
{
    private static IServiceCollection CreateServices()
        => new ServiceCollection().RegisterBusinessServices(typeof(ServiceCollectionExtensionsTests).Assembly);

    [Fact]
    public void RegisterBusinessServices_ShouldRegisterBusinessInterfaceWithTransientLifetime()
    {
        var services = CreateServices();

        var descriptor = services.Single(x => x.ServiceType == typeof(ITransientSampleService));

        Assert.Equal(typeof(TransientSampleService), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void RegisterBusinessServices_ShouldRegisterSelfWhenNoBusinessInterface()
    {
        var services = CreateServices();

        var descriptor = services.Single(x => x.ServiceType == typeof(ScopedSampleService));

        Assert.Equal(typeof(ScopedSampleService), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void RegisterBusinessServices_ShouldIgnoreFrameworkInterfacesAndRegisterSelf()
    {
        var services = CreateServices();

        var descriptor = services.Single(x => x.ServiceType == typeof(SingletonWithFrameworkInterfaceSampleService));

        Assert.Equal(typeof(SingletonWithFrameworkInterfaceSampleService), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IDisposable));
    }

    [Fact]
    public void RegisterBusinessServices_ShouldRegisterAllBusinessInterfaces()
    {
        var services = CreateServices();

        var first = services.Single(x => x.ServiceType == typeof(IMultiFirstSampleService));
        var second = services.Single(x => x.ServiceType == typeof(IMultiSecondSampleService));

        Assert.Equal(typeof(MultiInterfaceSampleService), first.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, first.Lifetime);
        Assert.Equal(typeof(MultiInterfaceSampleService), second.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, second.Lifetime);
    }

    [Fact]
    public void RegisterBusinessServices_ShouldRegisterNonPublicTypes()
    {
        var services = CreateServices();

        var descriptor = services.Single(x => x.ServiceType == typeof(InternalSampleService));

        Assert.Equal(typeof(InternalSampleService), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void RegisterBusinessServices_ShouldNotRegisterMarkerInterfacesAsServiceTypes()
    {
        var services = CreateServices();

        Assert.DoesNotContain(services, x => x.ServiceType == typeof(ITransientDependency));
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IScopedDependency));
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(ISingletonDependency));
    }
}
