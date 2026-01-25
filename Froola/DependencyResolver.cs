using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Froola.Configs;
using Froola.Interfaces;
using Froola.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Froola;

/// <summary>
/// IoC (Inversion of Control) container for dependency injection.
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "StaticMemberInGenericType")]
public sealed class DependencyResolver : IDisposable
{
    /// <summary>
    /// Host
    /// </summary>
    private IHost? _host;
    

    /// <summary>
    /// Resolves a registered service of type T.
    /// </summary>
    public T Resolve<T>() where T : notnull
    {
        if (_host is null)
        {
            throw new InvalidOperationException($"{nameof(DependencyResolver)} is not initialized.");
        }

        return _host.Services.GetRequiredService<T>();
    }

    public object Resolve(Type instanceType) 
    {
        if (_host is null)
        {
            throw new InvalidOperationException($"{nameof(DependencyResolver)} is not initialized.");
        }

        return _host.Services.GetRequiredService(instanceType);
    }

    /// <summary>
    ///     Build and return a configured IHost (DI, Configuration, Logging unified)
    /// </summary>
    public IHost BuildHostWithContainerBuilder<TContainerBuilder>(string logSavePath, object[] additionalObjects)
        where TContainerBuilder : IContainerBuilder
    {
        return BuildHostWithContainerBuilder(typeof(TContainerBuilder), logSavePath, additionalObjects);
    }

    public IHost BuildHostWithContainerBuilder(Type containerBuilderType, string logSavePath,
        object[] additionalObjects)
    {
        if (Activator.CreateInstance(containerBuilderType) is IContainerBuilder containerBuilder)
        {
            return BuildHostWithContainerBuilder(containerBuilder, logSavePath, additionalObjects); 
        }

        throw new ArgumentException($"Type {containerBuilderType.Name} does not implement {nameof(IContainerBuilder)}");
    }

    public IHost BuildHostWithContainerBuilder(IContainerBuilder containerBuilder, string logSavePath,
        object[] additionalObjects)
    {
        Dispose();

        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddSingleton(this);

        containerBuilder.Register(builder.Services);

        foreach (var objectToRegister in additionalObjects)
        {
            builder.Services.AddSingleton(objectToRegister.GetType(), objectToRegister);
        }

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        _host = builder.Build();

        var baseLogger = Resolve<IFroolaLogger>();
        baseLogger.SetSaveDirectory(logSavePath);

        return _host;
    }

    public IHost BuildHost(Type[] configTypes)
    {
        Dispose();

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"), true, false);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton(this);

                foreach (var configType in configTypes)
                {
                    services.ConfigureConfig(configType, context.Configuration);

                    // Register the IPostConfigureOptions<T>
                    services.AddPostConfigureIfExists(configType);
                }
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .Build();

        return _host;
    }

    public void Dispose()
    {
        _host?.Dispose();
        _host = null;
    }

    public void OutputAllConfigValues()
    {
        foreach (var configType in ConfigHelper.GetAllConfigTypes())
        {
            var optionConfigValue = Resolve(ConfigHelper.GetIOptionsType(configType));
            var configValue = optionConfigValue.GetType().GetProperty("Value")?.GetValue(optionConfigValue);
            var logger = Resolve<ILogger<DependencyResolver>>();

            logger.LogInformation("----------------------------------------------");
            if (configValue is null)
            {
                continue;
            }

            logger.LogInformation($"[{configValue.GetType().Name} values from appsettings.json]");

            foreach (var property in configType.GetProperties())
            {
                logger.LogInformation($"{property.Name}: {property.GetValue(configValue)}");
            }

            logger.LogInformation("----------------------------------------------");
        }
    }
}
