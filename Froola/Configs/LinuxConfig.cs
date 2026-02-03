using System.Text.Json.Serialization;
using Froola.Configs.Attributes;
using Froola.Configs.Collections;
using Microsoft.Extensions.Options;

namespace Froola.Configs;

[OptionClass("Linux")]
public class LinuxConfig
{
    /// <summary>
    ///     Linux builder mode (Docker or Remote).
    /// </summary>
    public LinuxBuilderMode BuilderMode { get; set; } = LinuxBuilderMode.Docker;

    /// <summary>
    ///     Command for docker
    /// </summary>
    public string DockerCommand { get; set; } = "docker";

    /// <summary>
    ///     Name of UE Docker image
    /// </summary>
    public string DockerImage { get; set; } = "ghcr.io/epicgames/unreal-engine:dev-slim-%v";

    /// <summary>
    ///     Whether to copy custom plugins to Docker UE installation.
    /// </summary>
    public bool CopyPluginsToDocker { get; set; } = false;

    /// <summary>
    ///     Path to the directory containing custom plugins to copy to Docker UE installation.
    ///     Used when DockerPluginsSourcePaths is not specified or version is not found.
    /// </summary>
    public string DockerPluginsSourcePath { get; set; } = string.Empty;

    /// <summary>
    ///     Base path to Unreal Engine on Linux (Remote mode).
    /// </summary>
    public string LinuxUnrealBasePath { get; set; } = "/opt/UnrealEngine";

    /// <summary>
    ///     SSH username for Linux operations.
    /// </summary>
    public string SshUser { get; set; } = string.Empty;

    /// <summary>
    ///     SSH password for Linux operations.
    /// </summary>
    public string SshPassword { get; set; } = string.Empty;

    /// <summary>
    ///     Path to SSH private key for Linux operations.
    /// </summary>
    public string SshPrivateKeyPath { get; set; } = string.Empty;

    /// <summary>
    ///     SSH host address for Linux operations.
    /// </summary>
    public string SshHost { get; set; } = string.Empty;

    /// <summary>
    ///     SSH port for Linux operations.
    /// </summary>
    public int SshPort { get; set; } = 22;
}

public class LinuxConfigPostConfigure : IPostConfigureOptions<LinuxConfig>
{
    public void PostConfigure(string? name, LinuxConfig config)
    {
        var section = ConfigHelper.GetSection<LinuxConfig>();

        // DockerCommand
        if (string.IsNullOrWhiteSpace(config.DockerCommand))
        {
            throw new OptionsValidationException(section, typeof(LinuxConfig),
                [$"{nameof(LinuxConfig.DockerCommand)} must not be empty"]);
        }

        // DockerImage
        if (string.IsNullOrWhiteSpace(config.DockerImage))
        {
            throw new OptionsValidationException(section, typeof(LinuxConfig),
                [$"{nameof(LinuxConfig.DockerImage)} must not be empty"]);
        }
    }
}
