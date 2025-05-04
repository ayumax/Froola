using AutoFixture;
using Froola.Configs;
using Microsoft.Extensions.Options;

namespace Froola.Tests.Configs;

public class WindowsConfigTests
{
    private readonly Fixture _fixture = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PostConfigure_Throws_WhenRequiredPropertyIsNullOrEmpty(string? unrealBasePath)
    {
        var config = _fixture.Build<WindowsConfig>()
            .With(x => x.WindowsUnrealBasePath, unrealBasePath)
            .Create();
        var postConfigure = new WindowsConfigPostConfigure();
        Assert.Throws<OptionsValidationException>(() => postConfigure.PostConfigure(null, config));
    }

    [Fact]
    public void PostConfigure_DoesNotThrow_WhenWindowsUnrealBasePathExists()
    {
        var config = _fixture.Build<WindowsConfig>()
            .With(x => x.WindowsUnrealBasePath, Directory.GetCurrentDirectory())
            .Create();
        var postConfigure = new WindowsConfigPostConfigure();
        var exception = Record.Exception(() => postConfigure.PostConfigure(null, config));
        Assert.Null(exception);
    }
}