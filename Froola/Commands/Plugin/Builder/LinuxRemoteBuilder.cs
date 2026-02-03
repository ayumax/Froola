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
/// Linux remote implementation for building, testing, and packaging plugins using a Linux machine via SSH.
/// </summary>
public class LinuxRemoteBuilder(
    PluginConfig pluginConfig,
    WindowsConfig windowsConfig,
    MacConfig macConfig,
    LinuxConfig linuxConfig,
    ILinuxUnrealEngineRunner linuxUeRunner,
    IFileSystem fileSystem,
    ITestResultsEvaluator testResultsEvaluator,
    IFroolaLogger<LinuxRemoteBuilder> logger)
    : BuilderBase(pluginConfig, windowsConfig, macConfig, linuxConfig, logger, fileSystem, testResultsEvaluator),
        ILinuxRemoteBuilder
{
    private readonly PluginConfig _pluginConfig = pluginConfig;

    private bool _isReady;

    protected override EditorPlatform MyEditorPlatform => EditorPlatform.Linux;

    public async Task<BuildResult> Run(UEVersion engineVersion)
    {
        var result = new BuildResult
        {
            Os = EditorPlatform.Linux,
            EngineVersion = engineVersion,
            StatusOfBuild = BuildStatus.None,
            StatusOfTest = BuildStatus.None,
            StatusOfPackage = BuildStatus.None,
            StatusOfPackagePreflight = BuildStatus.None
        };

        if (!_isReady)
        {
            logger.LogError("Linux remote builder is not ready");
            return result;
        }

        try
        {
            var projectFileExists = await linuxUeRunner.FileExists(ProjectFilePath);
            if (!projectFileExists)
            {
                logger.LogInformation($"Project file does not exist: {ProjectFilePath}");
                return result;
            }

            logger.LogInformation($"Found project file: {ProjectFilePath}");

            var linuxResultDir = $"{RepositoryPath}/TestResults";
            await linuxUeRunner.MakeDirectory(linuxResultDir);
            logger.LogInformation($"Created results directory: {linuxResultDir}");

            result.StatusOfBuild = await BuildAsync() ? BuildStatus.Success : BuildStatus.Failed;
            if (result.StatusOfBuild == BuildStatus.Failed)
            {
                return result;
            }

            if (_pluginConfig.RunTest)
            {
                result.StatusOfTest = await TestAsync(engineVersion) ? BuildStatus.Success : BuildStatus.Failed;
            }

            if (_pluginConfig.RunPackagePreflight)
            {
                result.StatusOfPackagePreflight = await PackagePreflightAsync(engineVersion)
                    ? BuildStatus.Success
                    : BuildStatus.Failed;

                if (result.StatusOfPackagePreflight == BuildStatus.Failed)
                {
                    return result;
                }
            }

            if (_pluginConfig.RunPackage)
            {
                result.StatusOfPackage = await PackageBuildAsync(engineVersion)
                    ? BuildStatus.Success
                    : BuildStatus.Failed;

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
            logger.LogError($"Error during Linux remote execution: {ex.Message}", ex);
            return result;
        }
    }

    protected virtual async Task<bool> PackagePreflightAsync(UEVersion engineVersion)
    {
        var preflightArgs = UECommandsHelper.GetBuildCookRunPreflightArgs(ProjectFilePath, GamePlatform.Linux,
            EditorPlatform.Linux);
        var command = $"\"{RunUatBatPath}\" {preflightArgs}";
        var logFilePath = Path.Combine(BuildResultDir, "PackagePreflight.log");

        await using (var writer = new StreamWriter(logFilePath))
        {
            await foreach (var logLine in linuxUeRunner.ExecuteRemoteScriptWithLogsAsync(command,
                               _pluginConfig.EnvironmentVariableMap))
            {
                logger.LogInformation(logLine);
                await writer.WriteLineAsync(logLine);
            }
        }

        logger.LogInformation("Package preflight finished");

        var logContent = await FileSystem.ReadAllTextAsync(logFilePath);
        if (!IsUatSuccessLog(logContent))
        {
            logger.LogError("Package preflight failed based on log analysis.");
            return false;
        }

        return true;
    }

    public override async Task PrepareRepository(string baseRepositoryPath, UEVersion engineVersion)
    {
        _isReady = false;

        RepositoryPath =
            $"/tmp/{_pluginConfig.PluginName}/{DateTime.Now:yyyyMMdd_HHmmss}/{engineVersion.ToVersionString()}";

        try
        {
            var dirCheckResult = await linuxUeRunner.DirectoryExists(RepositoryPath);
            if (!dirCheckResult)
            {
                await linuxUeRunner.MakeDirectory(RepositoryPath);
                logger.LogInformation($"Created Linux repository directory: {RepositoryPath}");
            }

            logger.LogInformation(
                $"Uploading entire directory from {baseRepositoryPath} to Linux repository at {RepositoryPath}");

            var uploadSuccess = await linuxUeRunner.UploadDirectory(baseRepositoryPath, RepositoryPath);
            if (uploadSuccess)
            {
                logger.LogInformation($"Successfully uploaded repository to Linux at {RepositoryPath}");
            }
            else
            {
                logger.LogError("Failed to upload repository to Linux");
            }

            logger.LogInformation($"Linux repository prepared at: {RepositoryPath}");

            if (_pluginConfig.CopyPackageAfterBuild)
            {
                var pluginDestinationPath = GetEngineTargetPluginDirectory(engineVersion);
                if (await linuxUeRunner.DirectoryExists(pluginDestinationPath))
                {
                    await linuxUeRunner.DeleteDirectory(pluginDestinationPath);
                    logger.LogInformation($"Removed existing plugin at: {pluginDestinationPath}");
                }
            }

            _isReady = true;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error preparing Linux repository: {ex.Message}", ex);
        }
    }

    public async Task CleanupTempDirectory()
    {
        if (string.IsNullOrEmpty(RepositoryPath))
        {
            return;
        }

        logger.LogInformation($"Cleaning up Linux temporary directory: {RepositoryPath}");

        try
        {
            var deleted = await linuxUeRunner.DeleteDirectory(RepositoryPath);
            if (deleted)
            {
                logger.LogInformation($"Successfully deleted Linux temporary directory: {RepositoryPath}");
            }
            else
            {
                logger.LogError($"Failed to delete Linux temporary directory: {RepositoryPath}");
            }

            (linuxUeRunner as IDisposable)?.Dispose();
        }
        catch (Exception ex)
        {
            logger.LogError($"Error cleaning up Linux directory: {ex.Message}", ex);
        }
    }

    protected override List<GamePlatform> MyPlatform()
    {
        return _pluginConfig.PackagePlatforms.Where(x => x is GamePlatform.Linux).ToList();
    }

    protected virtual async Task<bool> BuildAsync()
    {
        logger.LogInformation("Executing build script...");

        var command = $"\"{BuildBatPath}\" {BuildProjectArgs}";

        await using var writer = new StreamWriter(Path.Combine(BuildResultDir, "Build.log"));

        await foreach (var logLine in linuxUeRunner.ExecuteRemoteScriptWithLogsAsync(command,
                           _pluginConfig.EnvironmentVariableMap))
        {
            logger.LogInformation(logLine);
            await writer.WriteLineAsync(logLine);
        }

        logger.LogInformation("Build finished");

        return true;
    }

    protected virtual async Task<bool> TestAsync(UEVersion engineVersion)
    {
        var linuxResultDir = $"{RepositoryPath}/TestResults";

        var testCommandArgs =
            UECommandsHelper.GetAutomationTestCommandArgs(ProjectFilePath, _pluginConfig.PluginName,
                linuxResultDir, EditorPlatform.Linux);

        logger.LogInformation("Executing test script...");

        await using var writer = new StreamWriter(Path.Combine(TestResultDir, "AutomationTest.log"));

        var command = $"\"{EditorPath}\" {testCommandArgs}";

        await foreach (var logLine in linuxUeRunner.ExecuteRemoteScriptWithLogsAsync(command,
                           _pluginConfig.EnvironmentVariableMap))
        {
            logger.LogInformation(logLine);
            await writer.WriteLineAsync(logLine);
        }

        logger.LogInformation("Test finished");

        logger.LogInformation(
            $"Downloading test results from {linuxResultDir} to {TestResultDir}");

        var downloadSuccess = await linuxUeRunner.DownloadDirectory(linuxResultDir, TestResultDir);

        logger.LogInformation(downloadSuccess
            ? $"Successfully downloaded test results to {TestResultDir}"
            : $"Failed to download test results from {linuxResultDir}");

        var statusOfTest = CheckTestResult(engineVersion) == BuildStatus.Success;
        logger.LogInformation(
            $"Test result: {(statusOfTest ? "SUCCESS" : "FAILURE")}");

        return statusOfTest;
    }

    protected virtual async Task<bool> PackageBuildAsync(UEVersion engineVersion)
    {
        var linuxPackagePath = Path.Combine(RepositoryPath, "packages").Replace("\\", "/");
        if (!await linuxUeRunner.DirectoryExists(linuxPackagePath))
        {
            await linuxUeRunner.MakeDirectory(linuxPackagePath);
        }

        var buildPluginArgs = UECommandsHelper.GetBuildPluginArgs(RepositoryPath, _pluginConfig.PluginName,
            linuxPackagePath, MyPlatform(), MyEditorPlatform);

        logger.LogInformation("Executing package script...");

        var command = $"\"{RunUatBatPath}\" {buildPluginArgs}";

        await using var writer = new StreamWriter(Path.Combine(PackageDir, "BuildPlugin.log"));

        await foreach (var logLine in linuxUeRunner.ExecuteRemoteScriptWithLogsAsync(command,
                           _pluginConfig.EnvironmentVariableMap))
        {
            logger.LogInformation(logLine);
            await writer.WriteLineAsync(logLine);
        }

        logger.LogInformation("Packaging finished");

        var downloadSuccess = await linuxUeRunner.DownloadDirectory(linuxPackagePath, PackageDir);

        logger.LogInformation(downloadSuccess
            ? $"Successfully downloaded package to {PackageDir}"
            : $"Failed to download package from {linuxPackagePath}");

        var statusOfPackage = CheckPackageBuildResult(engineVersion) == BuildStatus.Success;
        logger.LogInformation(
            $"Package build result: {(statusOfPackage ? "SUCCESS" : "FAILURE")}");

        return statusOfPackage;
    }

    public async Task<bool> BuildGamePackageAsync(UEVersion engineVersion)
    {
        var remoteOutputDir = Path.Combine(RepositoryPath, "GamePackage").Replace("\\", "/");
        if (!await linuxUeRunner.DirectoryExists(remoteOutputDir))
        {
            await linuxUeRunner.MakeDirectory(remoteOutputDir);
        }

        var logFilePath = Path.Combine(GameDir, "BuildGamePackage.log");

        try
        {
            var buildCookRunArgs = UECommandsHelper.GetBuildCookRunArgs(ProjectFilePath, remoteOutputDir,
                GamePlatform.Linux, EditorPlatform.Linux);

            var command = $"\"{RunUatBatPath}\" {buildCookRunArgs}";

            await using var writer = new StreamWriter(logFilePath);

            await foreach (var logLine in linuxUeRunner.ExecuteRemoteScriptWithLogsAsync(command,
                               _pluginConfig.EnvironmentVariableMap))
            {
                logger.LogInformation(logLine);
                await writer.WriteLineAsync(logLine);
            }

            logger.LogInformation("Game packaging script execution finished.");
        }
        catch (Exception ex)
        {
            logger.LogError($"Game packaging failed: {ex.Message}");
            return false;
        }

        var logContent = await FileSystem.ReadAllTextAsync(logFilePath);
        if (logContent.Contains("AutomationTool exiting with ExitCode=0") || logContent.Contains("BUILD SUCCESSFUL"))
        {
            logger.LogInformation("Game packaging completed successfully based on log analysis.");
        }
        else
        {
            logger.LogError("Game packaging failed based on log analysis.");
            return false;
        }

        var downloadSuccess = await linuxUeRunner.DownloadDirectory(remoteOutputDir, GameDir);
        if (downloadSuccess)
        {
            logger.LogInformation($"Successfully downloaded game package to {GameDir}");
        }
        else
        {
            logger.LogError($"Failed to download game package from {remoteOutputDir}");
            return false;
        }

        return true;
    }

    private async Task CopyPackageToDestination(UEVersion engineVersion)
    {
        try
        {
            var pluginDestinationPath = GetEngineTargetPluginDirectory(engineVersion);

            var localPackagedPluginDir = Path.Combine(PackageDir, "Plugin");
            if (!FileSystem.DirectoryExists(localPackagedPluginDir))
            {
                logger.LogWarning($"Packaged plugin directory not found locally: {localPackagedPluginDir}");
                return;
            }

            if (await linuxUeRunner.DirectoryExists(pluginDestinationPath))
            {
                await linuxUeRunner.DeleteDirectory(pluginDestinationPath);
                logger.LogInformation($"Removed existing plugin at: {pluginDestinationPath}");
            }

            await linuxUeRunner.MakeDirectory(pluginDestinationPath);
            await linuxUeRunner.CopyDirectory(localPackagedPluginDir, pluginDestinationPath);

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
        return
            $"{LinuxConfig.LinuxUnrealBasePath}/UE_{engineVersion.ToVersionString()}/Engine/Plugins/Marketplace/{_pluginConfig.PluginName}";
    }
}
