using Froola.Commands.Plugin;
using Froola.Commands.Plugin.Builder;
using Froola.Configs;
using Froola.Interfaces;
using Moq;

namespace Froola.Tests.Commands.Plugin.Builder;

public class LinuxBuilderTests
{
    private readonly Mock<IDockerRunner> _mockDockerRunner = new();
    private readonly Mock<IFileSystem> _mockFileSystem = new();
    private readonly Mock<ITestResultsEvaluator> _mockTestResultsEvaluator = new();
    private readonly Mock<IFroolaLogger<LinuxBuilder>> _mockLogger = new();

    private readonly PluginConfig _pluginConfig = new()
    {
        PluginName = "TestPlugin",
        ProjectName = "TestProject",
        ResultPath = "/tmp",
        RunTest = true,
        RunPackage = true,
        EditorPlatforms = { EditorPlatform.Linux },
        EngineVersions = { UEVersion.UE_5_3 },
        PackagePlatforms = { GamePlatform.Linux }
    };

    private readonly WindowsConfig _windowsConfig = new();
    private readonly MacConfig _macConfig = new();
    private readonly LinuxConfig _linuxConfig = new();

    private LinuxBuilder CreateBuilder()
    {
        return new LinuxBuilder(
            _pluginConfig, _windowsConfig, _macConfig, _linuxConfig,
            _mockDockerRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task PrepareRepository_CreatesAndCopiesDirectory_SetsRepositoryPath()
    {
        var builder = CreateBuilder();
        const string baseRepo = "/src/repo";
        const UEVersion version = UEVersion.UE_5_3;
        var expectedRepoPath = Path.Combine(baseRepo, "..", "Linux", version.ToVersionString());
        _mockFileSystem.Setup(f => f.DirectoryExists(expectedRepoPath)).Returns(false);

        await builder.PrepareRepository(baseRepo, version);
        builder.InitDirectory(version);
        _mockFileSystem.Verify(f => f.CreateDirectory(expectedRepoPath), Times.Once);
        _mockFileSystem.Verify(f => f.CopyDirectory(baseRepo, expectedRepoPath), Times.Once);
        Assert.Equal("/home/ue4/project", builder.RepositoryPath);
    }

    [Fact]
    public async Task PrepareRepository_HandlesException_LogsError()
    {
        var builder = CreateBuilder();
        const string baseRepo = "/src/repo";
        const UEVersion version = UEVersion.UE_5_3;
        _mockFileSystem.Setup(f => f.DirectoryExists(It.IsAny<string>())).Throws(new IOException("fail"));

        await builder.PrepareRepository(baseRepo, version);
        _mockLogger.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<IOException>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task InitDirectory_CreatesDirectoriesIfNotExist()
    {
        var builder = CreateBuilder();
        _mockFileSystem.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(false);
        await builder.PrepareRepository("/src/repo", UEVersion.UE_5_3);
        builder.InitDirectory(UEVersion.UE_5_3);
        _mockFileSystem.Verify(f => f.CreateDirectory(It.IsAny<string>()), Times.Exactly(4));
    }

    [Fact]
    public async Task CleanupTempDirectory_DeletesDirectoryAndHandlesException()
    {
        var builder = CreateBuilder();
        _mockFileSystem.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(f => f.DeleteDirectory(It.IsAny<string>(), true)).Throws(new IOException("fail"));
        await builder.PrepareRepository("/src/repo", UEVersion.UE_5_3);
        builder.InitDirectory(UEVersion.UE_5_3);
        await builder.CleanupTempDirectory();
        _mockLogger.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<IOException>(), It.IsAny<string>()),
            Times.Once);
    }

    // Derived class for Run branch tests
    private class TestableLinuxBuilder : LinuxBuilder
    {
        public int BuildAsyncCallCount { get; private set; }
        public int TestAsyncCallCount { get; private set; }
        public int PackageBuildAsyncCallCount { get; private set; }

        public bool BuildAsyncResult { get; set; } = true;
        public bool TestAsyncResult { get; set; } = true;
        public bool PackageBuildAsyncResult { get; set; } = true;

        public TestableLinuxBuilder(
            PluginConfig pluginConfig,
            WindowsConfig windowsConfig,
            MacConfig macConfig,
            LinuxConfig linuxConfig,
            IDockerRunner dockerRunner,
            IFileSystem fileSystem,
            ITestResultsEvaluator testResultsEvaluator,
            IFroolaLogger<LinuxBuilder> logger)
            : base(pluginConfig, windowsConfig, macConfig, linuxConfig, dockerRunner, fileSystem, testResultsEvaluator, logger)
        {
        }

        protected override async Task<bool> BuildAsync(UEVersion engineVersion)
        {
            BuildAsyncCallCount++;
            return await Task.FromResult(BuildAsyncResult);
        }

        protected override async Task<bool> TestAsync(UEVersion engineVersion)
        {
            TestAsyncCallCount++;
            return await Task.FromResult(TestAsyncResult);
        }

        protected override async Task<bool> PackageBuildAsync(UEVersion engineVersion)
        {
            PackageBuildAsyncCallCount++;
            return await Task.FromResult(PackageBuildAsyncResult);
        }
    }

    [Fact]
    public async Task Run_BuildFails_EndsImmediately()
    {
        _mockDockerRunner.Setup(r => r.IsDockerReady()).ReturnsAsync(true);
var builder = new TestableLinuxBuilder(
            _pluginConfig, _windowsConfig, _macConfig, _linuxConfig,
            _mockDockerRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = false
        };
        const UEVersion version = UEVersion.UE_5_3;
        await builder.PrepareRepository("/src/repo", version);
        builder.InitDirectory(version);
        var result = await builder.Run(version);
        Assert.Equal(BuildStatus.Failed, result.StatusOfBuild);
        Assert.Equal(1, builder.BuildAsyncCallCount);
        Assert.Equal(0, builder.TestAsyncCallCount);
        Assert.Equal(0, builder.PackageBuildAsyncCallCount);
    }

    [Fact]
    public async Task Run_RunTestAndRunPackage_BothCalledIfFlagsTrue()
    {
        _mockDockerRunner.Setup(r => r.IsDockerReady()).ReturnsAsync(true);
var builder = new TestableLinuxBuilder(
            _pluginConfig, _windowsConfig, _macConfig, _linuxConfig,
            _mockDockerRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = true,
            TestAsyncResult = true,
            PackageBuildAsyncResult = true
        };
        _pluginConfig.RunTest = true;
        _pluginConfig.RunPackage = true;
        const UEVersion version = UEVersion.UE_5_3;
        await builder.PrepareRepository("/src/repo", version);
        builder.InitDirectory(version);
        var result = await builder.Run(version);
        Assert.Equal(BuildStatus.Success, result.StatusOfBuild);
        Assert.Equal(1, builder.BuildAsyncCallCount);
        Assert.Equal(1, builder.TestAsyncCallCount);
        Assert.Equal(1, builder.PackageBuildAsyncCallCount);
    }

    [Fact]
    public async Task Run_OnlyTestCalledIfRunTestTrue()
    {
        _mockDockerRunner.Setup(r => r.IsDockerReady()).ReturnsAsync(true);
var builder = new TestableLinuxBuilder(
            _pluginConfig, _windowsConfig, _macConfig, _linuxConfig,
            _mockDockerRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = true,
            TestAsyncResult = true,
            PackageBuildAsyncResult = true
        };
        _pluginConfig.RunTest = true;
        _pluginConfig.RunPackage = false;
        const UEVersion version = UEVersion.UE_5_3;
        await builder.PrepareRepository("/src/repo", version);
        builder.InitDirectory(version);
        var result = await builder.Run(version);
        Assert.Equal(1, builder.TestAsyncCallCount);
        Assert.Equal(0, builder.PackageBuildAsyncCallCount);
    }

    [Fact]
    public async Task Run_OnlyPackageCalledIfRunPackageTrue()
    {
        _mockDockerRunner.Setup(r => r.IsDockerReady()).ReturnsAsync(true);
var builder = new TestableLinuxBuilder(
            _pluginConfig, _windowsConfig, _macConfig, _linuxConfig,
            _mockDockerRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = true,
            TestAsyncResult = true,
            PackageBuildAsyncResult = true
        };
        _pluginConfig.RunTest = false;
        _pluginConfig.RunPackage = true;
        const UEVersion version = UEVersion.UE_5_3;
        await builder.PrepareRepository("/src/repo", version);
        builder.InitDirectory(version);
        var result = await builder.Run(version);
        Assert.Equal(0, builder.TestAsyncCallCount);
        Assert.Equal(1, builder.PackageBuildAsyncCallCount);
    }
}