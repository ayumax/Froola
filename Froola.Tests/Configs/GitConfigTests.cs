using AutoFixture;
using Froola.Configs;
using Froola.Configs.Collections;

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
        config.GitBranches = new OptionDictionary { { "5.3", "UE5.3" }, { "5.2", "UE5.2" } };
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
    public void PostConfigure_Throws_WhenGitBranchIsNullOrEmptyAndNoGitBranches()
    {
        var config = _fixture.Build<GitConfig>()
            .With(x => x.GitRepositoryUrl, "https://github.com/example/repo.git")
            .With(x => x.GitBranch, string.Empty)
            .With(x => x.LocalRepositoryPath, string.Empty)
            .With(x => x.GitBranches, new OptionDictionary()) // Empty GitBranches
            .Create();

        Assert.Throws<ArgumentException>(() => config.Build());
    }

    [Fact]
    public void PostConfigure_DoesNotThrow_WhenGitBranchIsEmptyButGitBranchesIsNotEmpty()
    {
        var config = _fixture.Build<GitConfig>()
            .With(x => x.GitRepositoryUrl, "https://github.com/example/repo.git")
            .With(x => x.GitBranch, string.Empty)
            .With(x => x.LocalRepositoryPath, string.Empty)
            .With(x => x.GitBranches, new OptionDictionary { { "5.3", "UE5.3" } })
            .Create();

        var result = config.Build();
        Assert.NotNull(result);
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

        var result = config.Build();
        Assert.NotNull(result);
    }

    [Fact]
    public void GitBranches_IsInitialized_WhenNewInstanceCreated()
    {
        // Arrange
        var config = new GitConfig();

        // Assert
        Assert.NotNull(config.GitBranches);
        Assert.Empty(config.GitBranches);
    }

    [Fact]
    public void GitBranchesWithVersion_IsInitialized_WhenNewInstanceCreated()
    {
        // Arrange
        var config = new GitConfig();

        // Assert
        Assert.NotNull(config.GitBranchesWithVersion);
        Assert.Empty(config.GitBranchesWithVersion);
    }

    [Fact]
    public void Build_ReturnsNonNull_WhenGitBranchesIsSet()
    {
        // Arrange
        var config = new GitConfig
        {
            GitRepositoryUrl = "https://github.com/example/repo.git",
            GitBranch = "main",
            GitBranches = new OptionDictionary { { "5.3", "UE5.3" }, { "5.2", "UE5.2" } }
        };

        // Act
        var result = config.Build();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.GitBranches.Count);
        Assert.Equal("UE5.3", result.GitBranches["5.3"]);
        Assert.Equal("UE5.2", result.GitBranches["5.2"]);
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