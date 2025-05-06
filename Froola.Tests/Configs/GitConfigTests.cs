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
        config.LocalRepositoryPath = Directory.GetCurrentDirectory();
    }

    [Fact]
    public void PostConfigure_Throws_WhenGitRepositoryUrlIsNullOrEmpty()
    {
        var config = _fixture.Build<GitConfig>()
            .With(x => x.GitRepositoryUrl, string.Empty)
            .With(x => x.GitBranch, "main")
            .With(x => x.LocalRepositoryPath, string.Empty)
            .Create();


        Assert.Throws<ArgumentException>(() => config.Build());
    }

    [Fact]
    public void PostConfigure_Throws_WhenGitBranchIsNullOrEmpty()
    {
        var config = _fixture.Build<GitConfig>()
            .With(x => x.GitRepositoryUrl, "https://github.com/example/repo.git")
            .With(x => x.GitBranch, string.Empty)
            .With(x => x.LocalRepositoryPath, string.Empty)
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
            .With(x => x.LocalRepositoryPath, Directory.GetCurrentDirectory())
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
            .With(x => x.LocalRepositoryPath, Directory.GetCurrentDirectory())
            .Create();

        var exception = Record.Exception(() => config.Build());
        Assert.Null(exception);
    }

    [Fact]
    // Should throw ArgumentException if LocalRepositoryPath is set but does not exist
    public void Build_Throws_WhenLocalRepositoryPathDoesNotExist()
    {
        var config = _fixture.Build<GitConfig>()
            .With(x => x.LocalRepositoryPath, "C:/path/to/nonexistent/repo")
            .Create();

        Assert.Throws<ArgumentException>(() => config.Build());
    }

    [Fact]
    // Should NOT throw if LocalRepositoryPath is set and exists
    public void Build_DoesNotThrow_WhenLocalRepositoryPathExists()
    {
        var tempDir = Directory.GetCurrentDirectory();

        var config = _fixture.Build<GitConfig>()
            .With(x => x.LocalRepositoryPath, tempDir)
            .Create();

        var exception = Record.Exception(() => config.Build());
        Assert.Null(exception);
    }

    [Fact]
    // Should NOT throw even if GitRepositoryUrl and GitBranch are empty when LocalRepositoryPath is set and exists
    public void Build_DoesNotThrow_WhenLocalRepositoryPathExists_AndOtherFieldsAreEmpty()
    {
        var tempDir = Directory.GetCurrentDirectory();

        var config = _fixture.Build<GitConfig>()
            .With(x => x.LocalRepositoryPath, tempDir)
            .With(x => x.GitRepositoryUrl, string.Empty)
            .With(x => x.GitBranch, string.Empty)
            .Create();

        var exception = Record.Exception(() => config.Build());
        Assert.Null(exception);
    }
}