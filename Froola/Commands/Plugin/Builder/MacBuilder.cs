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
/// Mac test implementation for building, testing, and packaging plugins using macOS environment.
/// </summary>
public class MacBuilder(
    PluginConfig pluginConfig,
    WindowsConfig windowsConfig,
    MacConfig macConfig,
    IMacUnrealEngineRunner macUeRunner,
    IFileSystem fileSystem,
    ITestResultsEvaluator testResultsEvaluator,
    IFroolaLogger<MacBuilder> logger)
    : BuilderBase(pluginConfig, windowsConfig, macConfig, logger, fileSystem, testResultsEvaluator),
        IMacBuilder
{
    private readonly PluginConfig _pluginConfig = pluginConfig;
    private readonly MacConfig _macConfig = macConfig;

    private bool _isReady;

    /// <summary>
    /// Gets the editor platform for this builder (Mac).
    /// </summary>
    protected override EditorPlatform MyEditorPlatform => EditorPlatform.Mac;

    /// <summary>
    /// Runs the Mac build, test, and packaging process for the specified engine version.
    /// </summary>
    /// <param name="engineVersion">Engine version string.</param>
    /// <returns>Build result for the specified engine version.</returns>
    public async Task<BuildResult> Run(UEVersion engineVersion)
    {
        var result = new BuildResult
        {
            Os = EditorPlatform.Mac,
            EngineVersion = engineVersion,
            StatusOfBuild = BuildStatus.None,
            StatusOfTest = BuildStatus.None,
            StatusOfPackage = BuildStatus.None
        };

        if (!_isReady)
        {
            logger.LogError("Mac builder is not ready");
            return result;
        }

        try
        {
            var projectFileExists = await macUeRunner.FileExists(ProjectFilePath);

            if (!projectFileExists)
            {
                logger.LogInformation($"Project file does not exist: {ProjectFilePath}");
                return result;
            }

            logger.LogInformation($"Found project file: {ProjectFilePath}");

            var macResultDir = $"{RepositoryPath}/TestResults";

            await macUeRunner.MakeDirectory(macResultDir);
            logger.LogInformation($"Created results directory: {macResultDir}");

            var originalXcodePath = string.Empty;
            var xcodeSwitched = false;
            if (_macConfig.XcodeNamesWithVersion.TryGetValue(engineVersion, out var xcodeName) &&
                !string.IsNullOrEmpty(xcodeName))
            {
                logger.LogInformation($"Switching Xcode: {xcodeName}");
                originalXcodePath = await macUeRunner.SwitchXcode(xcodeName);
                xcodeSwitched = true;
            }
            else
            {
                logger.LogWarning(
                    "Xcode name for this engine version not found. Skipping xcode-select.");
            }

            try
            {
                result.StatusOfBuild = await BuildAsync() ? BuildStatus.Success : BuildStatus.Failed;
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
                }

                return result;
            }
            finally
            {
                // Restore Xcode to original
                if (xcodeSwitched && !string.IsNullOrEmpty(originalXcodePath))
                {
                    logger.LogInformation($"Restoring original Xcode: {originalXcodePath}");
                    await macUeRunner.SwitchXcode(originalXcodePath);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error during test execution: {ex.Message}", ex);

            return result;
        }
    }

    /// <inheritdoc cref="IBuilder" />
    public override async Task PrepareRepository(string baseRepositoryPath, UEVersion engineVersion)
    {
        _isReady = false;

        RepositoryPath =
            $"/tmp/{_pluginConfig.PluginName}/{DateTime.Now:yyyyMMdd_HHmmss}/{engineVersion.ToVersionString()}";

        try
        {
            // Check if the remote directory exists and create it if needed
            var dirCheckResult = await macUeRunner.DirectoryExists(RepositoryPath);
            if (!dirCheckResult)
            {
                await macUeRunner.MakeDirectory(RepositoryPath);
                logger.LogInformation($"Created Mac repository directory: {RepositoryPath}");
            }

            // Upload the entire directory from Windows local to Mac repository at once
            logger.LogInformation(
                $"Uploading entire directory from {baseRepositoryPath} to Mac repository at {RepositoryPath}");

            // Upload the entire directory
            var uploadSuccess = await macUeRunner.UploadDirectory(baseRepositoryPath, RepositoryPath);
            if (uploadSuccess)
            {
                logger.LogInformation($"Successfully uploaded repository to Mac at {RepositoryPath}");
            }
            else
            {
                logger.LogError("Failed to upload repository to Mac");
            }

            logger.LogInformation($"Mac repository prepared at: {RepositoryPath}");

            if (_pluginConfig.CopyPackageAfterBuild)
            {
                var pluginDestinationPath = GetEngineTargetPluginDirectory(engineVersion);
                if (await macUeRunner.DirectoryExists(pluginDestinationPath))
                {
                    await macUeRunner.DeleteDirectory(pluginDestinationPath);
                    logger.LogInformation($"Removed existing plugin at: {pluginDestinationPath}");
                }
            }

            _isReady = true;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error preparing Mac repository: {ex.Message}", ex);
        }
    }


    /// <inheritdoc cref="IBuilder" />
    public async Task CleanupTempDirectory()
    {
        if (string.IsNullOrEmpty(RepositoryPath))
        {
            return;
        }

        logger.LogInformation($"Cleaning up Mac temporary directory: {RepositoryPath}");

        try
        {
            var deleted = await macUeRunner.DeleteDirectory(RepositoryPath);
            if (deleted)
            {
                logger.LogInformation($"Successfully deleted Mac temporary directory: {RepositoryPath}");
            }
            else
            {
                logger.LogError($"Failed to delete Mac temporary directory: {RepositoryPath}");
            }

            (macUeRunner as IDisposable)?.Dispose();
        }
        catch (Exception ex)
        {
            logger.LogError($"Error cleaning up Mac directory: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the game platforms for this builder.
    /// </summary>
    /// <returns>List of game platforms.</returns>
    protected override List<GamePlatform> MyPlatform()
    {
        return _pluginConfig.PackagePlatforms.Where(x => x is GamePlatform.Mac or GamePlatform.IOS).ToList();
    }


    /// <summary>
    /// Builds the plugin asynchronously for Mac.
    /// </summary>
    /// <returns>True if the build is successful, false otherwise.</returns>
    protected virtual async Task<bool> BuildAsync()
    {
        logger.LogInformation("Executing test script...");

        var command = $"\"{BuildBatPath}\" {BuildProjectArgs}";

        await using var writer = new StreamWriter(Path.Combine(BuildResultDir, "Build.log"));

        await foreach (var logLine in macUeRunner.ExecuteRemoteScriptWithLogsAsync(command,
                           _pluginConfig.EnvironmentVariableMap))
        {
            logger.LogInformation(logLine);
            await writer.WriteLineAsync(logLine);
        }

        logger.LogInformation("Build finished");

        return true;
    }

    /// <summary>
    /// Tests the plugin asynchronously for Mac.
    /// </summary>
    /// <param name="engineVersion">Engine version</param>
    /// <returns>True if the test is successful, false otherwise.</returns>
    protected virtual async Task<bool> TestAsync(UEVersion engineVersion)
    {
        var macResultDir = $"{RepositoryPath}/TestResults";

        var testCommandArgs =
            UECommandsHelper.GetAutomationTestCommandArgs(ProjectFilePath, _pluginConfig.PluginName,
                macResultDir, EditorPlatform.Mac);

        logger.LogInformation("Executing test script...");

        await using var writer = new StreamWriter(Path.Combine(TestResultDir, "AutomationTest.log"));

        var command = $"\"{EditorPath}\" {testCommandArgs}";

        await foreach (var logLine in macUeRunner.ExecuteRemoteScriptWithLogsAsync(command,
                           _pluginConfig.EnvironmentVariableMap))
        {
            logger.LogInformation(logLine);
            await writer.WriteLineAsync(logLine);
        }

        logger.LogInformation("Test finished");

        logger.LogInformation(
            $"Downloading test results from {macResultDir} to {TestResultDir}");

        var downloadSuccess = await macUeRunner.DownloadDirectory(macResultDir, TestResultDir);

        logger.LogInformation(downloadSuccess
            ? $"Successfully downloaded test results to {TestResultDir}"
            : $"Failed to download test results from {macResultDir}");

        var statusOfTest = CheckTestResult(engineVersion) == BuildStatus.Success;
        logger.LogInformation(
            $"Test result: {(statusOfTest ? "SUCCESS" : "FAILURE")}");

        return statusOfTest;
    }

    /// <summary>
    /// Builds the plugin package asynchronously for Mac.
    /// </summary>
    /// <param name="engineVersion">Engine version</param>
    /// <returns>True if the package build is successful, false otherwise.</returns>
    protected virtual async Task<bool> PackageBuildAsync(UEVersion engineVersion)
    {
        var macPackagePath = Path.Combine(RepositoryPath, "packages").Replace("\\", "/");
        if (!await macUeRunner.DirectoryExists(macPackagePath))
        {
            await macUeRunner.MakeDirectory(macPackagePath);
        }

        var buildPluginArgs = UECommandsHelper.GetBuildPluginArgs(RepositoryPath, _pluginConfig.PluginName,
            macPackagePath, MyPlatform(), MyEditorPlatform);


        logger.LogInformation("Executing package script...");

        var command = $"\"{RunUatBatPath}\" {buildPluginArgs}";

        await using var writer = new StreamWriter(Path.Combine(PackageDir, "BuildPlugin.log"));

        await foreach (var logLine in macUeRunner.ExecuteRemoteScriptWithLogsAsync(command,
                           _pluginConfig.EnvironmentVariableMap))
        {
            logger.LogInformation(logLine);
            await writer.WriteLineAsync(logLine);
        }

        logger.LogInformation("Packaging finished");

        var downloadSuccess = await macUeRunner.DownloadDirectory(macPackagePath, PackageDir);

        logger.LogInformation(downloadSuccess
            ? $"Successfully downloaded package to {PackageDir}"
            : $"Failed to download package from {macPackagePath}");

        var statusOfPackage = CheckPackageBuildResult(engineVersion) == BuildStatus.Success;
        logger.LogInformation(
            $"Package build result: {(statusOfPackage ? "SUCCESS" : "FAILURE")}");

        if (!statusOfPackage || !_pluginConfig.CopyPackageAfterBuild)
        {
            return statusOfPackage;
        }

        await CopyPackageToDestination(engineVersion, macPackagePath);

        return statusOfPackage;
    }

    private async Task CopyPackageToDestination(UEVersion engineVersion, string macPackagePath)
    {
        try
        {
            var pluginDestinationPath = GetEngineTargetPluginDirectory(engineVersion);

            // Check if the packaged plugin exists in the local PackageDir
            var localPackagedPluginDir = $"{macPackagePath}/Plugin";
            if (!await macUeRunner.DirectoryExists(localPackagedPluginDir))
            {
                logger.LogWarning($"Packaged plugin directory not found locally: {localPackagedPluginDir}");
                return;
            }

            // Remove existing plugin if it exists
            if (await macUeRunner.DirectoryExists(pluginDestinationPath))
            {
                await macUeRunner.DeleteDirectory(pluginDestinationPath);
                logger.LogInformation($"Removed existing plugin at: {pluginDestinationPath}");
            }

            await macUeRunner.MakeDirectory(pluginDestinationPath);

            // Copy the packaged plugin from local PackageDir to destination
            await macUeRunner.CopyDirectory(localPackagedPluginDir, pluginDestinationPath);

            logger.LogInformation(
                $"Successfully copied packaged plugin from {localPackagedPluginDir} to {pluginDestinationPath}");
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to copy packaged plugin: {ex.Message}", ex);
        }

        await Task.CompletedTask;
    }

    private string GetEngineTargetPluginDirectory(UEVersion engineVersion)
    {
        // Get version-specific destination path or fall back to default
        return
            $"{_macConfig.MacUnrealBasePath}/UE_{engineVersion.ToVersionString()}/Engine/Plugins/Marketplace/{_pluginConfig.PluginName}";
    }
}