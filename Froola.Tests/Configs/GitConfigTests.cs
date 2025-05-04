using AutoFixture;
using Froola.Configs;

namespace Froola.Tests.Configs;

public class GitConfigTests
{
    private readonly Fixture _fixture = new();

    private void SetupValidGitConfigStrings(GitConfig config)
    {
        config.GitRepositoryUrl = "https://github.com/example/repo.git";
        config.GitBranch = "main";
        config.GitSshKeyPath = "C:/dummy/sshkey";
    }

    [Fact]
    public void PostConfigure_Throws_WhenGitRepositoryUrlIsNullOrEmpty()
    {
        var config = _fixture.Build<GitConfig>()
            .With(x => x.GitRepositoryUrl, string.Empty)
            .With(x => x.GitBranch, "main")
            .Create();


        Assert.Throws<ArgumentException>(() => config.Build());
    }

    [Fact]
    public void PostConfigure_Throws_WhenGitBranchIsNullOrEmpty()
    {
        var config = _fixture.Build<GitConfig>()
            .With(x => x.GitRepositoryUrl, "https://github.com/example/repo.git")
            .With(x => x.GitBranch, string.Empty)
            .Create();

        Assert.Throws<ArgumentException>(() => config.Build());
    }

    [Fact]
    public void PostConfigure_DoesNotThrow_WhenAllRequiredPropertiesAreSet()
    {
        var config = _fixture.Build<GitConfig>()
            .With(x => x.GitRepositoryUrl, "https://github.com/example/repo.git")
            .With(x => x.GitBranch, "main")
            .With(x => x.GitSshKeyPath, "C:/dummy/sshkey")
            .Create();

        var exception = Record.Exception(() => config.Build());
        Assert.Null(exception);
    }

    [Fact]
    public void PostConfigure_DoesNotThrow_WhenGitSshKeyPathDoesNotExist()
    {
        var config = _fixture.Build<GitConfig>()
            .With(x => x.GitRepositoryUrl, "https://github.com/example/repo.git")
            .With(x => x.GitBranch, "main")
            .With(x => x.GitSshKeyPath, "C:/path/to/nonexistent/key")
            .Create();

        var exception = Record.Exception(() => config.Build());
        Assert.Null(exception);
    }
}