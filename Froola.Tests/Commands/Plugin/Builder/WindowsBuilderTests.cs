using Froola.Commands.Plugin;
using Froola.Commands.Plugin.Builder;
using Froola.Configs;
using Froola.Interfaces;
using Moq;

namespace Froola.Tests.Commands.Plugin.Builder;

public class WindowsBuilderTests
{
    private readonly Mock<IUnrealEngineRunner> _mockUnrealRunner = new();
    private readonly Mock<IFileSystem> _mockFileSystem = new();
    private readonly Mock<ITestResultsEvaluator> _mockTestResultsEvaluator = new();
    private readonly Mock<IFroolaLogger<WindowsBuilder>> _mockLogger = new();

    private readonly PluginConfig _pluginConfig = new()
    {
        PluginName = "TestPlugin",
        ProjectName = "TestProject",
        ResultPath = "C:/tmp",
        RunTest = true,
        RunPackage = true,
        EditorPlatforms = { EditorPlatform.Windows },
        EngineVersions = { UEVersion.UE_5_3 },
        PackagePlatforms = { GamePlatform.Win64 }
    };

    private readonly WindowsConfig _windowsConfig = new();
    private readonly MacConfig _macConfig = new();

    private WindowsBuilder CreateBuilder()
    {
        return new WindowsBuilder(
            _pluginConfig, _windowsConfig, _macConfig,
            _mockUnrealRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task PrepareRepository_CopiesDirectoryAndSetsRepositoryPath()
    {
        var builder = CreateBuilder();
        const string baseRepo = "C:/src/repo";
        const UEVersion version = UEVersion.UE_5_3;
        var expectedPath = Path.Combine(baseRepo, "..", "Windows");

        await builder.PrepareRepository(baseRepo, version);
        Assert.Equal(expectedPath, builder.RepositoryPath);
        _mockFileSystem.Verify(f => f.CopyDirectory(baseRepo, expectedPath), Times.Once);
    }

    [Fact]
    public async Task InitDirectory_CreatesDirectoriesIfNotExist()
    {
        var builder = CreateBuilder();
        const UEVersion version = UEVersion.UE_5_3;
        _mockFileSystem.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(false);
        const string baseRepo = "C:/tmp/TestRepoBase";

        await builder.PrepareRepository(baseRepo, version);

        builder.InitDirectory(version);
        _mockFileSystem.Verify(f => f.CreateDirectory(It.IsAny<string>()), Times.Exactly(3));
    }

    [Fact]
    public async Task CleanupTempDirectory_DeletesDirectoryAndHandlesException()
    {
        var builder = CreateBuilder();
        // RepositoryPath is set by PrepareRepository
        const string baseRepo = "C:/tmp/TestRepoBase";
        const UEVersion version = UEVersion.UE_5_3;
        await builder.PrepareRepository(baseRepo, version);
        _mockFileSystem.Setup(f => f.DeleteDirectory(It.IsAny<string>(), true)).Throws(new IOException("fail"));
        await builder.CleanupTempDirectory();
        _mockLogger.Verify(l => l.LogWarning(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    // Derived class for Run branch tests
    private class TestableWindowsBuilder(
        PluginConfig pluginConfig,
        WindowsConfig windowsConfig,
        MacConfig macConfig,
        IUnrealEngineRunner unrealRunner,
        IFileSystem fileSystem,
        ITestResultsEvaluator testResultsEvaluator,
        IFroolaLogger<WindowsBuilder> logger)
        : WindowsBuilder(pluginConfig, windowsConfig, macConfig, unrealRunner, fileSystem, testResultsEvaluator, logger)
    {
        public int BuildAsyncCallCount { get; private set; }
        public int TestAsyncCallCount { get; private set; }
        public int PackageBuildAsyncCallCount { get; private set; }

        public bool BuildAsyncResult { get; set; } = true;
        public bool TestAsyncResult { get; set; } = true;
        public bool PackageBuildAsyncResult { get; set; } = true;

        protected override async Task<bool> BuildAsync()
        {
            BuildAsyncCallCount++;
            return await Task.FromResult(BuildAsyncResult);
        }

        protected override async Task<bool> TestAsync()
        {
            TestAsyncCallCount++;
            return await Task.FromResult(TestAsyncResult);
        }

        protected override async Task<bool> PackageBuildAsync(string repoPath, string pluginName, string outputDir)
        {
            PackageBuildAsyncCallCount++;
            return await Task.FromResult(PackageBuildAsyncResult);
        }
    }

    [Fact]
    public async Task Run_BuildFails_EndsImmediately()
    {
        var builder = new TestableWindowsBuilder(
            _pluginConfig, _windowsConfig, _macConfig,
            _mockUnrealRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = false
        };
        const UEVersion version = UEVersion.UE_5_3;
        const string baseRepo = "C:/tmp/TestRepoBase";
        await builder.PrepareRepository(baseRepo, version);
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
        var builder = new TestableWindowsBuilder(
            _pluginConfig, _windowsConfig, _macConfig,
            _mockUnrealRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = true,
            TestAsyncResult = true,
            PackageBuildAsyncResult = true
        };
        _pluginConfig.RunTest = true;
        _pluginConfig.RunPackage = true;
        const UEVersion version = UEVersion.UE_5_3;
        const string baseRepo = "C:/tmp/TestRepoBase";
        await builder.PrepareRepository(baseRepo, version);
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
        var builder = new TestableWindowsBuilder(
            _pluginConfig, _windowsConfig, _macConfig,
            _mockUnrealRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = true,
            TestAsyncResult = true,
            PackageBuildAsyncResult = true
        };
        _pluginConfig.RunTest = true;
        _pluginConfig.RunPackage = false;
        const UEVersion version = UEVersion.UE_5_3;
        const string baseRepo = "C:/tmp/TestRepoBase";
        await builder.PrepareRepository(baseRepo, version);
        builder.InitDirectory(version);
        var result = await builder.Run(version);
        Assert.Equal(1, builder.TestAsyncCallCount);
        Assert.Equal(0, builder.PackageBuildAsyncCallCount);
    }

    [Fact]
    public async Task Run_OnlyPackageCalledIfRunPackageTrue()
    {
        var builder = new TestableWindowsBuilder(
            _pluginConfig, _windowsConfig, _macConfig,
            _mockUnrealRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = true,
            TestAsyncResult = true,
            PackageBuildAsyncResult = true
        };
        _pluginConfig.RunTest = false;
        _pluginConfig.RunPackage = true;
        const UEVersion version = UEVersion.UE_5_3;
        const string baseRepo = "C:/tmp/TestRepoBase";
        await builder.PrepareRepository(baseRepo, version);
        builder.InitDirectory(version);
        var result = await builder.Run(version);
        Assert.Equal(0, builder.TestAsyncCallCount);
        Assert.Equal(1, builder.PackageBuildAsyncCallCount);
    }

    [Fact]
    public async Task Run_CopyPackageAfterBuild_False_DoesNotCopyPackage()
    {
        var config = _pluginConfig;
        config.CopyPackageAfterBuild = false;
        config.RunPackage = true;
        
        var windowsConfig = new WindowsConfig
        {
            CopyPackageDestinationPaths = new() { { "5.3", "C:/TestDestination" } }
        };
        windowsConfig.CopyPackageDestinationPathsWithVersion.Add(UEVersion.UE_5_3, "C:/TestDestination");
        
        var builder = new TestableWindowsBuilder(
            config, windowsConfig, _macConfig,
            _mockUnrealRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = true,
            PackageBuildAsyncResult = true
        };
        
        _mockTestResultsEvaluator.Setup(e => e.EvaluatePackageBuildResults(It.IsAny<string>(), It.IsAny<EditorPlatform>(), It.IsAny<UEVersion>()))
            .Returns(BuildStatus.Success);
        
        const UEVersion version = UEVersion.UE_5_3;
        const string baseRepo = "C:/tmp/TestRepoBase";
        await builder.PrepareRepository(baseRepo, version);
        builder.InitDirectory(version);
        
        var result = await builder.Run(version);
        
        Assert.Equal(BuildStatus.Success, result.StatusOfPackage);
        // Verify no copy operations were performed
        _mockFileSystem.Verify(f => f.CopyDirectory(It.IsAny<string>(), It.IsAny<string>()), Times.Once); // Only PrepareRepository copy
    }
    
    [Fact]
    public async Task Run_CopyPackageAfterBuild_True_WithValidDestination_CopiesPackage()
    {
        var config = _pluginConfig;
        config.CopyPackageAfterBuild = true;
        config.RunPackage = true;
        
        var windowsConfig = new WindowsConfig
        {
            CopyPackageDestinationPaths = new() { { "5.3", "C:/TestDestination" } }
        };
        windowsConfig.CopyPackageDestinationPathsWithVersion.Add(UEVersion.UE_5_3, "C:/TestDestination");
        
        var builder = new TestableWindowsBuilder(
            config, windowsConfig, _macConfig,
            _mockUnrealRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = true,
            PackageBuildAsyncResult = true
        };
        
        _mockTestResultsEvaluator.Setup(e => e.EvaluatePackageBuildResults(It.IsAny<string>(), It.IsAny<EditorPlatform>(), It.IsAny<UEVersion>()))
            .Returns(BuildStatus.Success);
        
        // Setup file system mocks
        var packageSourcePath = Path.Combine("C:", "tmp", "packages", "Windows_UE5.3", "TestPlugin");
        var destinationPath = Path.Combine("C:/TestDestination", "TestPlugin");
        
        _mockFileSystem.Setup(f => f.DirectoryExists(packageSourcePath)).Returns(true);
        _mockFileSystem.Setup(f => f.DirectoryExists("C:/TestDestination")).Returns(true);
        _mockFileSystem.Setup(f => f.DirectoryExists(destinationPath)).Returns(false);
        
        const UEVersion version = UEVersion.UE_5_3;
        const string baseRepo = "C:/tmp/TestRepoBase";
        await builder.PrepareRepository(baseRepo, version);
        builder.InitDirectory(version);
        
        var result = await builder.Run(version);
        
        Assert.Equal(BuildStatus.Success, result.StatusOfPackage);
        
        // Verify copy operations
        _mockFileSystem.Verify(f => f.CopyDirectory(packageSourcePath, destinationPath), Times.Once);
        _mockLogger.Verify(l => l.LogInformation(It.Is<string>(s => s.Contains("Successfully copied packaged plugin")), It.IsAny<string>()), Times.Once);
    }
    
    [Fact]
    public async Task Run_CopyPackageAfterBuild_True_PackageNotFound_LogsWarning()
    {
        var config = _pluginConfig;
        config.CopyPackageAfterBuild = true;
        config.RunPackage = true;
        
        var windowsConfig = new WindowsConfig
        {
            CopyPackageDestinationPaths = new() { { "5.3", "C:/TestDestination" } }
        };
        windowsConfig.CopyPackageDestinationPathsWithVersion.Add(UEVersion.UE_5_3, "C:/TestDestination");
        
        var builder = new TestableWindowsBuilder(
            config, windowsConfig, _macConfig,
            _mockUnrealRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = true,
            PackageBuildAsyncResult = true
        };
        
        _mockTestResultsEvaluator.Setup(e => e.EvaluatePackageBuildResults(It.IsAny<string>(), It.IsAny<EditorPlatform>(), It.IsAny<UEVersion>()))
            .Returns(BuildStatus.Success);
        
        // Setup file system to return false for package directory existence
        var packageSourcePath = Path.Combine("C:", "tmp", "packages", "Windows_UE5.3", "TestPlugin");
        _mockFileSystem.Setup(f => f.DirectoryExists(packageSourcePath)).Returns(false);
        
        const UEVersion version = UEVersion.UE_5_3;
        const string baseRepo = "C:/tmp/TestRepoBase";
        await builder.PrepareRepository(baseRepo, version);
        builder.InitDirectory(version);
        
        var result = await builder.Run(version);
        
        Assert.Equal(BuildStatus.Success, result.StatusOfPackage);
        _mockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("Packaged plugin directory not found")), It.IsAny<string>()), Times.Once);
    }
    
    [Fact]
    public async Task Run_CopyPackageAfterBuild_True_NoDestinationConfigured_LogsWarning()
    {
        var config = _pluginConfig;
        config.CopyPackageAfterBuild = true;
        config.RunPackage = true;
        
        var windowsConfig = new WindowsConfig(); // No destination paths configured
        
        var builder = new TestableWindowsBuilder(
            config, windowsConfig, _macConfig,
            _mockUnrealRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = true,
            PackageBuildAsyncResult = true
        };
        
        _mockTestResultsEvaluator.Setup(e => e.EvaluatePackageBuildResults(It.IsAny<string>(), It.IsAny<EditorPlatform>(), It.IsAny<UEVersion>()))
            .Returns(BuildStatus.Success);
        
        const UEVersion version = UEVersion.UE_5_3;
        const string baseRepo = "C:/tmp/TestRepoBase";
        await builder.PrepareRepository(baseRepo, version);
        builder.InitDirectory(version);
        
        var result = await builder.Run(version);
        
        Assert.Equal(BuildStatus.Success, result.StatusOfPackage);
        _mockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("No destination path configured for plugin copy")), It.IsAny<string>()), Times.Once);
    }
    
    [Fact]
    public async Task Run_CopyPackageAfterBuild_True_CreatesDestinationDirectory()
    {
        var config = _pluginConfig;
        config.CopyPackageAfterBuild = true;
        config.RunPackage = true;
        
        var windowsConfig = new WindowsConfig
        {
            CopyPackageDestinationPaths = new() { { "5.3", "C:/NewDestination" } }
        };
        windowsConfig.CopyPackageDestinationPathsWithVersion.Add(UEVersion.UE_5_3, "C:/NewDestination");
        
        var builder = new TestableWindowsBuilder(
            config, windowsConfig, _macConfig,
            _mockUnrealRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = true,
            PackageBuildAsyncResult = true
        };
        
        _mockTestResultsEvaluator.Setup(e => e.EvaluatePackageBuildResults(It.IsAny<string>(), It.IsAny<EditorPlatform>(), It.IsAny<UEVersion>()))
            .Returns(BuildStatus.Success);
        
        // Setup file system mocks
        var packageSourcePath = Path.Combine("C:", "tmp", "packages", "Windows_UE5.3", "TestPlugin");
        _mockFileSystem.Setup(f => f.DirectoryExists(packageSourcePath)).Returns(true);
        _mockFileSystem.Setup(f => f.DirectoryExists("C:/NewDestination")).Returns(false); // Destination doesn't exist
        
        const UEVersion version = UEVersion.UE_5_3;
        const string baseRepo = "C:/tmp/TestRepoBase";
        await builder.PrepareRepository(baseRepo, version);
        builder.InitDirectory(version);
        
        var result = await builder.Run(version);
        
        Assert.Equal(BuildStatus.Success, result.StatusOfPackage);
        
        // Verify destination directory creation
        _mockFileSystem.Verify(f => f.CreateDirectory("C:/NewDestination"), Times.Once);
        _mockLogger.Verify(l => l.LogInformation(It.Is<string>(s => s.Contains("Created destination directory")), It.IsAny<string>()), Times.Once);
    }
    
    [Fact]
    public async Task Run_CopyPackageAfterBuild_True_RemovesExistingPlugin()
    {
        var config = _pluginConfig;
        config.CopyPackageAfterBuild = true;
        config.RunPackage = true;
        
        var windowsConfig = new WindowsConfig
        {
            CopyPackageDestinationPaths = new() { { "5.3", "C:/TestDestination" } }
        };
        windowsConfig.CopyPackageDestinationPathsWithVersion.Add(UEVersion.UE_5_3, "C:/TestDestination");
        
        var builder = new TestableWindowsBuilder(
            config, windowsConfig, _macConfig,
            _mockUnrealRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = true,
            PackageBuildAsyncResult = true
        };
        
        _mockTestResultsEvaluator.Setup(e => e.EvaluatePackageBuildResults(It.IsAny<string>(), It.IsAny<EditorPlatform>(), It.IsAny<UEVersion>()))
            .Returns(BuildStatus.Success);
        
        // Setup file system mocks
        var packageSourcePath = Path.Combine("C:", "tmp", "packages", "Windows_UE5.3", "TestPlugin");
        var destinationPath = Path.Combine("C:/TestDestination", "TestPlugin");
        
        _mockFileSystem.Setup(f => f.DirectoryExists(packageSourcePath)).Returns(true);
        _mockFileSystem.Setup(f => f.DirectoryExists("C:/TestDestination")).Returns(true);
        _mockFileSystem.Setup(f => f.DirectoryExists(destinationPath)).Returns(true); // Plugin already exists
        
        const UEVersion version = UEVersion.UE_5_3;
        const string baseRepo = "C:/tmp/TestRepoBase";
        await builder.PrepareRepository(baseRepo, version);
        builder.InitDirectory(version);
        
        var result = await builder.Run(version);
        
        Assert.Equal(BuildStatus.Success, result.StatusOfPackage);
        
        // Verify existing plugin removal
        _mockFileSystem.Verify(f => f.DeleteDirectory(destinationPath, true), Times.Once);
        _mockLogger.Verify(l => l.LogInformation(It.Is<string>(s => s.Contains("Removed existing plugin")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Run_CopyPackageAfterBuild_True_PackageFailed_DoesNotCopy()
    {
        var config = _pluginConfig;
        config.CopyPackageAfterBuild = true;
        config.RunPackage = true;
        
        var windowsConfig = new WindowsConfig
        {
            CopyPackageDestinationPaths = new() { { "5.3", "C:/TestDestination" } }
        };
        windowsConfig.CopyPackageDestinationPathsWithVersion.Add(UEVersion.UE_5_3, "C:/TestDestination");
        
        var builder = new TestableWindowsBuilder(
            config, windowsConfig, _macConfig,
            _mockUnrealRunner.Object, _mockFileSystem.Object,
            _mockTestResultsEvaluator.Object, _mockLogger.Object)
        {
            BuildAsyncResult = true,
            PackageBuildAsyncResult = true
        };
        
        // Setup package build to fail
        _mockTestResultsEvaluator.Setup(e => e.EvaluatePackageBuildResults(It.IsAny<string>(), It.IsAny<EditorPlatform>(), It.IsAny<UEVersion>()))
            .Returns(BuildStatus.Failed);
        
        const UEVersion version = UEVersion.UE_5_3;
        const string baseRepo = "C:/tmp/TestRepoBase";
        await builder.PrepareRepository(baseRepo, version);
        builder.InitDirectory(version);
        
        var result = await builder.Run(version);
        
        Assert.Equal(BuildStatus.Failed, result.StatusOfPackage);
        
        // Verify no copy operations were performed (only PrepareRepository copy)
        _mockFileSystem.Verify(f => f.CopyDirectory(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}