using Microsoft.Extensions.DependencyInjection;

namespace Froola.Utils;

/// <summary>
/// Builder class for registering services and interfaces in the dependency injection container.
/// </summary>
public class ServiceRegistrationBuilderSingleton<TImpl>
    where TImpl : class
{
    private readonly IServiceCollection _services;

    /// <summary>
    /// Initializes a new instance and registers the implementation as a singleton.
    /// </summary>
    public ServiceRegistrationBuilderSingleton(IServiceCollection services)
    {
        _services = services;
        services.AddSingleton<TImpl>();
    }

    /// <summary>
    /// Registers the implementation as the specified interface.
    /// </summary>
    public ServiceRegistrationBuilderSingleton<TImpl> As<TInterface>() where TInterface : class
    {
        _services.AddSingleton(typeof(TInterface), typeof(TImpl));
        return this;
    }
}

public class ServiceRegistrationBuilderTransient<TImpl>
    where TImpl : class
{
    private readonly IServiceCollection _services;

    /// <summary>
    ///     Initializes a new instance and registers the implementation as a singleton.
    /// </summary>
    public ServiceRegistrationBuilderTransient(IServiceCollection services)
    {
        _services = services;
        services.AddSingleton<TImpl>();
    }

    /// <summary>
    ///     Registers the implementation as the specified interface.
    /// </summary>
    public ServiceRegistrationBuilderTransient<TImpl> As<TInterface>() where TInterface : class
    {
        _services.AddTransient(typeof(TInterface), typeof(TImpl));
        return this;
    }
}

/// <summary>
/// Extension methods for IServiceCollection to simplify service registration.
/// </summary>
public static class IServiceCollectionExtension
{
    /// <summary>
    /// Adds a service and returns a builder for further configuration.
    /// </summary>
    public static ServiceRegistrationBuilderSingleton<TImpl> AddSingletonService<TImpl>(
        this IServiceCollection services)
        where TImpl : class
    {
        return new ServiceRegistrationBuilderSingleton<TImpl>(services);
    }

    public static ServiceRegistrationBuilderTransient<TImpl> AddTransientService<TImpl>(
        this IServiceCollection services)
        where TImpl : class
    {
        return new ServiceRegistrationBuilderTransient<TImpl>(services);
    }
}