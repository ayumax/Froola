using Froola.Configs;
using Froola.Interfaces;
using Froola.Runners;
using Microsoft.Extensions.Options;
using Moq;

namespace Froola.Tests.Runners;

public class WindowsUnrealEngineRunnerTests
{
    [Fact]
    public async Task BuildPlugin_LogsAndRunsProcess()
    {
        // Arrange
        var loggerMock = new Mock<IFroolaLogger<WindowsUnrealEngineRunner>>();
        var optionsMock = new Mock<IOptions<WindowsConfig>>();
        var processRunnerMock = new Mock<IProcessRunner>();
        const string unrealBasePath = "C:/UE";
        var config = new WindowsConfig { WindowsUnrealBasePath = unrealBasePath };
        optionsMock.Setup(o => o.Value).Returns(config);
        var runner = new WindowsUnrealEngineRunner(loggerMock.Object, optionsMock.Object, processRunnerMock.Object);
        const string pluginPath = "C:/project/MyPlugin.uplugin";
        const string outputPath = "C:/output";
        const int engineVersion = 3;
        const string targetPlatforms = "Win64";
        const string logFilePath = "";
        var expectedRunFile = Path.Combine(unrealBasePath, $"UE_5.{engineVersion}", "Engine", "Binaries", "DotNET",
            "AutomationTool", "AutomationTool.exe");
        var expectedArgs =
            $"BuildPlugin -Plugin={pluginPath} -Package={outputPath} -CreateSubFolder -TargetPlatforms={targetPlatforms} -NoSplash -Unattended -NullRHI\"";
        var workDirectory = Path.GetDirectoryName(pluginPath)!;
        processRunnerMock.Setup(pr => pr.RunAsync(expectedRunFile, expectedArgs, workDirectory, null))
            .Returns(MockAsyncEnumerable());

        // Act
        await runner.BuildPlugin(pluginPath, outputPath, engineVersion, targetPlatforms, logFilePath);

        // Assert
        loggerMock.Verify(
            l => l.LogInformation(It.Is<string>(s => s.Contains("Running UE build command")), It.IsAny<string>()),
            Times.Once);
        processRunnerMock.Verify(pr => pr.RunAsync(expectedRunFile, expectedArgs, workDirectory, null), Times.Once);
    }

    [Fact]
    public async Task RunUnrealEditor_LogsAndRunsProcess()
    {
        // Arrange
        var loggerMock = new Mock<IFroolaLogger<WindowsUnrealEngineRunner>>();
        var optionsMock = new Mock<IOptions<WindowsConfig>>();
        var processRunnerMock = new Mock<IProcessRunner>();
        const string unrealBasePath = "C:/UE";
        var config = new WindowsConfig { WindowsUnrealBasePath = unrealBasePath };
        optionsMock.Setup(o => o.Value).Returns(config);
        var runner = new WindowsUnrealEngineRunner(loggerMock.Object, optionsMock.Object, processRunnerMock.Object);
        const string editorPath = "C:/UE/Editor.exe";
        const string arguments = "-project=Test.uproject";
        const string workingDirectory = "C:/project";
        const string logFilePath = "";
        processRunnerMock.Setup(pr => pr.RunAsync(editorPath, arguments, workingDirectory, null))
            .Returns(MockAsyncEnumerable());

        // Act
        await runner.RunUnrealEditor(editorPath, arguments, workingDirectory, logFilePath);

        // Assert
        loggerMock.Verify(
            l => l.LogInformation(It.Is<string>(s => s.Contains("Running UnrealEditor command")), It.IsAny<string>()),
            Times.Once);
        processRunnerMock.Verify(pr => pr.RunAsync(editorPath, arguments, workingDirectory, null), Times.Once);
    }

    [Fact]
    public async Task RunBuildScript_LogsAndRunsProcess()
    {
        // Arrange
        var loggerMock = new Mock<IFroolaLogger<WindowsUnrealEngineRunner>>();
        var optionsMock = new Mock<IOptions<WindowsConfig>>();
        var processRunnerMock = new Mock<IProcessRunner>();
        var config = new WindowsConfig { WindowsUnrealBasePath = "C:/UE" };
        optionsMock.Setup(o => o.Value).Returns(config);
        var runner = new WindowsUnrealEngineRunner(loggerMock.Object, optionsMock.Object, processRunnerMock.Object);
        const string buildScriptPath = "C:/scripts/build.bat";
        const string arguments = "--target Win64";
        const string workingDirectory = "C:/project";
        const string logFilePath = "";
        processRunnerMock.Setup(pr => pr.RunAsync(buildScriptPath, arguments, workingDirectory, null))
            .Returns(MockAsyncEnumerable());

        // Act
        await runner.RunBuildScript(buildScriptPath, arguments, workingDirectory, logFilePath);

        // Assert
        loggerMock.Verify(
            l => l.LogInformation(It.Is<string>(s => s.Contains("Running build script")), It.IsAny<string>()),
            Times.Once);
        processRunnerMock.Verify(pr => pr.RunAsync(buildScriptPath, arguments, workingDirectory, null), Times.Once);
    }

    private static async IAsyncEnumerable<string> MockAsyncEnumerable()
    {
        yield return "Process output line 1";
        await Task.CompletedTask;
    }
}