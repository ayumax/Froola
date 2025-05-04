using AutoFixture;
using Froola.Configs;
using Microsoft.Extensions.Options;

namespace Froola.Tests.Configs;

public class WindowsConfigTests
{
    private readonly Fixture _fixture = new();

    private void SetupValidWindowsConfigStrings(WindowsConfig config)
    {
        // Add valid values if WindowsConfig has string properties with restrictions
    }

    [Fact]
    public void PostConfigure_Throws_WhenRequiredPropertyIsNullOrEmpty()
    {
        var config = _fixture.Build<WindowsConfig>()
            .Create();
        SetupValidWindowsConfigStrings(config);
        var postConfigure = new WindowsConfigPostConfigure();
        Assert.Throws<OptionsValidationException>(() => postConfigure.PostConfigure(null, config));
    }

    [Fact]
    public void PostConfigure_Throws_WhenWindowsUnrealBasePathDoesNotExist()
    {
        var config = _fixture.Build<WindowsConfig>()
            .With(x => x.WindowsUnrealBasePath, "Z:/this/path/does/not/exist")
            .Create();
        var postConfigure = new WindowsConfigPostConfigure();
        Assert.Throws<OptionsValidationException>(() => postConfigure.PostConfigure(null, config));
    }

    [Fact]
    public void PostConfigure_DoesNotThrow_WhenWindowsUnrealBasePathExists()
    {
        var existingPath = Directory.Exists("C:/") ? "C:/" : "C:\\";
        var config = _fixture.Build<WindowsConfig>()
            .With(x => x.WindowsUnrealBasePath, existingPath)
            .Create();
        var postConfigure = new WindowsConfigPostConfigure();
        var exception = Record.Exception(() => postConfigure.PostConfigure(null, config));
        Assert.Null(exception);
    }
}