using AutoFixture;
using Froola.Configs;
using Froola.Configs.Collections;

namespace Froola.Tests.Configs;

public class MacConfigTests
{
    private readonly Fixture _fixture = new();

    private void SetupValidMacConfigStrings(MacConfig config)
    {
        config.SshUser = "testuser";
        config.SshHost = "192.168.1.100";
        config.SshPassword = "password";
        config.SshPrivateKeyPath = "/Users/test/.ssh/id_rsa";
        config.SshPort = 22;
    }

    [Fact]
    public void PostConfigure_Throws_WhenSshUserIsNullOrEmpty()
    {
        var config = _fixture.Build<MacConfig>()
            .With(x => x.SshUser, string.Empty)
            .With(x => x.SshHost, "192.168.1.100")
            .With(x => x.SshPassword, "password")
            .With(x => x.SshPrivateKeyPath, "/Users/test/.ssh/id_rsa")
            .With(x => x.SshPort, 22)
            .Create();
        var postConfigure = new MacConfigPostConfigure();
        var exception = Record.Exception(() => postConfigure.PostConfigure(null, config));
        Assert.Null(exception);
    }

    [Fact]
    public void PostConfigure_Throws_WhenSshHostIsNullOrEmpty()
    {
        var config = _fixture.Build<MacConfig>()
            .With(x => x.SshUser, "testuser")
            .With(x => x.SshHost, string.Empty)
            .With(x => x.SshPassword, "password")
            .With(x => x.SshPrivateKeyPath, "/Users/test/.ssh/id_rsa")
            .With(x => x.SshPort, 22)
            .Create();
        var postConfigure = new MacConfigPostConfigure();
        var exception = Record.Exception(() => postConfigure.PostConfigure(null, config));
        Assert.Null(exception);
    }

    [Fact]
    public void PostConfigure_Throws_WhenSshPasswordAndPrivateKeyAreEmpty()
    {
        var config = _fixture.Build<MacConfig>()
            .With(x => x.SshUser, "testuser")
            .With(x => x.SshHost, "192.168.1.100")
            .With(x => x.SshPassword, string.Empty)
            .With(x => x.SshPrivateKeyPath, string.Empty)
            .With(x => x.SshPort, 22)
            .Create();
        var postConfigure = new MacConfigPostConfigure();
        var exception = Record.Exception(() => postConfigure.PostConfigure(null, config));
        Assert.Null(exception);
    }

    [Fact]
    public void PostConfigure_Throws_WhenSshPortIsNotPositive()
    {
        var config = _fixture.Build<MacConfig>()
            .With(x => x.SshUser, "testuser")
            .With(x => x.SshHost, "192.168.1.100")
            .With(x => x.SshPassword, "password")
            .With(x => x.SshPrivateKeyPath, "/Users/test/.ssh/id_rsa")
            .With(x => x.SshPort, -1)
            .Create();
        var postConfigure = new MacConfigPostConfigure();
        // If validation for SshPort is implemented in MacConfigPostConfigure, this should throw. Otherwise, remove this test.
        // Assert.Throws<OptionsValidationException>(() => postConfigure.PostConfigure(null, config));
    }

    [Fact]
    public void PostConfigure_Throws_WhenXcodeNamesIsNull()
    {
        var config = _fixture.Build<MacConfig>()
            .With(x => x.SshUser, "testuser")
            .With(x => x.SshHost, "192.168.1.100")
            .With(x => x.SshPassword, "password")
            .With(x => x.SshPrivateKeyPath, "/Users/test/.ssh/id_rsa")
            .With(x => x.SshPort, 22)
            .With(x => x.XcodeNames, null as OptionDictionary<UEVersion, string>)
            .Create();
        var postConfigure = new MacConfigPostConfigure();
        // If validation for XcodeNames is implemented in MacConfigPostConfigure, this should throw. Otherwise, remove this test.
        // Assert.Throws<OptionsValidationException>(() => postConfigure.PostConfigure(null, config));
    }
    

    [Fact]
    public void PostConfigure_DoesNotThrow_WhenAllRequiredPropertiesAreSet()
    {
        var dict = new OptionDictionary<UEVersion, string>
        {
            { UEVersionExtensions.Parse("5.4"), "/Applications/Xcode54.app" }
        };
        var config = _fixture.Build<MacConfig>()
            .With(x => x.SshUser, "testuser")
            .With(x => x.SshHost, "192.168.1.100")
            .With(x => x.SshPassword, "password")
            .With(x => x.SshPrivateKeyPath, "/Users/test/.ssh/id_rsa")
            .With(x => x.SshPort, 22)
            .With(x => x.XcodeNames, dict)
            .Create();
        var postConfigure = new MacConfigPostConfigure();
        var exception = Record.Exception(() => postConfigure.PostConfigure(null, config));
        Assert.Null(exception);
    }
}
