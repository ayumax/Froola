using Froola;
using Froola.Configs;
using Froola.Utils;

namespace Froola.Tests.Utils;

public class UECommandsHelperTests
{
    [Fact]
    public void GetUeDirectoryPath_ReturnsRemoteLinuxPath_WhenBuilderModeIsRemote()
    {
        var windowsConfig = new WindowsConfig { WindowsUnrealBasePath = @"C:\UE" };
        var macConfig = new MacConfig { MacUnrealBasePath = "/Users/Shared/Epic Games" };
        var linuxConfig = new LinuxConfig
        {
            BuilderMode = LinuxBuilderMode.Remote,
            LinuxUnrealBasePath = "/opt/UnrealEngine"
        };

        var result = UECommandsHelper.GetUeDirectoryPath(windowsConfig, macConfig, linuxConfig, UEVersion.UE_5_5,
            EditorPlatform.Linux);

        Assert.Equal("/opt/UnrealEngine/UE_5.5", result);
    }

    [Fact]
    public void GetUeDirectoryPath_ReturnsDockerLinuxPath_WhenBuilderModeIsDocker()
    {
        var windowsConfig = new WindowsConfig { WindowsUnrealBasePath = @"C:\UE" };
        var macConfig = new MacConfig { MacUnrealBasePath = "/Users/Shared/Epic Games" };
        var linuxConfig = new LinuxConfig
        {
            BuilderMode = LinuxBuilderMode.Docker,
            LinuxUnrealBasePath = "/opt/UnrealEngine"
        };

        var result = UECommandsHelper.GetUeDirectoryPath(windowsConfig, macConfig, linuxConfig, UEVersion.UE_5_5,
            EditorPlatform.Linux);

        Assert.Equal("/home/ue4/UnrealEngine", result);
    }
}
