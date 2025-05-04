using System.IO;
using Microsoft.Extensions.Options;
using Froola.Configs.Attributes;

namespace Froola.Configs;

[OptionClass("Windows")]
public class WindowsConfig
{
    /// <summary>
    ///     Base path to Unreal Engine on Windows
    /// </summary>
    public string WindowsUnrealBasePath { get; set; } = @"C:\Program Files\Epic Games";
}

public class WindowsConfigPostConfigure : IPostConfigureOptions<WindowsConfig>
{
    public void PostConfigure(string? name, WindowsConfig config)
    {
        var section = ConfigHelper.GetSection<WindowsConfig>();
        
        // WindowsUnrealBasePath
        if (!Directory.Exists(config.WindowsUnrealBasePath))
        {
            throw new OptionsValidationException(section, typeof(WindowsConfig),
                [$"{nameof(WindowsConfig.WindowsUnrealBasePath)} path does not exist"]);
        }
    }
}