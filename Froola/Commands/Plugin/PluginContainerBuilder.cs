using Froola.Commands.Plugin.Builder;
using Froola.Interfaces;
using Froola.Runners;
using Froola.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Froola.Commands.Plugin;

/// <summary>
/// Container builder for registering dependencies in the PluginBuilder project.
/// </summary>
public class PluginContainerBuilder : IContainerBuilder
{
    /// <summary>
    /// Registers all required services for dependency injection.
    /// </summary>
    public void Register(IServiceCollection services)
    {
        // Utilities
        services.AddSingleton<IUnrealEngineRunner, WindowsUnrealEngineRunner>();
        services.AddSingleton<IFileSystem, SystemFileSystem>();
        services.AddSingleton<IDockerRunner, DockerRunner>();
        services.AddSingleton<IMacUnrealEngineRunner, MacUnrealEngineRunner>();
        services.AddSingleton<ILinuxUnrealEngineRunner, LinuxUnrealEngineRunner>();
        services.AddSingleton<IGitClient, GitClient>();
        services.AddSingleton<ISshConnection, SshConnection>();
        services.AddSingleton<ILinuxSshConnection, LinuxSshConnection>();
        services.AddSingleton<IConfigJsonExporter, ConfigJsonExporter>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        
        // PluginCommands
        services.AddTransientService<WindowsBuilder>().As<IBuilder>().As<IWindowsBuilder>();
        services.AddTransientService<MacBuilder>().As<IBuilder>().As<IMacBuilder>();
        services.AddTransientService<LinuxBuilder>().As<IBuilder>().As<ILinuxBuilder>();
        services.AddTransientService<LinuxRemoteBuilder>().As<IBuilder>().As<ILinuxRemoteBuilder>();
        services.AddTransient<ITestResultsEvaluator, TestResultsEvaluator>();

        // Logger
        services.AddSingleton<IFroolaLogger, FroolaLogger>();
        services.AddSingleton(typeof(IFroolaLogger<>), typeof(FroolaLogger<>));
    }
}
