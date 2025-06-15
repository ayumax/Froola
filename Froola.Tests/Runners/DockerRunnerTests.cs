using Froola.Configs;
using Froola.Interfaces;
using Froola.Runners;
using Moq;

namespace Froola.Tests.Runners;

public class DockerRunnerTests
{
    [Fact]
    public async Task CopyScriptToDockerAndNormalizeAsync_ReturnsCorrectPath_WhenFileExists()
    {
        // Arrange
        var loggerMock = new Mock<IFroolaLogger<DockerRunner>>();
        var fileSystemMock = new Mock<IFileSystem>();
        const string localScriptPath = "C:/project/test.sh";
        const string projectDir = "C:/project";
        const string projectDirInDocker = "/docker/project";
        var dockerScriptPath = Path.Combine(projectDir, Path.GetFileName(localScriptPath));
        const string scriptContent = "echo test\r\necho done\r\n";

        fileSystemMock.Setup(f => f.FileExists(localScriptPath)).Returns(true);
        fileSystemMock.Setup(f => f.FileCopy(localScriptPath, dockerScriptPath, true));
        fileSystemMock.Setup(f => f.FileReadAllTextAsync(dockerScriptPath)).ReturnsAsync(scriptContent);
        fileSystemMock.Setup(f => f.WriteAllTextAsync(dockerScriptPath, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var processRunnerMock = new Mock<IProcessRunner>();
        var linuxConfig = new LinuxConfig { DockerCommand = "docker" };
        var runner = new DockerRunner(loggerMock.Object, fileSystemMock.Object, processRunnerMock.Object,
            linuxConfig);

        // Act
        var result = await runner.CopyScriptToDockerAndNormalizeAsync(localScriptPath, projectDir, projectDirInDocker);

        // Assert
        Assert.Equal("/docker/project/test.sh", result);
        fileSystemMock.Verify(f => f.FileCopy(localScriptPath, dockerScriptPath, true), Times.Once);
        fileSystemMock.Verify(f => f.WriteAllTextAsync(dockerScriptPath, "echo test\necho done\n"), Times.Once);
        loggerMock.Verify(l => l.LogInformation(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task CopyScriptToDockerAndNormalizeAsync_ReturnsNull_WhenFileDoesNotExist()
    {
        // Arrange
        var loggerMock = new Mock<IFroolaLogger<DockerRunner>>();
        var fileSystemMock = new Mock<IFileSystem>();
        const string localScriptPath = "C:/project/notfound.sh";
        const string projectDir = "C:/project";
        const string projectDirInDocker = "/docker/project";

        fileSystemMock.Setup(f => f.FileExists(localScriptPath)).Returns(false);
        var processRunnerMock = new Mock<IProcessRunner>();
        var linuxConfig = new LinuxConfig { DockerCommand = "docker" };
        var runner = new DockerRunner(loggerMock.Object, fileSystemMock.Object, processRunnerMock.Object,
            linuxConfig);

        // Act
        var result = await runner.CopyScriptToDockerAndNormalizeAsync(localScriptPath, projectDir, projectDirInDocker);

        // Assert
        Assert.Null(result);
        loggerMock.Verify(l => l.LogError(It.Is<string>(s => s.Contains(localScriptPath)), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task RunContainer_LogsCorrectDockerCommand()
    {
        // Arrange
        var loggerMock = new Mock<IFroolaLogger<DockerRunner>>();
        var fileSystemMock = new Mock<IFileSystem>();
        var processRunnerMock = new Mock<IProcessRunner>();
        var linuxConfig = new LinuxConfig { DockerCommand = "docker" };
        var runner = new DockerRunner(loggerMock.Object, fileSystemMock.Object, processRunnerMock.Object,
            linuxConfig);
        const string imageName = "ubuntu:latest";
        const string command = "echo hello";
        const string workingDirectory = "C:/project";
        var volumeMappings = new Dictionary<string, string> { { "C:/host", "/container" } };
        var envVars = new Dictionary<string, string> { { "ENV1", "VALUE1" } };

        // Act
        var enumerator = runner.RunContainer(imageName, command, workingDirectory, volumeMappings, envVars)
            .GetAsyncEnumerator();
        try
        {
            await enumerator.MoveNextAsync();
        }
        catch
        {
            // Ignore any exceptions from ProcessX
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        // Assert
        loggerMock.Verify(l => l.LogInformation(It.Is<string>(s => s.Contains("docker run")), It.IsAny<string>()),
            Times.Once);
        loggerMock.Verify(l => l.LogInformation(It.Is<string>(s => s.Contains(imageName)), It.IsAny<string>()),
            Times.Once);
        loggerMock.Verify(l => l.LogInformation(It.Is<string>(s => s.Contains(command)), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task PreparePluginsForDockerAsync_ReturnsNull_WhenSourcePathIsEmpty()
    {
        // Arrange
        var loggerMock = new Mock<IFroolaLogger<DockerRunner>>();
        var fileSystemMock = new Mock<IFileSystem>();
        var processRunnerMock = new Mock<IProcessRunner>();
        var linuxConfig = new LinuxConfig { DockerCommand = "docker" };
        var runner = new DockerRunner(loggerMock.Object, fileSystemMock.Object, processRunnerMock.Object,
            linuxConfig);

        // Act
        var result = await runner.PreparePluginsForDockerAsync("", "C:/project");

        // Assert
        Assert.Null(result);
        loggerMock.Verify(l => l.LogInformation(It.Is<string>(s => s.Contains("empty")), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task PreparePluginsForDockerAsync_ReturnsNull_WhenSourceDirectoryDoesNotExist()
    {
        // Arrange
        var loggerMock = new Mock<IFroolaLogger<DockerRunner>>();
        var fileSystemMock = new Mock<IFileSystem>();
        var processRunnerMock = new Mock<IProcessRunner>();
        var linuxConfig = new LinuxConfig { DockerCommand = "docker" };
        var runner = new DockerRunner(loggerMock.Object, fileSystemMock.Object, processRunnerMock.Object,
            linuxConfig);
        const string sourcePath = "C:/plugins";
        
        fileSystemMock.Setup(f => f.DirectoryExists(sourcePath)).Returns(false);

        // Act
        var result = await runner.PreparePluginsForDockerAsync(sourcePath, "C:/project");

        // Assert
        Assert.Null(result);
        loggerMock.Verify(l => l.LogError(It.Is<string>(s => s.Contains(sourcePath)), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task PreparePluginsForDockerAsync_ReturnsStagePathWhenSuccessful()
    {
        // Arrange
        var loggerMock = new Mock<IFroolaLogger<DockerRunner>>();
        var fileSystemMock = new Mock<IFileSystem>();
        var processRunnerMock = new Mock<IProcessRunner>();
        var linuxConfig = new LinuxConfig { DockerCommand = "docker" };
        var runner = new DockerRunner(loggerMock.Object, fileSystemMock.Object, processRunnerMock.Object,
            linuxConfig);
        const string sourcePath = "C:/plugins";
        const string projectDir = "C:/project";
        var expectedStagePath = Path.Combine(projectDir, "PluginsStage");
        
        fileSystemMock.Setup(f => f.DirectoryExists(sourcePath)).Returns(true);
        fileSystemMock.Setup(f => f.DirectoryExists(expectedStagePath)).Returns(false);
        fileSystemMock.Setup(f => f.CreateDirectory(expectedStagePath));
        fileSystemMock.Setup(f => f.CopyDirectory(sourcePath, expectedStagePath));

        // Act
        var result = await runner.PreparePluginsForDockerAsync(sourcePath, projectDir);

        // Assert
        Assert.Equal(expectedStagePath, result);
        fileSystemMock.Verify(f => f.CopyDirectory(sourcePath, expectedStagePath), Times.Once);
        loggerMock.Verify(l => l.LogInformation(It.Is<string>(s => s.Contains("Successfully prepared")), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task PreparePluginsForDockerAsync_CleansExistingStageDirectory()
    {
        // Arrange
        var loggerMock = new Mock<IFroolaLogger<DockerRunner>>();
        var fileSystemMock = new Mock<IFileSystem>();
        var processRunnerMock = new Mock<IProcessRunner>();
        var linuxConfig = new LinuxConfig { DockerCommand = "docker" };
        var runner = new DockerRunner(loggerMock.Object, fileSystemMock.Object, processRunnerMock.Object,
            linuxConfig);
        const string sourcePath = "C:/plugins";
        const string projectDir = "C:/project";
        var expectedStagePath = Path.Combine(projectDir, "PluginsStage");
        
        fileSystemMock.Setup(f => f.DirectoryExists(sourcePath)).Returns(true);
        fileSystemMock.Setup(f => f.DirectoryExists(expectedStagePath)).Returns(true);
        fileSystemMock.Setup(f => f.DeleteDirectory(expectedStagePath, true));
        fileSystemMock.Setup(f => f.CreateDirectory(expectedStagePath));
        fileSystemMock.Setup(f => f.CopyDirectory(sourcePath, expectedStagePath));

        // Act
        var result = await runner.PreparePluginsForDockerAsync(sourcePath, projectDir);

        // Assert
        Assert.Equal(expectedStagePath, result);
        fileSystemMock.Verify(f => f.DeleteDirectory(expectedStagePath, true), Times.Once);
        fileSystemMock.Verify(f => f.CreateDirectory(expectedStagePath), Times.Once);
    }
}