using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Froola.Configs;
using Froola.Interfaces;
using Froola.Utils;

namespace Froola.Commands.Plugin.Builder;

/// <summary>
/// Linux test implementation using Docker for building, testing, and packaging plugins.
/// </summary>
public class LinuxBuilder(
    PluginConfig pluginConfig,
    WindowsConfig windowsConfig,
    MacConfig macConfig,
    LinuxConfig linuxConfig,
    IDockerRunner dockerRunner,
    IFileSystem fileSystem,
    ITestResultsEvaluator testResultsEvaluator,
    IFroolaLogger<LinuxBuilder> logger)
    : BuilderBase(pluginConfig, windowsConfig, macConfig, logger, fileSystem, testResultsEvaluator), ILinuxBuilder
{
    /// <summary>
    /// Gets the editor platform for this builder (Linux).
    /// </summary>
    protected override EditorPlatform MyEditorPlatform => EditorPlatform.Linux;

    private const string PROJECT_DIR_IN_DOCKER = "/home/ue4/project";

    private const string UePluginsDirInDocker = "/home/ue4/UnrealEngine/Engine/Plugins/Marketplace" +
                                                "";
    private string _repoPathInWindows = "";
    private readonly PluginConfig _pluginConfig = pluginConfig;
    private readonly LinuxConfig _linuxConfig = linuxConfig;
    private readonly IFileSystem _fileSystem = fileSystem;


    /// <summary>
    /// Runs the Linux build, test, and packaging process for the specified engine version using Docker.
    /// </summary>
    /// <param name="engineVersion">Engine version string.</param>
    /// <returns>Build result for the specified engine version.</returns>
    public async Task<BuildResult> Run(UEVersion engineVersion)
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
            _fileSystem.CreateDirectory(Path.Combine(RepositoryPath, "TestResults"));
            _fileSystem.CreateDirectory(Path.Combine(RepositoryPath, "Packages"));

            if (!await dockerRunner.IsDockerReady())
            {
                logger.LogError(
                    $"{linuxConfig.DockerCommand} is not available. Please ensure {linuxConfig.DockerCommand} is installed and running.");
                return result;
            }

            result.StatusOfBuild = await BuildAsync(engineVersion) ? BuildStatus.Success : BuildStatus.Failed;
            if (result.StatusOfBuild == BuildStatus.Failed)
            {
                return result;
            }

            if (_pluginConfig.RunTest)
            {
                result.StatusOfTest = await TestAsync(engineVersion) ? BuildStatus.Success : BuildStatus.Failed;
            }

            if (_pluginConfig.RunPackage)
            {
                result.StatusOfPackage = await PackageBuildAsync(engineVersion)
                    ? BuildStatus.Success
                    : BuildStatus.Failed;

                // Copy package to destination if configured
                if (result.StatusOfPackage == BuildStatus.Success && _pluginConfig.CopyPackageAfterBuild)
                {
                    await CopyPackageToDestination(engineVersion);
                }
            }

            if (_pluginConfig.RunGamePackage)
            {
                result.StatusOfGamePackage = await BuildGamePackageAsync(engineVersion)
                    ? BuildStatus.Success
                    : BuildStatus.Failed;
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error running Linux test for UE5.{engineVersion}: {ex.Message}", ex);
            return result;
        }
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

            if (_pluginConfig.CopyPackageAfterBuild)
            {
                var pluginDestinationPath = GetEngineTargetPluginDirectory(engineVersion, true);
                if (_fileSystem.DirectoryExists(pluginDestinationPath))
                {
                    logger.LogInformation($"Removing existing plugin at: {pluginDestinationPath}");
                    _fileSystem.DeleteDirectory(pluginDestinationPath, true);
                }
            }
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
        return _pluginConfig.PackagePlatforms.Where(x => x is GamePlatform.Linux).ToList();
    }

    /// <summary>
    /// Builds the plugin asynchronously.
    /// </summary>
    /// <param name="engineVersion">Engine version string.</param>
    /// <returns>True if the build is successful, false otherwise.</returns>
    protected virtual async Task<bool> BuildAsync(UEVersion engineVersion)
    {
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

        var command = $"{BuildBatPath} {BuildProjectArgs}";

        logger.LogInformation($"Running Docker with image: {dockerImage}");

        try
        {
            await using var writer = new StreamWriter(Path.Combine(BuildResultDir, "Build.log"));

            await foreach (var logLine in dockerRunner.RunContainer(dockerImage, command, RepositoryPath,
                               volumeMappings, _pluginConfig.EnvironmentVariableMap))
            {
                logger.LogInformation($"{logLine}");
                await writer.WriteLineAsync(logLine);
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Docker error: {ex.Message}", ex);
        }

        logger.LogInformation("Build finished");

        return true;
    }

    /// <summary>
    /// Tests the plugin asynchronously.
    /// </summary>
    /// <param name="engineVersion">Engine version string.</param>
    /// <returns>True if the test is successful, false otherwise.</returns>
    protected virtual async Task<bool> TestAsync(UEVersion engineVersion)
    {
        var testResultDirInDocker = Path.Combine(PROJECT_DIR_IN_DOCKER, "TestResults").Replace("\\", "/");
        
        var testCommandArgs =
            UECommandsHelper.GetAutomationTestCommandArgs(ProjectFilePath, _pluginConfig.PluginName,
                testResultDirInDocker, EditorPlatform.Linux);

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

        var command = $"\"{EditorPath}\" {testCommandArgs.Replace('\"', '\'')}";

        logger.LogInformation($"Running Docker with image: {dockerImage}");

        try
        {
            await using var writer = new StreamWriter(Path.Combine(TestResultDir, "AutomationTest.log"));

            await foreach (var logLine in dockerRunner.RunContainer(dockerImage, command, RepositoryPath,
                               volumeMappings, _pluginConfig.EnvironmentVariableMap))
            {
                logger.LogInformation($"{logLine}");
                await writer.WriteLineAsync(logLine);
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Docker error: {ex.Message}", ex);
        }

        logger.LogInformation("Test finished");

        // Copy test results generated by Docker container
        var dockerTestResultsPath = Path.Combine(_repoPathInWindows, "TestResults");
        if (_fileSystem.DirectoryExists(dockerTestResultsPath))
        {
            _fileSystem.CopyDirectory(dockerTestResultsPath, TestResultDir);
        }

        var statusOfTest = CheckTestResult(engineVersion) == BuildStatus.Success;
        logger.LogInformation(
            $"Test result: {(statusOfTest ? "SUCCESS" : "FAILURE")}");

        return statusOfTest;
    }

    /// <inheritdoc />
    public async Task<bool> BuildGamePackageAsync(UEVersion engineVersion)
    {
        var gamePackageOutputDirInDocker = Path.Combine(PROJECT_DIR_IN_DOCKER, "GamePackage").Replace("\\", "/");
        var projectFilePathInDocker = Path.Combine(PROJECT_DIR_IN_DOCKER, $"{_pluginConfig.ProjectName}.uproject").Replace("\\", "/");
        var targetPlatform = GamePlatform.Linux;
        var buildCookRunArgs = UECommandsHelper.GetBuildCookRunArgs(projectFilePathInDocker, gamePackageOutputDirInDocker, targetPlatform, EditorPlatform.Linux);

        var volumeMappings = new Dictionary<string, string>
        {
            { _repoPathInWindows, PROJECT_DIR_IN_DOCKER }
        };

        if (linuxConfig.CopyPluginsToDocker)
        {
            var pluginSourcePath = GetEngineTargetPluginDirectory(engineVersion, false);
            volumeMappings[pluginSourcePath] = UePluginsDirInDocker;
        }

        var dockerImage = GetDockerImageName(engineVersion).ToLowerInvariant();
        var command = $"\"{RunUatBatPath}\" {buildCookRunArgs}";

        try
        {
            await using var writer = new StreamWriter(Path.Combine(PackageDir, "BuildGamePackage.log"));

            await foreach (var logLine in dockerRunner.RunContainer(dockerImage, command, RepositoryPath,
                               volumeMappings, _pluginConfig.EnvironmentVariableMap))
            {
                logger.LogInformation($"{logLine}");
                await writer.WriteLineAsync(logLine);
            }

            logger.LogInformation("Game packaging completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError($"Docker error during game packaging: {ex.Message}", ex);
            return false;
        }

        var dockerPackagePath = Path.Combine(_repoPathInWindows, "GamePackage");
        if (_fileSystem.DirectoryExists(dockerPackagePath))
        {
            var localOutputDir = GameDir;
            _fileSystem.CreateDirectory(localOutputDir);
            _fileSystem.CopyDirectory(dockerPackagePath, localOutputDir);
        }

        return true;
    }

    /// <summary>
    /// Builds the plugin package asynchronously.
    /// </summary>
    /// <param name="engineVersion">Engine version string.</param>
    /// <returns>True if the package build is successful, false otherwise.</returns>
    protected virtual async Task<bool> PackageBuildAsync(UEVersion engineVersion)
    {
        var packageOutputDirInDocker = Path.Combine(PROJECT_DIR_IN_DOCKER, "packages").Replace("\\", "/");

        var buildPluginArgs = UECommandsHelper.GetBuildPluginArgs(RepositoryPath, _pluginConfig.PluginName,
            packageOutputDirInDocker, MyPlatform(), MyEditorPlatform);
        

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

        var command = $"\"{RunUatBatPath}\" {buildPluginArgs}";

        logger.LogInformation($"Running Docker with image: {dockerImage}");

        try
        {
            await using var writer = new StreamWriter(Path.Combine(PackageDir, "BuildPlugin.log"));

            await foreach (var logLine in dockerRunner.RunContainer(dockerImage, command, RepositoryPath,
                               volumeMappings, _pluginConfig.EnvironmentVariableMap))
            {
                logger.LogInformation($"{logLine}");
                await writer.WriteLineAsync(logLine);
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Docker error: {ex.Message}", ex);
        }

        logger.LogInformation("Package finished");

        var dockerPackagePath = Path.Combine(_repoPathInWindows, "packages");
        if (_fileSystem.DirectoryExists(dockerPackagePath))
        {
            _fileSystem.CopyDirectory(dockerPackagePath, PackageDir);
        }

        var statusOfPackage = CheckPackageBuildResult(engineVersion) == BuildStatus.Success;
        logger.LogInformation(
            $"Package build result: {(statusOfPackage ? "SUCCESS" : "FAILURE")}");

        return statusOfPackage;
    }

    /// <summary>
    ///     Copies the packaged plugin to the configured destination path for use with future Docker builds
    /// </summary>
    /// <param name="engineVersion">Engine version</param>
    private async Task CopyPackageToDestination(UEVersion engineVersion)
    {
        try
        {
            var pluginDestinationPath = GetEngineTargetPluginDirectory(engineVersion, true);

            // Find the packaged plugin directory in the local PackageDir (already copied from Docker)
            var localPackagedPluginDir = Path.Combine(PackageDir, "Plugin");
            if (!_fileSystem.DirectoryExists(localPackagedPluginDir))
            {
                logger.LogWarning($"Packaged plugin directory not found locally: {localPackagedPluginDir}");
                return;
            }

            // Remove existing plugin if it exists
            if (_fileSystem.DirectoryExists(pluginDestinationPath))
            {
                _fileSystem.DeleteDirectory(pluginDestinationPath, true);
                logger.LogInformation($"Removed existing plugin at: {pluginDestinationPath}");
            }

            _fileSystem.CreateDirectory(pluginDestinationPath);

            // Copy the packaged plugin to the staging area
            _fileSystem.CopyDirectory(localPackagedPluginDir, pluginDestinationPath);
            logger.LogInformation($"Successfully copied packaged plugin from {localPackagedPluginDir} to {pluginDestinationPath}");
            
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to copy packaged plugin: {ex.Message}", ex);
        }

        await Task.CompletedTask;
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

        return Path.Combine(destinationPath, _pluginConfig.PluginName);
    }
}