using Microsoft.Extensions.DependencyInjection;

namespace Froola.Interfaces;

/// <summary>
/// Interface for registering services in a dependency injection container.
/// </summary>
public interface IContainerBuilder
{
    /// <summary>
    /// Registers services to the provided service collection.
    /// </summary>
    /// <param name="services">The service collection to register services to.</param>
    void Register(IServiceCollection services);
}