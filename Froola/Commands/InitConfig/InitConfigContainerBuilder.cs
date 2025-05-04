using Froola.Commands;
using Froola.Interfaces;
using Froola.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Froola.ContainerBuilders;

/// <summary>
///     Container builder for exporting config file
/// </summary>
public class InitConfigContainerBuilder : IContainerBuilder
{
    /// <summary>
    ///     Registers all required services for dependency injection.
    /// </summary>
    public void Register(IServiceCollection services)
    {
        // Logger
        services.AddSingleton<IFroolaLogger, FroolaLogger>();
        services.AddSingleton(typeof(IFroolaLogger<>), typeof(FroolaLogger<>));

        // Commands
        services.AddTransient<InitConfigCommand>();

        // IPostConfigureOptions
    }
}