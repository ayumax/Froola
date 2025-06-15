using System.Text.Json.Serialization;
using Froola.Configs.Attributes;
using Froola.Configs.Collections;
using Microsoft.Extensions.Options;

namespace Froola.Configs;

[OptionClass("Mac")]
public class MacConfig
{
    /// <summary>
    ///     Base path to Unreal Engine on Mac
    /// </summary>
    public string MacUnrealBasePath { get; set; } = "/Users/Shared/Epic Games";

    /// <summary>
    ///     SSH username for Mac operations
    /// </summary>
    public string SshUser { get; set; } = string.Empty;

    /// <summary>
    ///     SSH password for Mac operations
    /// </summary>
    public string SshPassword { get; set; } = string.Empty;

    /// <summary>
    ///     Path to SSH private key for Mac operations
    /// </summary>
    public string SshPrivateKeyPath { get; set; } = string.Empty;

    /// <summary>
    ///     SSH host address for Mac operations
    /// </summary>
    public string SshHost { get; set; } = string.Empty;

    /// <summary>
    ///     SSH port for Mac operations
    /// </summary>
    public int SshPort { get; set; } = 22;

    /// <summary>
    ///     Dictionary of Xcode file paths per Unreal Engine version (e.g. "/Applications/Xcode.app")
    /// </summary>
    public OptionDictionary XcodeNames { get; set; } = new()
    {
    };

    [JsonIgnore] public OptionDictionary<UEVersion, string> XcodeNamesWithVersion { get; set; } = new();

    /// <summary>
    ///     Dictionary of destination paths per Unreal Engine version for copying packaged plugins (e.g. "5.5": "/Users/Shared/Epic Games/UE_5.5/Engine/Plugins")
    /// </summary>
    public OptionDictionary CopyPackageDestinationPaths { get; set; } = new();

    /// <summary>
    ///     Internal dictionary with parsed UE versions as keys
    /// </summary>
    [JsonIgnore] public OptionDictionary<UEVersion, string> CopyPackageDestinationPathsWithVersion { get; set; } = new();
}

public class MacConfigPostConfigure : IPostConfigureOptions<MacConfig>
{
    public void PostConfigure(string? name, MacConfig config)
    {
        // XcodeNames
        config.XcodeNamesWithVersion.Clear();
        foreach (var (key, value) in config.XcodeNames)
        {
            config.XcodeNamesWithVersion.Add(UEVersionExtensions.Parse(key), value);
        }

        // CopyPackageDestinationPaths, CopyPackageDestinationPathsWithVersion
        config.CopyPackageDestinationPathsWithVersion.Clear();
        foreach (var (key, value) in config.CopyPackageDestinationPaths)
        {
            config.CopyPackageDestinationPathsWithVersion.Add(UEVersionExtensions.Parse(key), value);
        }
    }
}