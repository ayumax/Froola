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
/// Mac project packaging implementation.
/// </summary>
public class MacBuilder(
    PackageConfig packageConfig,
    WindowsConfig windowsConfig,
    MacConfig macConfig,
    IMacUnrealEngineRunner macUeRunner,
    IFileSystem fileSystem,
    IFroolaLogger<MacBuilder> logger)
    : BuilderBase(packageConfig, windowsConfig, macConfig, logger, fileSystem),
        IMacBuilder
{
    private readonly PackageConfig _packageConfig = packageConfig;
    private readonly MacConfig _macConfig = macConfig;

    private bool _isReady;

    /// <summary>
    /// Gets the editor platform for this builder (Mac).
    /// </summary>
    protected override EditorPlatform MyEditorPlatform => EditorPlatform.Mac;

    /// <summary>
    /// Not implemented for project-only builder.
    /// </summary>
    public Task<BuildResult> Run(UEVersion engineVersion)
    {
        throw new NotImplementedException("Run is not used for project packaging. Use RunPackage instead.");
    }

    /// <summary>
    /// Runs the Mac project packaging process for the specified engine version.
    /// </summary>
    public async Task<BuildResult> RunPackage(UEVersion engineVersion)
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
            var originalXcodePath = string.Empty;
            var xcodeSwitched = false;
            if (_macConfig.XcodeNamesWithVersion.TryGetValue(engineVersion, out var xcodeName) &&
                !string.IsNullOrEmpty(xcodeName))
            {
                logger.LogInformation($"Switching Xcode: {xcodeName}");
                originalXcodePath = await macUeRunner.SwitchXcode(xcodeName);
                xcodeSwitched = true;
            }

            try
            {
                var macPackagePath = Path.Combine(RepositoryPath, "packages").Replace("\\", "/");
                if (!await macUeRunner.DirectoryExists(macPackagePath))
                {
                    await macUeRunner.MakeDirectory(macPackagePath);
                }

                var buildCookRunArgs = UECommandsHelper.GetBuildCookRunArgs(ProjectFilePath, macPackagePath,
                    MyPlatform(), MyEditorPlatform);

                logger.LogInformation("Executing project package script...");

                var command = $"\"{RunUatBatPath}\" {buildCookRunArgs}";

                await using var writer = new StreamWriter(Path.Combine(PackageDir, "BuildCookRun.log"));

                await foreach (var logLine in macUeRunner.ExecuteRemoteScriptWithLogsAsync(command,
                                   _packageConfig.EnvironmentVariableMap))
                {
                    logger.LogInformation(logLine);
                    await writer.WriteLineAsync(logLine);
                }

                logger.LogInformation("Project packaging finished");

                var downloadSuccess = await macUeRunner.DownloadDirectory(macPackagePath, PackageDir);

                logger.LogInformation(downloadSuccess
                    ? $"Successfully downloaded project package to {PackageDir}"
                    : $"Failed to download project package from {macPackagePath}");

                result.StatusOfPackage = CheckProjectPackageBuildResult(engineVersion, _packageConfig.ProjectName);
                if (result.StatusOfPackage == BuildStatus.Success)
                {
                    ZipPackage(engineVersion);
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
            logger.LogError($"Error during project packaging: {ex.Message}", ex);
            result.StatusOfPackage = BuildStatus.Failed;
            return result;
        }
    }

    /// <inheritdoc cref="IBuilder" />
    public override async Task PrepareRepository(string baseRepositoryPath, UEVersion engineVersion)
    {
        _isReady = false;

        RepositoryPath =
            $"/tmp/{_packageConfig.ProjectName}_Package/{DateTime.Now:yyyyMMdd_HHmmss}/{engineVersion.ToVersionString()}";

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
        return _packageConfig.PackagePlatforms.Where(x => x is GamePlatform.Mac or GamePlatform.IOS).ToList();
    }
}
