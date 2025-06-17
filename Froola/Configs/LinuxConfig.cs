using System.Text.Json.Serialization;
using Froola.Configs.Attributes;
using Froola.Configs.Collections;
using Microsoft.Extensions.Options;

namespace Froola.Configs;

[OptionClass("Linux")]
public class LinuxConfig
{
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