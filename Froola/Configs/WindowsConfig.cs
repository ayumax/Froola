using System.IO;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Froola.Configs.Attributes;
using Froola.Configs.Collections;

namespace Froola.Configs;

[OptionClass("Windows")]
public class WindowsConfig
{
    /// <summary>
    ///     Base path to Unreal Engine on Windows
    /// </summary>
    public string WindowsUnrealBasePath { get; set; } = @"C:\Program Files\Epic Games";
    
    /// <summary>
    ///     Dictionary of destination paths per Unreal Engine version for copying packaged plugins (e.g. "5.5": "C:\\UE_5.5\\Engine\\Plugins")
    /// </summary>
    public OptionDictionary CopyPackageDestinationPaths { get; set; } = new();

    /// <summary>
    ///     Internal dictionary with parsed UE versions as keys
    /// </summary>
    [JsonIgnore] public OptionDictionary<UEVersion, string> CopyPackageDestinationPathsWithVersion { get; set; } = new();
}

public class WindowsConfigPostConfigure : IPostConfigureOptions<WindowsConfig>
{
    public void PostConfigure(string? name, WindowsConfig config)
    {
        var section = ConfigHelper.GetSection<WindowsConfig>();
        
        // WindowsUnrealBasePath
        if (string.IsNullOrWhiteSpace(config.WindowsUnrealBasePath))
        {
            throw new OptionsValidationException(section, typeof(WindowsConfig),
                [$"{nameof(WindowsConfig.WindowsUnrealBasePath)} must not be empty"]);
        }

        // CopyPackageDestinationPaths, CopyPackageDestinationPathsWithVersion
        config.CopyPackageDestinationPathsWithVersion.Clear();
        foreach (var (key, value) in config.CopyPackageDestinationPaths)
        {
            config.CopyPackageDestinationPathsWithVersion.Add(UEVersionExtensions.Parse(key), value);
        }
    }
}