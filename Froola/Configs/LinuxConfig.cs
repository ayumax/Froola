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

    /// <summary>
    ///     Dictionary of Docker plugin source paths per Unreal Engine version (e.g. "5.5": "C:\\Plugins\\UE5.5")
    /// </summary>
    public OptionDictionary DockerPluginsSourcePaths { get; set; } = new();

    /// <summary>
    ///     Internal dictionary with parsed UE versions as keys
    /// </summary>
    [JsonIgnore] public OptionDictionary<UEVersion, string> DockerPluginsSourcePathsWithVersion { get; set; } = new();

    /// <summary>
    ///     Dictionary of destination paths per Unreal Engine version for copying packaged plugins (e.g. "5.5": "C:\\TempPlugins\\UE5.5")
    /// </summary>
    public OptionDictionary CopyPackageDestinationPaths { get; set; } = new();

    /// <summary>
    ///     Internal dictionary with parsed UE versions as keys
    /// </summary>
    [JsonIgnore] public OptionDictionary<UEVersion, string> CopyPackageDestinationPathsWithVersion { get; set; } = new();
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

        // DockerPluginsSourcePaths, DockerPluginsSourcePathsWithVersion
        config.DockerPluginsSourcePathsWithVersion.Clear();
        foreach (var (key, value) in config.DockerPluginsSourcePaths)
        {
            config.DockerPluginsSourcePathsWithVersion.Add(UEVersionExtensions.Parse(key), value);
        }

        // CopyPackageDestinationPaths, CopyPackageDestinationPathsWithVersion
        config.CopyPackageDestinationPathsWithVersion.Clear();
        foreach (var (key, value) in config.CopyPackageDestinationPaths)
        {
            config.CopyPackageDestinationPathsWithVersion.Add(UEVersionExtensions.Parse(key), value);
        }
    }
}