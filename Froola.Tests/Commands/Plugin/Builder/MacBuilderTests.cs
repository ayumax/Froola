using Froola.Commands.Plugin;
using Froola.Commands.Plugin.Builder;
using Froola.Configs;
using Froola.Interfaces;
using Moq;

namespace Froola.Tests.Commands.Plugin.Builder;

public class MacBuilderTests
{
    private readonly Mock<IMacUnrealEngineRunner> _mockMacRunner = new();
    private readonly Mock<IFileSystem> _mockFileSystem = new();
    private readonly Mock<ITestResultsEvaluator> _mockTestResultsEvaluator = new();
    private readonly Mock<IFroolaLogger<MacBuilder>> _mockLogger = new();

    private readonly PluginConfig _pluginConfig = new()
    {
        PluginName = "TestPlugin",
        ProjectName = "TestProject",
        ResultPath = "C:/tmp",
        RunTest = true,
        RunPackage = true,
        EditorPlatforms = { EditorPlatform.Mac },
        EngineVersions = { UEVersion.UE_5_3 },
        PackagePlatforms = { GamePlatform.Mac }
    };

    private readonly WindowsConfig _windowsConfig = new();
    private readonly MacConfig _macConfig = new();

    private MacBuilder CreateBuilder()
    {
        return new MacBuilder(
            _pluginConfig, _windowsConfig, _macConfig,
            _mockMacRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task PrepareRepository_CreatesAndSetsRepositoryPath()
    {
        var builder = CreateBuilder();
        const string baseRepo = "C:/src/repo";
        const UEVersion version = UEVersion.UE_5_3;
        _mockMacRunner.Setup(r => r.DirectoryExists(It.IsAny<string>())).ReturnsAsync(false);
        _mockMacRunner.Setup(r => r.MakeDirectory(It.IsAny<string>())).ReturnsAsync(true);
        _mockMacRunner.Setup(r => r.UploadDirectory(baseRepo, It.IsAny<string>())).ReturnsAsync(true);

        await builder.PrepareRepository(baseRepo, version);
        // Verify that RepositoryPath starts with "/tmp/TestPlugin/" and ends with "/5.3"
        Assert.StartsWith("/tmp/TestPlugin/", builder.RepositoryPath);
        Assert.EndsWith("/5.3", builder.RepositoryPath);
        _mockMacRunner.Verify(r => r.MakeDirectory(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task InitDirectory_CreatesDirectoriesIfNotExist()
    {
        var builder = CreateBuilder();
        const string baseRepo = "C:/src/repo";
        const UEVersion version = UEVersion.UE_5_3;
        _mockMacRunner.Setup(r => r.DirectoryExists(It.IsAny<string>())).ReturnsAsync(false);
        _mockMacRunner.Setup(r => r.MakeDirectory(It.IsAny<string>())).ReturnsAsync(true);
        _mockMacRunner.Setup(r => r.UploadDirectory(baseRepo, It.IsAny<string>())).ReturnsAsync(true);
        await builder.PrepareRepository(baseRepo, version);
        builder.InitDirectory(version);
        // Verify that RepositoryPath is initialized
        Assert.False(string.IsNullOrEmpty(builder.RepositoryPath));
        // Verify that directory creation is called
        _mockMacRunner.Verify(r => r.MakeDirectory(It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task CleanupTempDirectory_DeletesDirectoryAndHandlesException()
    {
        var builder = CreateBuilder();
        const string baseRepo = "C:/src/repo";
        const UEVersion version = UEVersion.UE_5_3;
        await builder.PrepareRepository(baseRepo, version);
        var macRepoPath = builder.RepositoryPath;
        _mockMacRunner.Setup(r => r.DeleteDirectory(macRepoPath)).ReturnsAsync(false);

        await builder.CleanupTempDirectory();
        _mockLogger.Object.LogError(It.IsAny<string>());
    }

    // Derived class for Run branch tests
    private class TestableMacBuilder : MacBuilder
    {
        public int BuildAsyncCallCount { get; private set; }
        public int TestAsyncCallCount { get; private set; }
        public int PackageBuildAsyncCallCount { get; private set; }

        public bool BuildAsyncResult { get; set; } = true;
        public bool TestAsyncResult { get; set; } = true;
        public bool PackageBuildAsyncResult { get; set; } = true;

        public TestableMacBuilder(
            PluginConfig pluginConfig,
            WindowsConfig windowsConfig,
            MacConfig macConfig,
            IMacUnrealEngineRunner macUeRunner,
            IFileSystem fileSystem,
            ITestResultsEvaluator testResultsEvaluator,
            IFroolaLogger<MacBuilder> logger)
            : base(pluginConfig, windowsConfig, macConfig, macUeRunner, fileSystem, testResultsEvaluator, logger)
        {
        }

        protected override async Task<bool> BuildAsync()
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
        var builder = new TestableMacBuilder(
            _pluginConfig, _windowsConfig, _macConfig,
            _mockMacRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = false
        };
        const string baseRepo = "C:/src/repo";
        const UEVersion version = UEVersion.UE_5_3;
        _mockMacRunner.Setup(r => r.DirectoryExists(It.IsAny<string>())).ReturnsAsync(false);
        _mockMacRunner.Setup(r => r.MakeDirectory(It.IsAny<string>())).ReturnsAsync(true);
        _mockMacRunner.Setup(r => r.UploadDirectory(baseRepo, It.IsAny<string>())).ReturnsAsync(true);
        _mockMacRunner.Setup(r => r.DownloadDirectory(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        await builder.PrepareRepository(baseRepo, version);
        builder.InitDirectory(version);
        _mockMacRunner.Setup(r => r.FileExists(It.IsAny<string>())).ReturnsAsync(true);
        var result = await builder.Run(version);
        Assert.Equal(BuildStatus.Failed, result.StatusOfBuild);
        Assert.Equal(1, builder.BuildAsyncCallCount);
        Assert.Equal(0, builder.TestAsyncCallCount);
        Assert.Equal(0, builder.PackageBuildAsyncCallCount);
    }

    [Fact]
    public async Task Run_RunTestAndRunPackage_BothCalledIfFlagsTrue()
    {
        var builder = new TestableMacBuilder(
            _pluginConfig, _windowsConfig, _macConfig,
            _mockMacRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = true,
            TestAsyncResult = true,
            PackageBuildAsyncResult = true
        };
        _pluginConfig.RunTest = true;
        _pluginConfig.RunPackage = true;
        const string baseRepo = "C:/src/repo";
        const UEVersion version = UEVersion.UE_5_3;
        _mockMacRunner.Setup(r => r.DirectoryExists(It.IsAny<string>())).ReturnsAsync(false);
        _mockMacRunner.Setup(r => r.MakeDirectory(It.IsAny<string>())).ReturnsAsync(true);
        _mockMacRunner.Setup(r => r.UploadDirectory(baseRepo, It.IsAny<string>())).ReturnsAsync(true);
        _mockMacRunner.Setup(r => r.DownloadDirectory(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        await builder.PrepareRepository(baseRepo, version);
        builder.InitDirectory(version);
        _mockMacRunner.Setup(r => r.FileExists(It.IsAny<string>())).ReturnsAsync(true);
        var result = await builder.Run(version);
        Assert.Equal(BuildStatus.Success, result.StatusOfBuild);
        Assert.Equal(1, builder.BuildAsyncCallCount);
        Assert.Equal(1, builder.TestAsyncCallCount);
        Assert.Equal(1, builder.PackageBuildAsyncCallCount);
    }

    [Fact]
    public async Task Run_OnlyTestCalledIfRunTestTrue()
    {
        var builder = new TestableMacBuilder(
            _pluginConfig, _windowsConfig, _macConfig,
            _mockMacRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = true,
            TestAsyncResult = true,
            PackageBuildAsyncResult = true
        };
        _pluginConfig.RunTest = true;
        _pluginConfig.RunPackage = false;
        const string baseRepo = "C:/src/repo";
        const UEVersion version = UEVersion.UE_5_3;
        _mockMacRunner.Setup(r => r.DirectoryExists(It.IsAny<string>())).ReturnsAsync(false);
        _mockMacRunner.Setup(r => r.MakeDirectory(It.IsAny<string>())).ReturnsAsync(true);
        _mockMacRunner.Setup(r => r.UploadDirectory(baseRepo, It.IsAny<string>())).ReturnsAsync(true);
        _mockMacRunner.Setup(r => r.DownloadDirectory(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        await builder.PrepareRepository(baseRepo, version);
        builder.InitDirectory(version);
        _mockMacRunner.Setup(r => r.FileExists(It.IsAny<string>())).ReturnsAsync(true);
        var result = await builder.Run(version);
        Assert.Equal(BuildStatus.Success, result.StatusOfBuild);
        Assert.Equal(1, builder.TestAsyncCallCount);
        Assert.Equal(0, builder.PackageBuildAsyncCallCount);
    }

    [Fact]
    public async Task Run_OnlyPackageCalledIfRunPackageTrue()
    {
        var builder = new TestableMacBuilder(
            _pluginConfig, _windowsConfig, _macConfig,
            _mockMacRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = true,
            TestAsyncResult = true,
            PackageBuildAsyncResult = true
        };
        _pluginConfig.RunTest = false;
        _pluginConfig.RunPackage = true;
        const string baseRepo = "C:/src/repo";
        const UEVersion version = UEVersion.UE_5_3;
        _mockMacRunner.Setup(r => r.DirectoryExists(It.IsAny<string>())).ReturnsAsync(false);
        _mockMacRunner.Setup(r => r.MakeDirectory(It.IsAny<string>())).ReturnsAsync(true);
        _mockMacRunner.Setup(r => r.UploadDirectory(baseRepo, It.IsAny<string>())).ReturnsAsync(true);
        _mockMacRunner.Setup(r => r.DownloadDirectory(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        await builder.PrepareRepository(baseRepo, version);
        builder.InitDirectory(version);
        _mockMacRunner.Setup(r => r.FileExists(It.IsAny<string>())).ReturnsAsync(true);
        var result = await builder.Run(version);
        Assert.Equal(BuildStatus.Success, result.StatusOfBuild);
        Assert.Equal(0, builder.TestAsyncCallCount);
        Assert.Equal(1, builder.PackageBuildAsyncCallCount);
    }

    [Fact]
    public async Task Run_CopyPackageAfterBuild_False_DoesNotCopyPackage()
    {
        var config = _pluginConfig;
        config.CopyPackageAfterBuild = false;
        config.RunPackage = true;
        
        var macConfig = new MacConfig
        {
            CopyPackageDestinationPaths = new() { { "5.3", "/Applications/UE_5.3/Engine/Plugins" } }
        };
        macConfig.CopyPackageDestinationPathsWithVersion.Add(UEVersion.UE_5_3, "/Applications/UE_5.3/Engine/Plugins");
        
        var builder = new TestableMacBuilder(
            config, _windowsConfig, macConfig,
            _mockMacRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = true,
            PackageBuildAsyncResult = true
        };
        
        const string baseRepo = "C:/src/repo";
        const UEVersion version = UEVersion.UE_5_3;
        _mockMacRunner.Setup(r => r.DirectoryExists(It.IsAny<string>())).ReturnsAsync(false);
        _mockMacRunner.Setup(r => r.MakeDirectory(It.IsAny<string>())).ReturnsAsync(true);
        _mockMacRunner.Setup(r => r.UploadDirectory(baseRepo, It.IsAny<string>())).ReturnsAsync(true);
        _mockMacRunner.Setup(r => r.DownloadDirectory(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        _mockMacRunner.Setup(r => r.FileExists(It.IsAny<string>())).ReturnsAsync(true);
        
        await builder.PrepareRepository(baseRepo, version);
        builder.InitDirectory(version);
        
        var result = await builder.Run(version);
        
        Assert.Equal(BuildStatus.Success, result.StatusOfBuild);
        // Verify no additional copy operations were performed
        _mockFileSystem.Verify(f => f.CopyDirectory(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}