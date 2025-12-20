using Froola.Commands.Package.Builder;
using Froola.Commands.Plugin;
using Froola.Configs;
using Froola.Interfaces;
using Moq;

namespace Froola.Tests.Commands.Package.Builder;

public class WindowsBuilderTests
{
    private readonly Mock<IUnrealEngineRunner> _mockUnrealRunner = new();
    private readonly Mock<IFileSystem> _mockFileSystem = new();
    private readonly Mock<IFroolaLogger<WindowsBuilder>> _mockLogger = new();

    private readonly PackageConfig _packageConfig = new()
    {
        ProjectName = "TestProject",
        ResultPath = "C:/tmp",
        EditorPlatforms = { EditorPlatform.Windows },
        EngineVersions = { UEVersion.UE_5_3 },
        PackagePlatforms = { GamePlatform.Win64 }
    };

    private readonly WindowsConfig _windowsConfig = new();
    private readonly MacConfig _macConfig = new();

    private WindowsBuilder CreateBuilder()
    {
        return new WindowsBuilder(
            _packageConfig, _windowsConfig, _macConfig,
            _mockUnrealRunner.Object, _mockFileSystem.Object, _mockLogger.Object);
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
        _mockFileSystem.Verify(f => f.CreateDirectory(It.IsAny<string>()), Times.Once); // Only PackageDir
    }

    [Fact]
    public async Task RunPackage_CallsRunBuildScript()
    {
        var builder = CreateBuilder();
        const UEVersion version = UEVersion.UE_5_3;
        const string baseRepo = "C:/tmp/TestRepoBase";
        await builder.PrepareRepository(baseRepo, version);
        builder.InitDirectory(version);

        _mockFileSystem.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);

        var result = await builder.RunPackage(version);

        Assert.Equal(BuildStatus.Success, result.StatusOfPackage);
        _mockUnrealRunner.Verify(r => r.RunBuildScript(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()), Times.Once);
    }

    [Fact]
    public async Task RunPackage_WithIsZipped_CallsZipDirectory()
    {
        var builder = CreateBuilder();
        const UEVersion version = UEVersion.UE_5_3;
        const string baseRepo = "C:/tmp/TestRepoBase";
        await builder.PrepareRepository(baseRepo, version);
        builder.InitDirectory(version);

        _packageConfig.IsZipped = true;
        _packageConfig.ZipPackageName = "CustomZipName";
        _mockFileSystem.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);

        await builder.RunPackage(version);

        var expectedZipName = $"CustomZipName_UE5.3_Windows.zip";
        _mockFileSystem.Verify(f => f.ZipDirectory(It.IsAny<string>(), It.Is<string>(s => s.Contains(expectedZipName))), Times.Once);
    }

    [Fact]
    public async Task RunPackage_WithoutZipPackageName_UsesProjectNameForZip()
    {
        var builder = CreateBuilder();
        const UEVersion version = UEVersion.UE_5_3;
        const string baseRepo = "C:/tmp/TestRepoBase";
        await builder.PrepareRepository(baseRepo, version);
        builder.InitDirectory(version);

        _packageConfig.IsZipped = true;
        _packageConfig.ZipPackageName = string.Empty;
        _mockFileSystem.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);

        await builder.RunPackage(version);

        var expectedZipName = $"{_packageConfig.ProjectName}_UE5.3_Windows.zip";
        _mockFileSystem.Verify(f => f.ZipDirectory(It.IsAny<string>(), It.Is<string>(s => s.Contains(expectedZipName))), Times.Once);
    }
}
