using AutoFixture;
using Froola.Configs;
using Microsoft.Extensions.Options;

namespace Froola.Tests.Configs;

public class LinuxConfigTests
{
    private readonly Fixture _fixture = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PostConfigure_Throws_WhenDockerCommandIsNullOrEmpty(string? dockerCommand)
    {
        var config = _fixture.Build<LinuxConfig>()
            .With(x => x.DockerCommand, dockerCommand)
            .With(x => x.DockerImage, "ghcr.io/epicgames/unreal-engine:dev-slim-%v")
            .Create();
        var postConfigure = new LinuxConfigPostConfigure();
        Assert.Throws<OptionsValidationException>(() => postConfigure.PostConfigure(null, config));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PostConfigure_Throws_WhenDockerImageIsNullOrEmpty(string? dockerImage)
    {
        var config = _fixture.Build<LinuxConfig>()
            .With(x => x.DockerCommand, "docker")
            .With(x => x.DockerImage, dockerImage)
            .Create();
        var postConfigure = new LinuxConfigPostConfigure();
        Assert.Throws<OptionsValidationException>(() => postConfigure.PostConfigure(null, config));
    }

    [Fact]
    public void PostConfigure_DoesNotThrow_WhenDockerSettingsAreValid()
    {
        var config = _fixture.Build<LinuxConfig>()
            .With(x => x.DockerCommand, "docker")
            .With(x => x.DockerImage, "ghcr.io/epicgames/unreal-engine:dev-slim-%v")
            .Create();
        var postConfigure = new LinuxConfigPostConfigure();
        var exception = Record.Exception(() => postConfigure.PostConfigure(null, config));
        Assert.Null(exception);
    }
}
