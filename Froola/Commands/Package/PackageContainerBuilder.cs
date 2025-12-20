using Froola.Commands.Package.Builder;
using Froola.Commands.Plugin.Builder;
using Froola.Interfaces;
using Froola.Runners;
using Froola.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Froola.Commands.Package;

/// <summary>
/// Container builder for registering dependencies in the PackageCommand.
/// </summary>
public class PackageContainerBuilder : IContainerBuilder
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
        services.AddSingleton<IGitClient, GitClient>();
        services.AddSingleton<ISshConnection, SshConnection>();
        services.AddSingleton<IConfigJsonExporter, ConfigJsonExporter>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        
        // Builders
        services.AddTransientService<Package.Builder.WindowsBuilder>().As<IBuilder>().As<IWindowsBuilder>();
        services.AddTransientService<Package.Builder.MacBuilder>().As<IBuilder>().As<IMacBuilder>();
        services.AddTransientService<Package.Builder.LinuxBuilder>().As<IBuilder>().As<ILinuxBuilder>();
        services.AddTransient<ITestResultsEvaluator, TestResultsEvaluator>();

        // Logger
        services.AddSingleton<IFroolaLogger, FroolaLogger>();
        services.AddSingleton(typeof(IFroolaLogger<>), typeof(FroolaLogger<>));
    }
}
