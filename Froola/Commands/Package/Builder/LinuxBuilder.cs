using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Froola.Commands.Plugin;
using Froola.Configs;
using Froola.Interfaces;
using Froola.Utils;

namespace Froola.Commands.Package.Builder;

/// <summary>
/// Linux project packaging implementation using Docker.
/// </summary>
public class LinuxBuilder(
    PackageConfig packageConfig,
    WindowsConfig windowsConfig,
    MacConfig macConfig,
    LinuxConfig linuxConfig,
    IDockerRunner dockerRunner,
    IFileSystem fileSystem,
    IFroolaLogger<LinuxBuilder> logger)
    : BuilderBase(packageConfig, windowsConfig, macConfig, logger, fileSystem), ILinuxBuilder
{
    /// <summary>
    /// Gets the editor platform for this builder (Linux).
    /// </summary>
    protected override EditorPlatform MyEditorPlatform => EditorPlatform.Linux;

    private const string PROJECT_DIR_IN_DOCKER = "/home/ue4/project";

    private const string UePluginsDirInDocker = "/home/ue4/UnrealEngine/Engine/Plugins/Marketplace";
    private string _repoPathInWindows = "";
    private readonly PackageConfig _packageConfig = packageConfig;
    private readonly LinuxConfig _linuxConfig = linuxConfig;
    private readonly IFileSystem _fileSystem = fileSystem;

    /// <summary>
    /// Not implemented for project-only builder.
    /// </summary>
    public Task<BuildResult> Run(UEVersion engineVersion)
    {
        throw new NotImplementedException("Run is not used for project packaging. Use RunPackage instead.");
    }

    /// <summary>
    /// Runs the Linux project packaging process for the specified engine version using Docker.
    /// </summary>
    public async Task<BuildResult> RunPackage(UEVersion engineVersion)
    {
        var result = new BuildResult
        {
            Os = EditorPlatform.Linux,
            EngineVersion = engineVersion,
            StatusOfBuild = BuildStatus.None,
            StatusOfTest = BuildStatus.None,
            StatusOfPackage = BuildStatus.None
        };

        try
        {
            if (!await dockerRunner.IsDockerReady())
            {
                logger.LogError(
                    $"{linuxConfig.DockerCommand} is not available. Please ensure {linuxConfig.DockerCommand} is installed and running.");
                return result;
            }

            var packageOutputDirInDocker = Path.Combine(PROJECT_DIR_IN_DOCKER, "packages").Replace("\\", "/");

            var buildCookRunArgs = UECommandsHelper.GetBuildCookRunArgs(ProjectFilePath, packageOutputDirInDocker,
                MyPlatform(), MyEditorPlatform);

            var volumeMappings = new Dictionary<string, string>
            {
                { _repoPathInWindows, PROJECT_DIR_IN_DOCKER }
            };

            // Add plugins volume mapping if plugins were prepared
            if (linuxConfig.CopyPluginsToDocker)
            {
                var pluginSourcePath = GetEngineTargetPluginDirectory(engineVersion, false);
                volumeMappings[pluginSourcePath] = UePluginsDirInDocker;
            }

            var dockerImage = GetDockerImageName(engineVersion).ToLowerInvariant();

            var command = $"\"{RunUatBatPath}\" {buildCookRunArgs}";

            logger.LogInformation($"Running Docker with image: {dockerImage} for project packaging");

            try
            {
                await using var writer = new StreamWriter(Path.Combine(PackageDir, "BuildCookRun.log"));

                await foreach (var logLine in dockerRunner.RunContainer(dockerImage, command, RepositoryPath,
                                   volumeMappings, _packageConfig.EnvironmentVariableMap))
                {
                    logger.LogInformation($"{logLine}");
                    await writer.WriteLineAsync(logLine);
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Docker error: {ex.Message}", ex);
            }

            logger.LogInformation("Project packaging finished");

            var dockerPackagePath = Path.Combine(_repoPathInWindows, "packages");
            if (_fileSystem.DirectoryExists(dockerPackagePath))
            {
                _fileSystem.CopyDirectory(dockerPackagePath, PackageDir);
            }

            result.StatusOfPackage = CheckProjectPackageBuildResult(engineVersion, _packageConfig.ProjectName);

            if (result.StatusOfPackage == BuildStatus.Success)
            {
                ZipPackage(engineVersion);
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error running Linux project packaging for UE5.{engineVersion}: {ex.Message}", ex);
            result.StatusOfPackage = BuildStatus.Failed;
        }

        return result;
    }

    /// <inheritdoc cref="IBuilder" />
    public override Task PrepareRepository(string baseRepositoryPath, UEVersion engineVersion)
    {
        _repoPathInWindows = Path.Combine(baseRepositoryPath, "..", "Linux");

        try
        {
            if (!_fileSystem.DirectoryExists(_repoPathInWindows))
            {
                _fileSystem.CreateDirectory(_repoPathInWindows);
            }

            // Copy all files from source (tempDir) to Linux directory (local Docker volume)
            logger.LogInformation(
                $"Copying files from {baseRepositoryPath} to Linux Docker volume at {_repoPathInWindows}");
            _fileSystem.CopyDirectory(baseRepositoryPath, _repoPathInWindows);

            logger.LogInformation($"Linux repository prepared at (Docker volume): {_repoPathInWindows}");
        }
        catch (Exception ex)
        {
            logger.LogError($"Error preparing Linux repository: {ex.Message}", ex);
        }

        RepositoryPath = PROJECT_DIR_IN_DOCKER;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleans up the temporary directory on Linux.
    /// </summary>
    public Task CleanupTempDirectory()
    {
        if (string.IsNullOrEmpty(RepositoryPath))
        {
            return Task.CompletedTask;
        }

        logger.LogInformation($"Cleaning up Linux temporary directory: {RepositoryPath}");

        try
        {
            if (_fileSystem.DirectoryExists(RepositoryPath))
            {
                _fileSystem.DeleteDirectory(RepositoryPath, true);
                logger.LogInformation($"Successfully deleted Linux temporary directory: {RepositoryPath}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error cleaning up Linux directory: {ex.Message}", ex);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the Docker image name based on the engine version.
    /// </summary>
    /// <param name="engineVersion">Engine version string.</param>
    /// <returns>Corresponding Docker image name.</returns>
    private string GetDockerImageName(UEVersion engineVersion)
    {
        return linuxConfig.DockerImage.Replace("%v", engineVersion.ToVersionString());
    }

    /// <summary>
    /// Gets the game platforms for this builder.
    /// </summary>
    /// <returns>List of game platforms.</returns>
    protected override List<GamePlatform> MyPlatform()
    {
        return _packageConfig.PackagePlatforms.Where(x => x is GamePlatform.Linux).ToList();
    }

    private string GetEngineTargetPluginDirectory(UEVersion engineVersion, bool addMyPluginNameFolder)
    {
        // Get version-specific destination path or fall back to default
        var destinationPath = Path.Combine(_linuxConfig.DockerPluginsSourcePath, engineVersion.ToVersionString());

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            logger.LogWarning("No destination path configured for plugin copy, skipping copy operation");
            return string.Empty;
        }

        if (!Path.IsPathRooted(destinationPath))
        {
            destinationPath = Path.GetFullPath(destinationPath);
        }

        if (!addMyPluginNameFolder)
        {
            return destinationPath;
        }

        // We don't have a plugin name in project packaging, but this method is kept for compatibility with volume mapping logic if needed
        return Path.Combine(destinationPath, "ProjectPlugins");
    }
}
