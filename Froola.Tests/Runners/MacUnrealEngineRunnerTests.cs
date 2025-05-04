using Froola.Interfaces;
using Froola.Runners;
using Moq;

namespace Froola.Tests.Runners;

public class MacUnrealEngineRunnerTests
{
    [Fact]
    public async Task MakeDirectory_CallsEnsureDirectoryExistsAndReturnsResult()
    {
        var sshMock = new Mock<ISshConnection>();
        sshMock.Setup(s => s.EnsureDirectoryExists("/remote/path")).ReturnsAsync(true);
        var runner = new MacUnrealEngineRunner(sshMock.Object);
        var result = await runner.MakeDirectory("/remote/path");
        Assert.True(result);
        sshMock.Verify(s => s.EnsureDirectoryExists("/remote/path"), Times.Once);
    }

    [Fact]
    public async Task DirectoryExists_ReturnsTrueWhenExists()
    {
        var sshMock = new Mock<ISshConnection>();
        sshMock.Setup(s => s.SendCommand(It.IsAny<string>())).ReturnsAsync("exists");
        var runner = new MacUnrealEngineRunner(sshMock.Object);
        var result = await runner.DirectoryExists("/remote/path");
        Assert.True(result);
        sshMock.Verify(s => s.SendCommand("test -d '/remote/path' && echo exists || echo notfound"), Times.Once);
    }

    [Fact]
    public async Task DirectoryExists_ReturnsFalseWhenNotFound()
    {
        var sshMock = new Mock<ISshConnection>();
        sshMock.Setup(s => s.SendCommand(It.IsAny<string>())).ReturnsAsync("notfound");
        var runner = new MacUnrealEngineRunner(sshMock.Object);
        var result = await runner.DirectoryExists("/remote/path");
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteDirectory_ReturnsTrueOnSuccess()
    {
        var sshMock = new Mock<ISshConnection>();
        sshMock.Setup(s => s.DeleteDirectory("/remote/path")).Returns(Task.FromResult(true));
        var runner = new MacUnrealEngineRunner(sshMock.Object);
        var result = await runner.DeleteDirectory("/remote/path");
        Assert.True(result);
        sshMock.Verify(s => s.DeleteDirectory("/remote/path"), Times.Once);
    }

    [Fact]
    public async Task DeleteDirectory_ReturnsFalseOnException()
    {
        var sshMock = new Mock<ISshConnection>();
        sshMock.Setup(s => s.DeleteDirectory(It.IsAny<string>())).Returns(Task.FromResult(false));
        var runner = new MacUnrealEngineRunner(sshMock.Object);
        var result = await runner.DeleteDirectory("/remote/path");
        Assert.False(result);
    }

    [Fact]
    public async Task GetCurrentXcodePath_RemovesSuffixIfPresent()
    {
        var sshMock = new Mock<ISshConnection>();
        sshMock.Setup(s => s.SendCommand("xcode-select -p"))
            .ReturnsAsync("/Applications/Xcode.app/Contents/Developer\n");
        var runner = new MacUnrealEngineRunner(sshMock.Object);
        var result = await runner.GetCurrentXcodePath();
        Assert.Equal("/Applications/Xcode.app", result);
    }

    [Fact]
    public async Task GetCurrentXcodePath_ReturnsFullPathIfNoSuffix()
    {
        var sshMock = new Mock<ISshConnection>();
        sshMock.Setup(s => s.SendCommand("xcode-select -p")).ReturnsAsync("/Other/XcodePath\n");
        var runner = new MacUnrealEngineRunner(sshMock.Object);
        var result = await runner.GetCurrentXcodePath();
        Assert.Equal("/Other/XcodePath", result);
    }

    [Fact]
    public async Task SwitchXcode_CallsSendCommandAndReturnsOriginalPath()
    {
        var sshMock = new Mock<ISshConnection>();
        sshMock.SetupSequence(s => s.SendCommand(It.IsAny<string>()))
            .ReturnsAsync("/Applications/Xcode.app/Contents/Developer\n") // For GetCurrentXcodePath
            .ReturnsAsync(""); // For switch
        var runner = new MacUnrealEngineRunner(sshMock.Object);
        var result = await runner.SwitchXcode("/New/Xcode.app");
        Assert.Equal("/Applications/Xcode.app", result);
        sshMock.Verify(s => s.SendCommand("sudo xcode-select --switch '/New/Xcode.app'"), Times.Once);
    }

    [Fact]
    public async Task ExecuteRemoteScriptWithLogsAsync_YieldsAllLogLines()
    {
        var sshMock = new Mock<ISshConnection>();
        var logLines = new List<string> { "log1", "log2" };
        sshMock.Setup(s => s.RunCommandWithSshShellAsyncEnumerable(It.IsAny<IEnumerable<string>>()))
            .Returns(MockAsyncEnumerable(logLines));
        var runner = new MacUnrealEngineRunner(sshMock.Object);
        var result = new List<string>();
        await foreach (var line in runner.ExecuteRemoteScriptWithLogsAsync(new[] { "echo test" }))
        {
            result.Add(line);
        }

        Assert.Equal(logLines, result);
    }

    [Fact]
    public void Dispose_CallsSshDisposeOnce()
    {
        var sshMock = new Mock<ISshConnection>();
        var runner = new MacUnrealEngineRunner(sshMock.Object);
        runner.Dispose();
        runner.Dispose(); // 2回目は何もしない
        sshMock.Verify(s => s.Dispose(), Times.Once);
    }

    private static async IAsyncEnumerable<string> MockAsyncEnumerable(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            yield return line;
            await Task.CompletedTask;
        }
    }
}