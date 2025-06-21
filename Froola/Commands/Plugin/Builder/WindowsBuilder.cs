using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Diagnostics;
using Froola.Configs;
using Froola.Interfaces;
using Froola.Utils;

namespace Froola.Commands.Plugin.Builder;

/// <summary>
///     WindowsBuilder handles build, test, and packaging processes for the Windows platform.
/// </summary>
[SuppressMessage("ReSharper", "InvertIf")]
public class WindowsBuilder(
    PluginConfig pluginConfig,
    WindowsConfig windowsConfig,
    MacConfig macConfig,
    IUnrealEngineRunner unrealRunner,
    IFileSystem fileSystem,
    ITestResultsEvaluator testResultsEvaluator,
    IFroolaLogger<WindowsBuilder> logger)
    : BuilderBase(pluginConfig, windowsConfig, macConfig, logger, fileSystem, testResultsEvaluator), IWindowsBuilder
{
    private readonly PluginConfig _pluginConfig = pluginConfig;
    private readonly WindowsConfig _windowsConfig = windowsConfig;
    private readonly IFileSystem _fileSystem = fileSystem;

    /// <summary>
    ///     Gets the editor platform type for Windows.
    /// </summary>
    protected override EditorPlatform MyEditorPlatform => EditorPlatform.Windows;

    /// <summary>
    /// Runs Windows test for the specified engine version.
    /// </summary>
    public async Task<BuildResult> Run(UEVersion engineVersion)
    {
        var result = new BuildResult
        {
            Os = EditorPlatform.Windows,
            EngineVersion = engineVersion,
            StatusOfBuild = BuildStatus.None,
            StatusOfTest = BuildStatus.None,
            StatusOfPackage = BuildStatus.None
        };

        // Build
        result.StatusOfBuild = await BuildAsync() ? BuildStatus.Success : BuildStatus.Failed;
        if (result.StatusOfBuild == BuildStatus.Failed)
        {
            return result;
        }

        if (_pluginConfig.RunTest)
        {
            // Test
            if (!await TestAsync())
            {
                result.StatusOfTest = BuildStatus.Failed;
                return result;
            }

            result.StatusOfTest = CheckTestResult(engineVersion);

            logger.LogInformation(
                $"Final test result: {(result.StatusOfTest == BuildStatus.Success ? "SUCCESS" : "FAILURE")}");
        }

        if (_pluginConfig.RunPackage)
        {
            // Package
            if (!await PackageBuildAsync(RepositoryPath, _pluginConfig.PluginName, PackageDir))
            {
                result.StatusOfPackage = BuildStatus.Failed;
                return result;
            }

            result.StatusOfPackage = CheckPackageBuildResult(engineVersion);

            // Copy package to destination if configured
            if (result.StatusOfPackage == BuildStatus.Success && _pluginConfig.CopyPackageAfterBuild)
            {
                await CopyPackageToDestination(engineVersion);
            }
        }

        return result;
    }

    /// <inheritdoc cref="IBuilder" />
    public override Task PrepareRepository(string baseRepositoryPath, UEVersion engineVersion)
    {
        RepositoryPath = Path.Combine(baseRepositoryPath, "..", "Windows");

        _fileSystem.CopyDirectory(baseRepositoryPath, RepositoryPath);

        if (_pluginConfig.CopyPackageAfterBuild)
        {
            var destinationPath = GetEngineTargetPluginDirectory(engineVersion);

            // Remove existing plugin if it exists
            if (_fileSystem.DirectoryExists(destinationPath))
            {
                _fileSystem.DeleteDirectory(destinationPath, true);
                logger.LogInformation($"Removed existing plugin at: {destinationPath}");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleans up the temporary directory for Windows builds.
    /// </summary>
    public Task CleanupTempDirectory()
    {
        try
        {
            _fileSystem.DeleteDirectory(RepositoryPath, true);
        }
        catch (Exception e)
        {
            logger.LogWarning(e.Message);
        }
       
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Builds the project asynchronously for the specified engine version.
    /// </summary>
    /// <summary>
    ///     Builds the project asynchronously for the specified engine version.
    /// </summary>
    protected virtual async Task<bool> BuildAsync()
    {
        try
        {
            var workingDirectory = Path.GetDirectoryName(ProjectFilePath)!;

            var logFilePath = Path.Combine(BuildResultDir, "Build.log");

            logger.LogInformation("Starting build process...");
            await unrealRunner.RunBuildScript(BuildBatPath, BuildProjectArgs, workingDirectory, logFilePath);
            logger.LogInformation("Build completed successfully.");
        }
        catch (ProcessErrorException ex)
        {
            // If the exit code is 0, it means the packaging process was successful, so we don't consider it a failure.
            if (ex.ExitCode != 0)
            {
                logger.LogInformation($"BUILD ERROR Process error: {ex.Message}");
                logger.LogInformation($"Build failed with exit code: {ex.ExitCode}");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Runs automation tests asynchronously for the specified engine version.
    /// </summary>
    /// <summary>
    ///     Runs automation tests asynchronously for the specified engine version.
    /// </summary>
    protected virtual async Task<bool> TestAsync()
    {
        try
        {
            var workingDirectory = Path.GetDirectoryName(ProjectFilePath)!;

            var testCommandArgs =
                UECommandsHelper.GetAutomationTestCommandArgs(ProjectFilePath, _pluginConfig.PluginName,
                    TestResultDir, EditorPlatform.Windows);

            logger.LogInformation($"Test command: {EditorPath} {testCommandArgs}");

            var logFilePath = Path.Combine(TestResultDir, "AutomationTest.log");

            await unrealRunner.RunUnrealEditor(EditorPath, testCommandArgs, workingDirectory, logFilePath);
        }
        catch (ProcessErrorException ex)
        {
            // If the exit code is 0, it means the packaging process was successful, so we don't consider it a failure.
            if (ex.ExitCode != 0)
            {
                logger.LogInformation($"Process error: {ex.Message}");
                logger.LogInformation($"Test process exited with code: {ex.ExitCode}");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Builds the plugin package asynchronously for the specified engine version.
    /// </summary>
    /// <summary>
    ///     Builds the plugin package asynchronously for the specified engine version.
    /// </summary>
    protected virtual async Task<bool> PackageBuildAsync(string repoPath, string pluginName, string outputDir)
    {
        var upluginPath = UECommandsHelper.GetUpluginPath(repoPath, pluginName, EditorPlatform.Windows);
        var workingDirectory = Path.GetDirectoryName(upluginPath)!;

        try
        {
            var buildPluginArgs = UECommandsHelper.GetBuildPluginArgs(repoPath, pluginName, outputDir,
                MyPlatform(), EditorPlatform.Windows);

            var logFilePath = Path.Combine(outputDir, "BuildPlugin.log");

            await unrealRunner.RunBuildScript(RunUatBatPath, buildPluginArgs, workingDirectory, logFilePath);
            logger.LogInformation("Plugin packaging completed successfully.");
        }
        catch (ProcessErrorException ex)
        {
            // If the exit code is 0, it means the packaging process was successful, so we don't consider it a failure.
            if (ex.ExitCode != 0)
            {
                logger.LogError($"Plugin packaging failed: {ex.Message}");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Copies the packaged plugin to the configured destination path
    /// </summary>
    /// <param name="engineVersion">Engine version</param>
    private async Task CopyPackageToDestination(UEVersion engineVersion)
    {
        try
        {
            var destinationPath = GetEngineTargetPluginDirectory(engineVersion);

            // Find the packaged plugin directory
            var packagedPluginDir = Path.Combine(PackageDir, "Plugin");
            if (!_fileSystem.DirectoryExists(packagedPluginDir))
            {
                logger.LogWarning($"Packaged plugin directory not found: {packagedPluginDir}");
                return;
            }
            
            // Remove existing plugin if it exists
            if (_fileSystem.DirectoryExists(destinationPath))
            {
                _fileSystem.DeleteDirectory(destinationPath, true);
                logger.LogInformation($"Removed existing plugin at: {destinationPath}");
            }

            _fileSystem.CreateDirectory(destinationPath);

            // Copy the packaged plugin
            _fileSystem.CopyDirectory(packagedPluginDir, destinationPath);

            logger.LogInformation($"Successfully copied packaged plugin from {packagedPluginDir} to {destinationPath}");
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
        return Path.Combine(_windowsConfig.WindowsUnrealBasePath, $"UE_{engineVersion.ToVersionString()}",
            @"Engine\Plugins\Marketplace", _pluginConfig.PluginName);
    }

    /// <summary>
    ///     Gets the game platforms for the specified input parameters.
    /// </summary>
    protected override List<GamePlatform> MyPlatform()
    {
        return _pluginConfig.PackagePlatforms.Where(x => x is GamePlatform.Win64 or GamePlatform.Android).ToList();
    }
}