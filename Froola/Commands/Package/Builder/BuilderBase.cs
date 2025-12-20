using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Froola.Commands.Plugin;
using Froola.Configs;
using Froola.Interfaces;
using Froola.Utils;

namespace Froola.Commands.Package.Builder;

/// <summary>
/// Abstract base class for platform-specific project builders. Handles directory and repository setup.
/// </summary>
public abstract class BuilderBase(
    PackageConfig packageConfig,
    WindowsConfig windowsConfig,
    MacConfig macConfig,
    IFroolaLogger logger,
    IFileSystem fileSystem)
{
    public string RepositoryPath { get; protected set; } = null!;

    protected string PackageDir = null!;
    protected string ProjectFilePath = null!;
    protected string UeDirectoryPath = null!;
    protected string EditorPath = null!;
    protected string BuildBatPath = null!;
    protected string RunUatBatPath = null!;

    /// <summary>
    /// Prepares the repository for building and sets up input parameters.
    /// </summary>
    public abstract Task PrepareRepository(string baseRepositoryPath, UEVersion engineVersion);

    /// <summary>
    /// Initializes build and package directories for the specified engine version.
    /// </summary>
    public void InitDirectory(UEVersion engineVersion)
    {
        PackageDir = Path.Combine(packageConfig.ResultPath, "packages",
            $"{MyEditorPlatform}_{engineVersion.ToFullVersionString()}");
        if (!fileSystem.DirectoryExists(PackageDir))
        {
            fileSystem.CreateDirectory(PackageDir);
        }

        ProjectFilePath =
            UECommandsHelper.GetUprojectPath(RepositoryPath, packageConfig.ProjectName, MyEditorPlatform);

        UeDirectoryPath =
            UECommandsHelper.GetUeDirectoryPath(windowsConfig, macConfig, engineVersion, MyEditorPlatform);

        EditorPath = UECommandsHelper.GetUnrealEditorPath(UeDirectoryPath, MyEditorPlatform);

        BuildBatPath = UECommandsHelper.GetBuildScriptPath(UeDirectoryPath, MyEditorPlatform);

        RunUatBatPath = UECommandsHelper.GetRunUatScriptPath(UeDirectoryPath, MyEditorPlatform);

        logger.LogInformation("----------------------------------------------");
        logger.LogInformation($"Package directory: {PackageDir}");
        logger.LogInformation($"Project file path: {ProjectFilePath}");
        logger.LogInformation($"Unreal Engine directory: {UeDirectoryPath}");
        logger.LogInformation($"Unreal Editor path: {EditorPath}");
        logger.LogInformation($"Build script path: {BuildBatPath}");
        logger.LogInformation($"Run UAT script path: {RunUatBatPath}");
        logger.LogInformation("----------------------------------------------");
    }

    /// <summary>
    /// Gets the game platforms for the builder implementation.
    /// </summary>
    protected abstract List<GamePlatform> MyPlatform();

    /// <summary>
    /// Gets the editor platform for the builder implementation.
    /// </summary>
    protected abstract EditorPlatform MyEditorPlatform { get; }

    /// <summary>
    /// Zips the generated package directory.
    /// </summary>
    protected void ZipPackage(UEVersion engineVersion)
    {
        if (!packageConfig.IsZipped)
        {
            return;
        }

        var zipPackageName = string.IsNullOrEmpty(packageConfig.ZipPackageName)
            ? packageConfig.ProjectName
            : packageConfig.ZipPackageName;

        var zipFileName = $"{zipPackageName}_UE{engineVersion.ToVersionString()}_{MyEditorPlatform}.zip";
        var zipFilePath = Path.Combine(packageConfig.ResultPath, "releases", zipFileName);

        var releasesDir = Path.GetDirectoryName(zipFilePath)!;
        if (!fileSystem.DirectoryExists(releasesDir))
        {
            fileSystem.CreateDirectory(releasesDir);
        }

        if (fileSystem.FileExists(zipFilePath))
        {
            fileSystem.DeleteFile(zipFilePath);
        }

        var sourceDir = Path.Combine(PackageDir, "Project");
        if (!fileSystem.DirectoryExists(sourceDir))
        {
            logger.LogWarning($"Source directory for zipping not found: {sourceDir}");
            return;
        }

        logger.LogInformation($"Zipping package from {sourceDir} to {zipFilePath}");
        fileSystem.ZipDirectory(sourceDir, zipFilePath);
    }

    /// <summary>
    /// Checks the project package build result for the specified engine version.
    /// </summary>
    protected BuildStatus CheckProjectPackageBuildResult(UEVersion engineVersion, string projectName)
    {
        // For project packaging, we check if the executable or some manifest exists in the output directory.
        // This is a simplified check.
        return fileSystem.DirectoryExists(Path.Combine(PackageDir, "Project")) 
            ? BuildStatus.Success 
            : BuildStatus.Failed;
    }
}
