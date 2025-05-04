using System.Runtime.CompilerServices;
using Cysharp.Diagnostics;
using Froola.Interfaces;
using Froola.Utils;
using Moq;
using Xunit.Abstractions;

namespace Froola.Tests;

public class GitClientTests
{
    private readonly IFroolaLogger<GitClient> _logger;
    private readonly IProcessRunner _processRunner;

    public GitClientTests(ITestOutputHelper output)
    {
        _logger = new FroolaLogger<GitClient>(new TestFroolaLogger(output));
        _processRunner = CreateNormalProcessRunner().Object;
    }

    [Theory]
    [InlineData("https://github.com/user/repo.git", "git@github.com:user/repo.git")]
    [InlineData("https://github.com/user/repo", "git@github.com:user/repo.git")]
    [InlineData("git@github.com:user/repo.git", "git@github.com:user/repo.git")]
    [InlineData("", "")]
    public void ConvertHttpsToSshUrl_ExpectedBehavior(string input, string expected)
    {
        var client = new GitClient(_logger, _processRunner);
        var actual = client.ConvertHttpsToSshUrl(input);
        Assert.Equal(expected, actual);
    }


    [Fact]
    public async Task CloneRepository_WithoutSshKey_Success()
    {
        var client = new GitClient(_logger, _processRunner)
        {
            SshKeyPath = string.Empty
        };
        var result = await client.CloneRepository("https://github.com/user/repo.git", "main", Path.GetTempPath());
        Assert.True(result);
    }

    [Fact]
    public async Task CloneRepository_WithSshKey_Success()
    {
        var client = new GitClient(_logger, _processRunner);
        var tempKey = Path.GetTempFileName();
        client.SshKeyPath = tempKey;
        var result = await client.CloneRepository("https://github.com/user/repo.git", "main", Path.GetTempPath());
        Assert.True(result);
        File.Delete(tempKey);
    }

    [Fact]
    public async Task CloneRepository_ReturnsFalseOnException()
    {
        var errorRunner = CreateErrorProcessRunner().Object;
        var client = new GitClient(_logger, errorRunner);
        var result = await client.CloneRepository("https://github.com/user/repo.git", "main", Path.GetTempPath());
        Assert.False(result);
    }

    private Mock<IProcessRunner> CreateNormalProcessRunner()
    {
        var mock = new Mock<IProcessRunner>();
        // Setup for 4-argument version with any env
        mock.Setup(r => r.RunAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IDictionary<string, string>>()
        )).Returns((string a, string b, string c, IDictionary<string, string> d) => DummyAsyncEnumerable("dummy output"));
        // Setup for 4-argument version with null env
        mock.Setup(r => r.RunAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            null
        )).Returns((string a, string b, string c, IDictionary<string, string> d) => DummyAsyncEnumerable("dummy output"));

        return mock;
    }

    private Mock<IProcessRunner> CreateErrorProcessRunner()
    {
        var mock = new Mock<IProcessRunner>();
        mock.Setup(r => r.RunAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IDictionary<string, string>>()
        )).Returns((string a, string b, string c, IDictionary<string, string> d) => ThrowingAsyncEnumerable());
        mock.Setup(r => r.RunAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            null
        )).Returns((string a, string b, string c, IDictionary<string, string> d) => ThrowingAsyncEnumerable());

        return mock;
    }

    private async IAsyncEnumerable<string> DummyAsyncEnumerable(string value,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 必ず値を返す
        yield return !string.IsNullOrEmpty(value) ? value : "dummy output";
        await Task.CompletedTask;
    }

    private async IAsyncEnumerable<string> ThrowingAsyncEnumerable(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return string.Empty;
        await Task.CompletedTask;
        throw new ProcessErrorException(1, ["dummy error"]);
    }
}